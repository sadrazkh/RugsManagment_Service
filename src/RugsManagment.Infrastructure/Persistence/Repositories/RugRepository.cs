using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Ledger;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// فرش: بارگذاری ردیابی‌شده برای ویرایش، فهرست صفحه‌بندی‌شده (همه در SQL)، شماره و پیشنهاد مقدار، و سطل زباله.
///
/// <para><b>بها از فاز ۲:</b> سه جای قدیمیِ «سرمایهٔ کل» در این کلاس (مرتب‌سازی، ردیف فهرست، سطل زباله — جاهای ۲ تا ۴ در
/// جدول D.4) دیگر فرمول ندارند و ستون <c>RugCostTotal.Total</c> را می‌خوانند که پاس سازگاری می‌نویسد؛ پس فهرست، جزئیات و
/// گزارش‌ها یک عدد می‌بینند. ردیف مشتق‌شده فقط از projection بدون ردیابی خوانده می‌شود، هرگز با Include روی موجودیت
/// ردیابی‌شده (D.4 گام ۸)؛ بارگذاری ردیابی‌شده ردیفش را در <see cref="IRugCostTotalReader"/> می‌گذارد.</para>
/// </summary>
public class RugRepository : Repository<Rug>, IRugRepository
{
    private readonly IRugCostTotalReader _costTotals;

    /// <param name="tracking">فقط تا ردیاب «وصل‌شده بی بارگذاری» پیش از هر بارگذاری فرش ساخته شده باشد.</param>
    public RugRepository(AppDbContext db, LedgerTracking tracking, IRugCostTotalReader costTotals)
        : base(db)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        _costTotals = costTotals;
    }

    /// <summary>
    /// AsSplitQuery: چند مجموعهٔ مستقل (عکس‌ها، مراحل، سطرهای خرید) در یک JOIN ضرب دکارتی می‌سازند — فرشی با ۱۲ عکس و
    /// ۱۰ مرحله ۱۲۰ ردیف تکراری برمی‌گرداند. با تقسیم، هر مجموعه کوئری جدای خودش را دارد و ردیف‌ها جمع می‌شوند نه ضرب.
    /// </summary>
    public async Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rug = await Db.Rugs
            .Include(r => r.Batch)
            .Include(r => r.Sale)
            .Include(r => r.Images.OrderBy(i => i.SortOrder))
            .Include(r => r.WorkflowSteps.OrderBy(s => s.OrderIndex))
                .ThenInclude(s => s.ProcessStepType)
            .Include(r => r.WorkflowSteps)
                .ThenInclude(s => s.ServiceProvider)
            .Include(r => r.PurchaseLines.OrderBy(l => l.SortOrder))
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

        if (rug is not null)
            await _costTotals.PreloadAsync([rug.Id], cancellationToken);
        return rug;
    }

    public async Task<IReadOnlyList<Rug>> ListByTenantAsync(
        Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default)
    {
        var query = Db.Rugs
            .AsNoTracking()
            .Include(r => r.Batch)
            .Include(r => r.Sale)
            .Include(r => r.WorkflowSteps)
                .ThenInclude(s => s.ProcessStepType)
            // برای گزارش کهنگی لازم است: «چه کسی این مرحله را نگه داشته»
            .Include(r => r.WorkflowSteps)
                .ThenInclude(s => s.ServiceProvider)
            .Include(r => r.PurchaseLines)
            .AsSplitQuery()
            .Where(r => r.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var rugs = await query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync(cancellationToken);

        // داشبورد و تحلیل‌ها برای هر فرش بها می‌خوانند؛ یک پرس‌وجو برای همه، نه یکی برای هر فرش
        await _costTotals.PreloadTenantAsync(tenantId, cancellationToken);
        return rugs;
    }

    /// <summary>
    /// <c>Update()</c> فقط روی فرشِ ردیابی‌شده (spec B.1، D.4 «Bypass rule»): فرشی که بی بارگذاری ردیابی‌شده Update شود
    /// مقدار اصلی‌اش را گم می‌کند و پاس سازگاری تغییرش را نمی‌بیند، پس رد می‌شود. فرشِ «افزوده» دست نمی‌خورد (Update آن را
    /// Modified می‌کرد و INSERT به UPDATE روی ردیف ناموجود تبدیل می‌شد).
    /// </summary>
    public override void Update(Rug entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        switch (Db.Entry(entity).State)
        {
            case EntityState.Detached:
                throw new InvalidOperationException(
                    "فرش ردیابی‌نشده Update نمی‌شود؛ آن را با GetWithWorkflowAsync بارگذاری کنید (پاس سازگاری مقدار اصلی را لازم دارد).");
            case EntityState.Added:
                return;
            default:
                base.Update(entity);
                return;
        }
    }

    /// <summary>
    /// فهرست صفحه‌بندی‌شده. کل کار در SQL انجام می‌شود:
    /// فیلتر → شمارش → مرتب‌سازی → برش صفحه → projection.
    /// هیچ موجودیت Rug ساخته نمی‌شود، پس هزینه به تعداد کل فرش‌های کارگاه وابسته نیست.
    /// </summary>
    public async Task<PagedResult<RugListItemDto>> SearchAsync(
        Guid tenantId, RugQuery query, CancellationToken cancellationToken = default)
    {
        query = query.Sanitized();

        var filtered = ApplyFilters(Db.Rugs.AsNoTracking().Where(r => r.TenantId == tenantId), query);

        var totalCount = await filtered.CountAsync(cancellationToken);
        if (totalCount == 0)
            return new PagedResult<RugListItemDto>([], 0, query.Page, query.PageSize);

        // مرتب‌سازی روی موجودیت انجام می‌شود، نه روی نتیجهٔ projection:
        // EF نمی‌تواند OrderBy روی عضوی از یک record ساخته‌شده در Select را به SQL ترجمه کند.
        var items = await Project(ApplySort(filtered, query))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RugListItemDto>(items, totalCount, query.Page, query.PageSize);
    }

    private IQueryable<Rug> ApplyFilters(IQueryable<Rug> source, RugQuery query)
    {
        // خط‌های محموله (فاز محموله): فرش ناوبری به خطش ندارد، پس زیرپرس‌وجوی همبسته روی همان DbContext
        var lines = Db.Set<ShipmentLine>();

        if (query.Status.HasValue)
            source = source.Where(r => r.Status == query.Status.Value);

        if (query.BatchId.HasValue)
            source = source.Where(r => r.BatchId == query.BatchId.Value);

        if (query.WithoutBatch)
            source = source.Where(r => r.BatchId == null);

        // «الان روی این نوع مرحله است» یعنی مرحله‌ای با وضعیت InProgress از این نوع دارد
        if (query.StepTypeId.HasValue)
            source = source.Where(r => r.WorkflowSteps.Any(s =>
                s.Status == WorkflowStepStatus.InProgress && s.ProcessStepTypeId == query.StepTypeId.Value));

        if (query.CreatedFrom.HasValue)
            source = source.Where(r => r.CreatedAt >= query.CreatedFrom.Value);

        if (query.CreatedTo.HasValue)
            source = source.Where(r => r.CreatedAt <= query.CreatedTo.Value);

        // ── محموله و نگله (فاز محموله): فرش‌هایی که خطی در این محموله / این نگله دارند، یا هیچ خطی ندارند ──
        if (query.ShipmentId is Guid shipmentId)
            source = source.Where(r => lines.Any(l => l.RugId == r.Id && l.ShipmentId == shipmentId));

        if (query.BaleId is Guid baleId)
            source = source.Where(r => lines.Any(l => l.RugId == r.Id && l.BaleId == baleId));

        if (query.NotBaled)
            source = source.Where(r => !lines.Any(l => l.RugId == r.Id));

        // ── جای فرش (C.10)، همان ترتیب RugLocationRules؛ سطل زباله فهرست جدا دارد ──
        if (query.Location is RugLocation location)
        {
            source = location switch
            {
                RugLocation.Sold => source.Where(r => r.Sale != null),
                RugLocation.Exported => source.Where(r => r.Sale == null
                    && lines.Any(l => l.RugId == r.Id && l.Shipment.Status == ShipmentStatus.Finalized)),
                RugLocation.InBale => source.Where(r => r.Sale == null
                    && lines.Any(l => l.RugId == r.Id && l.Shipment.Status == ShipmentStatus.Open)),
                RugLocation.InStock => source.Where(r => r.Sale == null && !lines.Any(l => l.RugId == r.Id)),
                _ => source.Where(r => false)
            };
        }

        if (query.Serial is { } serialText)
            source = ApplySerialFilter(source, serialText);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // هر دو طرف مقایسه یکسان‌سازی می‌شوند تا «كاشان» عربی هم با «کاشان» فارسی پیدا شود.
            // جایگزینی‌ها عمداً inline نوشته شده‌اند: فراخوانی متد کمکی داخل expression tree
            // توسط EF ترجمه نمی‌شود (string.Replace خودش به replace() پستگرس ترجمه می‌شود).
            // شماره با ارقام لاتین ذخیره می‌شود و Normalize ارقام فارسی را لاتین می‌کند، پس «۳۷۷۸» هم پیدا می‌شود.
            var pattern = $"%{PersianText.Normalize(query.Search)}%";

            source = source.Where(r =>
                EF.Functions.ILike(r.Sku, pattern)
                || (r.SerialNo != null && EF.Functions.ILike(r.SerialNo, pattern))
                || (r.Title != null && EF.Functions.ILike(
                    r.Title.Replace(PersianText.ArabicYeh, PersianText.PersianYeh)
                           .Replace(PersianText.ArabicKaf, PersianText.PersianKaf), pattern))
                || (r.Origin != null && EF.Functions.ILike(
                    r.Origin.Replace(PersianText.ArabicYeh, PersianText.PersianYeh)
                            .Replace(PersianText.ArabicKaf, PersianText.PersianKaf), pattern))
                || (r.Pattern != null && EF.Functions.ILike(
                    r.Pattern.Replace(PersianText.ArabicYeh, PersianText.PersianYeh)
                             .Replace(PersianText.ArabicKaf, PersianText.PersianKaf), pattern))
                || (r.Material != null && EF.Functions.ILike(
                    r.Material.Replace(PersianText.ArabicYeh, PersianText.PersianYeh)
                              .Replace(PersianText.ArabicKaf, PersianText.PersianKaf), pattern))
                || (r.Notes != null && EF.Functions.ILike(
                    r.Notes.Replace(PersianText.ArabicYeh, PersianText.PersianYeh)
                           .Replace(PersianText.ArabicKaf, PersianText.PersianKaf), pattern)));
        }

        return source;
    }

    /// <summary>
    /// شماره، دقیق و نه «شامل» (همان قاعدهٔ <c>/api/rugs/by-serial</c>): عدد تک → شمارهٔ همین عدد یا دسته‌ای که آن را دارد؛
    /// دسته → فرش‌هایی که بازه‌شان با آن اشتراک دارد؛ متن → همان متن. متنِ نامعتبر هیچ ردیفی نمی‌دهد (از آدرس صفحه می‌آید).
    /// </summary>
    private static IQueryable<Rug> ApplySerialFilter(IQueryable<Rug> source, string text)
    {
        if (!SerialNumber.TryParse(text, out var serial, out _) || serial is null)
            return source.Where(r => false);

        var display = serial.Display;
        if (serial.From is int from && serial.To is int to)
        {
            return source.Where(r => r.SerialNo == display
                                     || (r.SerialFrom != null && r.SerialFrom <= to && from <= r.SerialTo));
        }

        return source.Where(r => r.SerialNo == display);
    }

    /// <summary>
    /// ساخت ردیف سبک فهرست. بها از <c>RugCostTotal</c> (پاس سازگاری)، نه فرمول؛ مرحلهٔ جاری با زیرکوئری همبسته.
    /// هر عضو فاز ۲ صریح پاس داده می‌شود: داخل expression tree آرگومان اختیاریِ حذف‌شده مقدار پیش‌فرض بی‌صدا می‌گیرد (I.1).
    /// </summary>
    private IQueryable<RugListItemDto> Project(IQueryable<Rug> source) => Project(source, Db.Set<ShipmentLine>());

    private static IQueryable<RugListItemDto> Project(IQueryable<Rug> source, IQueryable<ShipmentLine> lines) => source
        .Select(r => new RugListItemDto(
            r.Id,
            r.Sku,
            r.Title,
            r.Origin,
            r.Pattern,
            r.WidthMeters,
            r.LengthMeters,
            // مساحت یک تخته در اندازهٔ مؤثر، همان ستون تولیدشدهٔ گردشده (I.3)
            r.AreaM2,
            r.Status,
            // بندانگشتیِ عکس شاخص؛ اگر نسخهٔ بندانگشتی نداشت، خودِ تصویر
            r.Images
                .Where(i => i.IsPrimary)
                .Select(i => "/media/rugs/" + r.Id.ToString() + "/" +
                             (i.ThumbnailFileName != null ? i.ThumbnailFileName : i.FileName))
                .FirstOrDefault(),
            r.BatchId,
            r.Batch != null ? r.Batch.Name : null,
            r.WorkflowSteps
                .Where(s => s.Status == WorkflowStepStatus.InProgress)
                .OrderBy(s => s.OrderIndex)
                .Select(s => s.ProcessStepType.NameFa)
                .FirstOrDefault(),
            r.WorkflowSteps
                .Where(s => s.Status == WorkflowStepStatus.InProgress)
                .OrderBy(s => s.OrderIndex)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefault(),
            // جای ۳ از هفت جای «سرمایهٔ کل» (D.4): ستون جمعی که پاس نوشته
            r.CostTotal != null ? r.CostTotal.Total : (decimal?)null,
            r.WorkflowSteps.Count(s =>
                s.Status == WorkflowStepStatus.Completed || s.Status == WorkflowStepStatus.Skipped),
            r.WorkflowSteps.Count,
            r.CreatedAt,
            r.SerialNo,
            r.Quantity,
            r.TotalAreaM2,
            r.CostTotal != null,
            r.CostTotal != null ? r.CostTotal.CostPerM2 : null,
            // ترتیب C.10 (همان RugLocationRules): سطل زباله ← فروش ← خط در محمولهٔ قطعی ← خط در محمولهٔ باز ← انبار
            r.DeletedAt != null ? RugLocation.Trashed
                : r.Sale != null ? RugLocation.Sold
                : lines.Where(l => l.RugId == r.Id)
                      .Select(l => (RugLocation?)(l.Shipment.Status == ShipmentStatus.Finalized ? RugLocation.Exported : RugLocation.InBale))
                      .FirstOrDefault() ?? RugLocation.InStock,
            lines.Where(l => l.RugId == r.Id).Select(l => (int?)l.Bale.Number).FirstOrDefault(),
            lines.Where(l => l.RugId == r.Id).Select(l => (int?)l.Shipment.Number).FirstOrDefault(),
            EF.Property<uint>(r, "xmin")));

    /// <summary>
    /// مرتب‌سازی روی خود موجودیت (قبل از projection) — تنها شکلی که EF به ORDER BY ترجمه می‌کند.
    /// Id به‌عنوان معیار آخر می‌آید تا ترتیب بین صفحه‌ها پایدار و بدون تکرار/جاافتادگی باشد.
    /// </summary>
    private static IQueryable<Rug> ApplySort(IQueryable<Rug> source, RugQuery query)
    {
        var descending = query.Descending;

        IOrderedQueryable<Rug> ordered = query.SortBy switch
        {
            RugSortBy.Sku => descending
                ? source.OrderByDescending(r => r.Sku)
                : source.OrderBy(r => r.Sku),

            RugSortBy.Title => descending
                ? source.OrderByDescending(r => r.Title)
                : source.OrderBy(r => r.Title),

            // متراژ کل ردیف (دسته‌ی ۵۰ تختهٔ ۰٫۱۶ = ۸٫۰۰)، بعد مساحت یک تخته
            RugSortBy.Area => descending
                ? source.OrderByDescending(r => r.TotalAreaM2).ThenByDescending(r => r.AreaM2)
                : source.OrderBy(r => r.TotalAreaM2).ThenBy(r => r.AreaM2),

            // جای ۲ از هفت جای «سرمایهٔ کل» (D.4): ستون جمع؛ فرشی که هنوز ردیف ندارد مثل صفر
            RugSortBy.TotalCost => descending
                ? source.OrderByDescending(r => r.CostTotal != null ? r.CostTotal.Total : 0m)
                : source.OrderBy(r => r.CostTotal != null ? r.CostTotal.Total : 0m),

            RugSortBy.Status => descending
                ? source.OrderByDescending(r => r.Status)
                : source.OrderBy(r => r.Status),

            RugSortBy.CurrentStep => descending
                ? source.OrderByDescending(r => r.WorkflowSteps
                    .Where(s => s.Status == WorkflowStepStatus.InProgress)
                    .OrderBy(s => s.OrderIndex)
                    .Select(s => s.ProcessStepType.NameFa)
                    .FirstOrDefault())
                : source.OrderBy(r => r.WorkflowSteps
                    .Where(s => s.Status == WorkflowStepStatus.InProgress)
                    .OrderBy(s => s.OrderIndex)
                    .Select(s => s.ProcessStepType.NameFa)
                    .FirstOrDefault()),

            // شمارهٔ عددی بر اساس اولِ بازه؛ شمارهٔ متنی و بعد بی‌شماره همیشه آخر (در هر دو جهت)
            RugSortBy.Serial => descending
                ? source.OrderBy(r => r.SerialSort == null).ThenBy(r => r.SerialNo == null)
                    .ThenByDescending(r => r.SerialSort).ThenByDescending(r => r.SerialNo)
                : source.OrderBy(r => r.SerialSort == null).ThenBy(r => r.SerialNo == null)
                    .ThenBy(r => r.SerialSort).ThenBy(r => r.SerialNo),

            _ => descending
                ? source.OrderByDescending(r => r.CreatedAt)
                : source.OrderBy(r => r.CreatedAt)
        };

        return ordered.ThenBy(r => r.Id);
    }

    /// <summary>
    /// SKU خودکار به شکل RUG-yyyyMM-NNNN (مسیر قدیمی؛ از فاز ۲ ثبت فرش شمارنده‌های کارگاه را به کار می‌برد).
    /// شماره از «بزرگ‌ترین شمارهٔ موجود در همان ماه و همان کارگاه» گرفته می‌شود، نه از تعداد کل رکوردها.
    /// </summary>
    public async Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var prefix = $"RUG-{DateTime.UtcNow:yyyyMM}-";

        // IgnoreQueryFilters لازم است: ردیف حذف‌شدهٔ نرم هنوز جای SKU را در ایندکس یکتا اشغال می‌کند
        var existing = await Db.Rugs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Sku.StartsWith(prefix))
            .Select(r => r.Sku)
            .ToListAsync(cancellationToken);

        var maxSequence = existing
            .Select(sku => int.TryParse(sku[prefix.Length..], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return prefix + (maxSequence + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
    }

    // ── شمارهٔ بازرگان ─────────────────────────────────────────────

    public async Task<int?> GetMaxSerialAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.Rugs
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.SerialFrom != null)
            .MaxAsync(r => (int?)(r.SerialTo ?? r.SerialFrom), cancellationToken);

    public async Task<RugBySerialDto?> FindBySerialAsync(
        Guid tenantId, SerialNumber serial, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serial);
        var active = Db.Rugs.AsNoTracking().Where(r => r.TenantId == tenantId);
        var display = serial.Display;
        var single = serial.From is int n && serial.To == n ? n : (int?)null;

        // ۱) دقیق: همان متن، یا شمارهٔ تکِ همان عدد («0123» = «123»)
        var exact = single is int value
            ? active.Where(r => r.SerialNo == display || (r.SerialFrom == value && r.SerialTo == value))
            : active.Where(r => r.SerialNo == display);
        var hit = await Project(OrderBySerial(exact)).FirstOrDefaultAsync(cancellationToken);
        if (hit is not null)
            return new RugBySerialDto(hit, hit.Location, SerialMatchType.Exact);

        // ۲) عدد تک داخل یک دسته
        if (single is int number)
        {
            var inRange = active.Where(r => r.SerialFrom != null && r.SerialFrom <= number && number <= r.SerialTo);
            hit = await Project(OrderBySerial(inRange)).FirstOrDefaultAsync(cancellationToken);
            if (hit is not null)
                return new RugBySerialDto(hit, hit.Location, SerialMatchType.InRange);
        }

        return null;
    }

    private static IQueryable<Rug> OrderBySerial(IQueryable<Rug> source)
        => source.OrderBy(r => r.SerialSort == null).ThenBy(r => r.SerialSort).ThenBy(r => r.Id);

    // ── پیشنهاد مقدار ─────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> ListDistinctValuesAsync(
        Guid tenantId, RugValuesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query = query.Sanitized();
        var pattern = query.Search is null ? null : $"%{PersianText.Normalize(query.Search)}%";

        if (query.Column == RugValueColumn.Custom)
            return await ListCustomValuesAsync(tenantId, query.CustomKey, pattern, query.Take, cancellationToken);

        var rugs = Db.Rugs.AsNoTracking().Where(r => r.TenantId == tenantId);
        IQueryable<string?> values = query.Column switch
        {
            RugValueColumn.Title => rugs.Select(r => r.Title),
            RugValueColumn.Origin => rugs.Select(r => r.Origin),
            RugValueColumn.Pattern => rugs.Select(r => r.Pattern),
            RugValueColumn.Material => rugs.Select(r => r.Material),
            RugValueColumn.Seller => Db.RugBatches.AsNoTracking().Where(b => b.TenantId == tenantId).Select(b => b.SellerName),
            _ => rugs.Select(r => (string?)null).Where(_ => false)
        };

        values = values.Where(v => v != null && v != "");
        if (pattern is not null)
        {
            values = values.Where(v => EF.Functions.ILike(
                v!.Replace(PersianText.ArabicYeh, PersianText.PersianYeh)
                  .Replace(PersianText.ArabicKaf, PersianText.PersianKaf), pattern));
        }

        return await values
            .GroupBy(v => v!)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Value)
            .Take(query.Take)
            .Select(x => x.Value)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// فیلد سفارشی از <c>MetadataJson -&gt;&gt; key</c>؛ کلید فقط پارامتر است، هرگز در متن SQL (قرارداد <see cref="RugValuesQuery"/>).
    /// </summary>
    private async Task<IReadOnlyList<string>> ListCustomValuesAsync(
        Guid tenantId, string? key, string? pattern, int take, CancellationToken cancellationToken)
    {
        if (!RugValuesQuery.IsValidCustomKey(key))
            return [];

        const string sql = """
            SELECT x.v AS "Value"
            FROM (
                SELECT btrim(r."MetadataJson" ->> @key) AS v
                FROM "Rugs" r
                WHERE r."TenantId" = @tenant AND r."DeletedAt" IS NULL
                    AND r."MetadataJson" IS NOT NULL AND jsonb_typeof(r."MetadataJson") = 'object'
            ) x
            WHERE x.v IS NOT NULL AND x.v <> ''
                AND (@pattern::text IS NULL OR replace(replace(x.v, @arabicYeh, @persianYeh), @arabicKaf, @persianKaf) ILIKE @pattern)
            GROUP BY x.v
            ORDER BY count(*) DESC, x.v
            LIMIT @take
            """;

        return await Db.Database
            .SqlQueryRaw<string>(sql,
                new NpgsqlParameter("key", NpgsqlDbType.Text) { Value = key! },
                new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId },
                new NpgsqlParameter("pattern", NpgsqlDbType.Text) { Value = (object?)pattern ?? DBNull.Value },
                new NpgsqlParameter("arabicYeh", NpgsqlDbType.Text) { Value = PersianText.ArabicYeh },
                new NpgsqlParameter("persianYeh", NpgsqlDbType.Text) { Value = PersianText.PersianYeh },
                new NpgsqlParameter("arabicKaf", NpgsqlDbType.Text) { Value = PersianText.ArabicKaf },
                new NpgsqlParameter("persianKaf", NpgsqlDbType.Text) { Value = PersianText.PersianKaf },
                new NpgsqlParameter("take", NpgsqlDbType.Integer) { Value = take })
            .ToListAsync(cancellationToken);
    }

    // ── سطل زباله ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.Rugs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.DeletedAt != null)
            .OrderByDescending(r => r.DeletedAt)
            .ThenBy(r => r.Id)
            .Select(r => new DeletedRugDto(
                r.Id,
                r.Sku,
                r.Title,
                r.Origin,
                r.WidthMeters,
                r.LengthMeters,
                r.Status,
                // جای ۴ از هفت جای «سرمایهٔ کل» (D.4): ستون جمع — تا کاربر بداند با حذف نهایی چه چیزی از دست می‌رود
                r.CostTotal != null ? r.CostTotal.Total : 0m,
                r.DeletedAt!.Value,
                Db.Users
                    .Where(u => u.Id == r.DeletedByUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
                r.SerialNo,
                r.Quantity))
            .ToListAsync(cancellationToken);

    public async Task<Rug?> GetDeletedAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.Rugs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.Id == id && r.TenantId == tenantId && r.DeletedAt != null, cancellationToken);
}
