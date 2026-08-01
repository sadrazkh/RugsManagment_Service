using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Providers;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>جستجوی کارگاه با slug — برای جلوگیری از تکرار هنگام ثبت مشتری جدید</summary>
public class TenantRepository(AppDbContext db) : Repository<Tenant>(db), ITenantRepository
{
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => await Db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
}

/// <summary>کاربر همراه Tenant — برای نمایش نام کارگاه در JWT و DTO</summary>
public class UserRepository(AppDbContext db) : Repository<User>(db), IUserRepository
{
    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await Db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
}

/// <summary>
/// فرش همراه تمام مراحل و نوع مرحله — برای صفحهٔ جزئیات و موتور WorkflowEngine.
/// </summary>
public class RugRepository(AppDbContext db) : Repository<Rug>(db), IRugRepository
{
    /// <summary>
    /// AsSplitQuery چون دو مجموعهٔ هم‌سطح (عکس‌ها و مراحل) با هم Include می‌شوند:
    /// در حالت تک‌کوئری، پستگرس حاصل‌ضرب دکارتی می‌دهد (۵ عکس × ۸ مرحله = ۴۰ ردیف
    /// که هر کدام کل ستون‌های فرش را تکرار می‌کند) و با زیاد شدن گالری بدتر می‌شود.
    ///
    /// رفت‌وبرگشت‌های جدا خارج از تراکنش‌اند، پس نظرياً می‌شود بین دو کوئری تغییری رخ دهد؛
    /// اینجا بی‌خطر است چون فرش و مراحل هر دو توکن هم‌زمانی xmin دارند و اگر خواندنِ
    /// ناهم‌خوان به نوشتن اشتباه منجر شود، SaveChanges آن را رد می‌کند.
    /// </summary>
    public async Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.Rugs
            .Include(r => r.Batch)
            .Include(r => r.Sale)
            .Include(r => r.Images.OrderBy(i => i.SortOrder))
            .Include(r => r.WorkflowSteps.OrderBy(s => s.OrderIndex))
                .ThenInclude(s => s.ProcessStepType)
            .Include(r => r.WorkflowSteps)
                .ThenInclude(s => s.ServiceProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

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
            .Where(r => r.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
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

    private static IQueryable<Rug> ApplyFilters(IQueryable<Rug> source, RugQuery query)
    {
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

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // هر دو طرف مقایسه یکسان‌سازی می‌شوند تا «كاشان» عربی هم با «کاشان» فارسی پیدا شود.
            // جایگزینی‌ها عمداً inline نوشته شده‌اند: فراخوانی متد کمکی داخل expression tree
            // توسط EF ترجمه نمی‌شود (string.Replace خودش به replace() پستگرس ترجمه می‌شود).
            var pattern = $"%{PersianText.Normalize(query.Search)}%";

            source = source.Where(r =>
                EF.Functions.ILike(r.Sku, pattern)
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
    /// هزینهٔ کل یک فرش به‌صورت عبارت قابل ترجمه به SQL.
    /// به‌عنوان Expression تعریف شده (نه متد) چون فراخوانی متد داخل expression tree
    /// توسط EF ترجمه نمی‌شود. باید با فرمول داخل <see cref="Project"/> یکی بماند.
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<Func<Rug, decimal>> TotalInvestmentExpression =
        r => (r.PurchaseCost ?? 0) + r.WorkflowSteps
            .Where(s => s.Status == WorkflowStepStatus.Completed || s.Status == WorkflowStepStatus.InProgress)
            .Sum(s => (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0) < 0
                ? 0
                : (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0));

    /// <summary>نام مرحلهٔ در حال انجام — برای مرتب‌سازی بر اساس مرحله.</summary>
    private static readonly System.Linq.Expressions.Expression<Func<Rug, string?>> CurrentStepNameExpression =
        r => r.WorkflowSteps
            .Where(s => s.Status == WorkflowStepStatus.InProgress)
            .OrderBy(s => s.OrderIndex)
            .Select(s => s.ProcessStepType.NameFa)
            .FirstOrDefault();

    /// <summary>
    /// ساخت ردیف سبک فهرست. هزینه و مرحلهٔ جاری به‌صورت زیرکوئری همبسته محاسبه می‌شوند.
    /// فرمول هزینه دقیقاً مطابق WorkflowEngine.CalculateRugCosts است:
    /// فقط مراحل تکمیل‌شده و در حال انجام، و هر مرحله هرگز منفی نمی‌شود.
    /// </summary>
    private static IQueryable<RugListItemDto> Project(IQueryable<Rug> source) => source
        .Select(r => new RugListItemDto(
            r.Id,
            r.Sku,
            r.Title,
            r.Origin,
            r.Pattern,
            r.WidthMeters,
            r.LengthMeters,
            r.WidthMeters * r.LengthMeters,
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
            (r.PurchaseCost ?? 0) + r.WorkflowSteps
                .Where(s => s.Status == WorkflowStepStatus.Completed || s.Status == WorkflowStepStatus.InProgress)
                .Sum(s => (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0) < 0
                    ? 0
                    : (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0)),
            r.WorkflowSteps.Count(s =>
                s.Status == WorkflowStepStatus.Completed || s.Status == WorkflowStepStatus.Skipped),
            r.WorkflowSteps.Count,
            r.CreatedAt));

    /// <summary>
    /// مرتب‌سازی روی خود موجودیت (قبل از projection) — تنها شکلی که EF به ORDER BY ترجمه می‌کند.
    /// ستون‌های محاسباتی مثل مساحت و هزینهٔ کل همان‌جا به‌صورت عبارت نوشته می‌شوند؛
    /// عبارت هزینه باید با Project هم‌خوان بماند.
    /// Id به‌عنوان معیار دوم می‌آید تا ترتیب بین صفحه‌ها پایدار و بدون تکرار/جاافتادگی باشد.
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

            RugSortBy.Area => descending
                ? source.OrderByDescending(r => r.WidthMeters * r.LengthMeters)
                : source.OrderBy(r => r.WidthMeters * r.LengthMeters),

            RugSortBy.TotalCost => descending
                ? source.OrderByDescending(TotalInvestmentExpression)
                : source.OrderBy(TotalInvestmentExpression),

            RugSortBy.Status => descending
                ? source.OrderByDescending(r => r.Status)
                : source.OrderBy(r => r.Status),

            RugSortBy.CurrentStep => descending
                ? source.OrderByDescending(CurrentStepNameExpression)
                : source.OrderBy(CurrentStepNameExpression),

            _ => descending
                ? source.OrderByDescending(r => r.CreatedAt)
                : source.OrderBy(r => r.CreatedAt)
        };

        return ordered.ThenBy(r => r.Id);
    }

    /// <summary>
    /// SKU خودکار به شکل RUG-yyyyMM-NNNN.
    /// شماره از «بزرگ‌ترین شمارهٔ موجود در همان ماه و همان کارگاه» گرفته می‌شود، نه از تعداد کل رکوردها؛
    /// در نتیجه حذف یک فرش باعث تکرار SKU نمی‌شود.
    /// (ایندکس یکتای TenantId+Sku آخرین خط دفاع در برابر ثبت هم‌زمان است — لایهٔ سرویس روی خطا دوباره تلاش می‌کند.)
    /// </summary>
    public async Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var prefix = $"RUG-{DateTime.UtcNow:yyyyMM}-";

        // IgnoreQueryFilters لازم است: ردیف حذف‌شدهٔ نرم هنوز جای SKU را در ایندکس یکتا
        // اشغال می‌کند، پس اگر نادیده گرفته شود ثبت فرش بعدی با تکرار SKU شکست می‌خورد.
        var existing = await Db.Rugs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Sku.StartsWith(prefix))
            .Select(r => r.Sku)
            .ToListAsync(cancellationToken);

        var maxSequence = existing
            .Select(sku => int.TryParse(sku[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return prefix + (maxSequence + 1).ToString("D4");
    }

    // ── سطل زباله ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.Rugs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.DeletedAt != null)
            .OrderByDescending(r => r.DeletedAt)
            .Select(r => new DeletedRugDto(
                r.Id,
                r.Sku,
                r.Title,
                r.Origin,
                r.WidthMeters,
                r.LengthMeters,
                r.Status,
                // همان فرمول هزینهٔ فهرست — تا کاربر بداند با حذف نهایی چه چیزی از دست می‌رود
                (r.PurchaseCost ?? 0) + r.WorkflowSteps
                    .Where(s => s.Status == WorkflowStepStatus.Completed || s.Status == WorkflowStepStatus.InProgress)
                    .Sum(s => (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0) < 0
                        ? 0
                        : (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0)),
                r.DeletedAt!.Value,
                Db.Users
                    .Where(u => u.Id == r.DeletedByUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

    public async Task<Rug?> GetDeletedAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.Rugs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.Id == id && r.TenantId == tenantId && r.DeletedAt != null, cancellationToken);
}

public class WorkflowTemplateRepository(AppDbContext db) : Repository<WorkflowTemplate>(db), IWorkflowTemplateRepository
{
    public async Task<WorkflowTemplate?> GetWithStepsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.WorkflowTemplates
            .Include(t => t.Steps.OrderBy(s => s.OrderIndex))
                .ThenInclude(s => s.ProcessStepType)
            .Include(t => t.Steps)
                .ThenInclude(s => s.DefaultServiceProvider)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<WorkflowTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.WorkflowTemplates
            .AsNoTracking()
            .Include(t => t.Steps.OrderBy(s => s.OrderIndex))
                .ThenInclude(s => s.ProcessStepType)
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
}

public class ProcessStepTypeRepository(AppDbContext db) : Repository<ProcessStepType>(db), IProcessStepTypeRepository
{
    public async Task<IReadOnlyList<ProcessStepType>> ListAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Db.ProcessStepTypes.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProcessStepType>> ListForTenantAsync(
        Guid tenantId, bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var query = Db.ProcessStepTypes
            .AsNoTracking()
            // سیستمی (بدون کارگاه) + اختصاصی همین کارگاه
            .Where(s => s.TenantId == null || s.TenantId == tenantId);

        if (onlyActive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.NameFa)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcessStepType?> GetForTenantAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ProcessStepTypes
            .FirstOrDefaultAsync(
                s => s.Id == id && (s.TenantId == null || s.TenantId == tenantId),
                cancellationToken);

    /// <summary>فقط برای مرحله‌های سیستمی — در seed استفاده می‌شود.</summary>
    public async Task<ProcessStepType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await Db.ProcessStepTypes.FirstOrDefaultAsync(
            s => s.Code == code && s.TenantId == null, cancellationToken);
}

public class RugBatchRepository(AppDbContext db) : Repository<RugBatch>(db), IRugBatchRepository
{
    public async Task<IReadOnlyList<RugBatch>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.RugBatches.AsNoTracking()
            .Include(b => b.Rugs)
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.ReceivedAt ?? b.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// AsSplitQuery چون زنجیرهٔ تودرتوی مجموعه‌هاست (محموله → فرش‌ها → مراحل):
    /// در تک‌کوئری تعداد ردیف‌ها حاصل‌ضرب «تعداد فرش × تعداد مراحل» می‌شود و
    /// یک محمولهٔ ۵۰تایی با مسیر ۸ مرحله‌ای ۴۰۰ ردیف پرتکرار برمی‌گرداند.
    /// </summary>
    public async Task<RugBatch?> GetWithRugsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.RugBatches
            .Include(b => b.Rugs)
                .ThenInclude(r => r.WorkflowSteps)
                    .ThenInclude(s => s.ProcessStepType)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);
}

public class ServiceProviderRepository(AppDbContext db) : Repository<ServiceProvider>(db), IServiceProviderRepository
{
    public async Task<IReadOnlyList<ServiceProvider>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceProvider>> ListAllWithRatesAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders.AsNoTracking()
            .Include(p => p.Rates).ThenInclude(r => r.ProcessStepType)
            .Where(p => p.TenantId == tenantId)
            // فعال‌ها اول، بعد بر اساس نام
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<ServiceProvider?> GetWithRatesAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders
            .Include(p => p.Rates).ThenInclude(r => r.ProcessStepType)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);

    /// <summary>
    /// مانده‌حساب همهٔ طرف‌ها در یک کوئری.
    /// فرمول هزینهٔ هر مرحله دقیقاً همان WorkflowEngine.CalculateRugCosts است.
    /// </summary>
    public async Task<IReadOnlyList<ProviderBalanceDto>> ListBalancesAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.Name)
            .Select(p => new ProviderBalanceDto(
                p.Id,
                p.Name,
                p.IsActive,
                p.Phone,
                Db.RugWorkflowSteps
                    .Where(s => s.ServiceProviderId == p.Id && s.Status == WorkflowStepStatus.Completed)
                    .Sum(s => (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0) < 0
                        ? 0
                        : (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0)),
                Db.RugWorkflowSteps
                    .Where(s => s.ServiceProviderId == p.Id && s.Status == WorkflowStepStatus.InProgress)
                    .Sum(s => (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0) < 0
                        ? 0
                        : (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0)),
                Db.ProviderPayments.Where(x => x.ServiceProviderId == p.Id).Sum(x => x.Amount),
                Db.RugWorkflowSteps.Count(s => s.ServiceProviderId == p.Id && s.Status == WorkflowStepStatus.Completed),
                Db.RugWorkflowSteps.Count(s => s.ServiceProviderId == p.Id && s.Status == WorkflowStepStatus.InProgress),
                Db.ProviderPayments.Where(x => x.ServiceProviderId == p.Id)
                    .OrderByDescending(x => x.PaidAt)
                    .Select(x => (DateTimeOffset?)x.PaidAt)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProviderWorkItemDto>> ListWorkAsync(
        Guid tenantId, Guid providerId, CancellationToken cancellationToken = default)
        => await Db.RugWorkflowSteps.AsNoTracking()
            .Where(s => s.ServiceProviderId == providerId
                        && s.Rug.TenantId == tenantId
                        && (s.Status == WorkflowStepStatus.Completed || s.Status == WorkflowStepStatus.InProgress))
            // تازه‌ترین کار اول؛ مراحل بدون تاریخ تکمیل (در جریان) بالای فهرست
            .OrderByDescending(s => s.CompletedAt ?? s.StartedAt)
            .Select(s => new ProviderWorkItemDto(
                s.Id,
                s.RugId,
                s.Rug.Sku,
                s.Rug.Title,
                s.ProcessStepType.NameFa,
                s.CompletedAt,
                s.Status,
                (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0) < 0
                    ? 0
                    : (s.ManualCostOverride ?? s.CalculatedCost ?? 0) + (s.Adjustment ?? 0)))
            .ToListAsync(cancellationToken);

    public async Task<bool> HasWorkHistoryAsync(Guid providerId, CancellationToken cancellationToken = default)
        => await Db.RugWorkflowSteps.AnyAsync(s => s.ServiceProviderId == providerId, cancellationToken)
           || await Db.ProviderPayments.AnyAsync(p => p.ServiceProviderId == providerId, cancellationToken);
}
