using System.Text.Json;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Features.Intake.Grid;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Intake;

/// <summary>جدول‌های فرشی: داده (<c>GET /api/grid/{gridId}</c>)، ذخیرهٔ دسته‌ای (G.6) و برگرداندن فرش حذف‌شده (E6).</summary>
public interface IRugGridService
{
    /// <summary>داده‌های یک جدول برای کاربر جاری؛ شناسهٔ ناشناخته → ۴۰۴.</summary>
    Task<GridDataDto> LoadAsync(Guid tenantId, string gridId, GridLoadRequest request, CancellationToken ct = default);

    /// <summary><c>POST /api/grid/rugs/batch</c>: نتیجهٔ هر ردیف (ذخیره، نادرست، تعارض، حذف).</summary>
    Task<GridBatchResultDto> SaveAsync(Guid tenantId, GridBatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// props صفحهٔ «ورود فرش» برای یک روز ایران (پیش‌فرض امروز): عنوان، فروشندهٔ پیش‌فرض (فروشندهٔ تازه‌ترین ورودیِ همان روز)،
    /// حالت شماره و سقف ضلع.
    /// </summary>
    Task<IntakePageDto> IntakePageAsync(Guid tenantId, DateOnly? day, CancellationToken ct = default);

    /// <summary>
    /// «حذف شد [برگردان]»: فرش از سطل زباله برمی‌گردد و ردیفش دوباره به جدول می‌رود. Business هر فرشی؛ بقیه همان قاعدهٔ
    /// حذف (فرش خودش در ۲۴ ساعت، بی مبلغ) — وگرنه ۴۰۳ (E6).
    /// </summary>
    /// <param name="gridId">جدولی که ردیف به آن برمی‌گردد (خانه‌های ردیف از ستون‌های همان جدول)؛ پیش‌فرض جدول ورود.</param>
    Task<GridRowDto> RestoreAsync(Guid tenantId, Guid rugId, string? gridId = null, CancellationToken ct = default);
}

/// <summary>
/// ذخیرهٔ ردیف‌های جدول ورود و انبار (spec G.6).
///
/// <para><b>یک تراکنش، یک پاس.</b> همهٔ ردیف‌ها زیر قفل حساب‌های کارگاه و در یک تراکنش نوشته و با یک
/// <c>SaveChanges</c> (پاس سازگاری) ذخیره می‌شوند. ردیفی که خانهٔ نادرست دارد پیش از ذخیره کنار می‌رود (نویسندهٔ فیلدها
/// فرش را نیمه‌نوشته نمی‌گذارد) و بقیه ذخیره می‌شوند. اگر خودِ ذخیره خطا داد (همپوشانی شماره، قفل محموله، کلید تکراری،
/// هم‌زمانی)، همه برمی‌گردد و هر ردیف جدا در تراکنش خودش دوباره اجرا می‌شود تا فقط ردیف خطادار جدا شود.</para>
///
/// <para><b>یکتایی.</b> ردیف تازه با <see cref="GridOp.ClientRowId"/> ساخته می‌شود؛ تلاش دوبارهٔ همان ردیف (قطع اینترنت،
/// پیش‌نویس) همان فرش را پیدا می‌کند و فرش دوم نمی‌سازد.</para>
///
/// <para><b>ادغام سه‌طرفه.</b> اگر نسخهٔ ردیف از وقتی کلاینت دیده عوض شده، هر خانه جدا سنجیده می‌شود: خانه‌ای که فقط کس
/// دیگری عوض کرده دست نمی‌خورد، خانه‌ای که فقط این کاربر عوض کرده نوشته می‌شود، و خانه‌ای که هر دو (به دو مقدار
/// متفاوت) عوض کرده‌اند کل ردیف را «تعارض» می‌کند و مقدار فعلی سرور برمی‌گردد («نوشتهٔ من بماند / نوشتهٔ او را ببینم»).</para>
///
/// <para><b>ورودی هر ردیف</b> از (روز سربرگ، فروشندهٔ همان ردیف) پیدا یا ساخته می‌شود؛ عوض کردن فروشنده فقط همان ردیف را
/// جابه‌جا می‌کند. <b>پول</b>: کلید پولی از غیر Business کل درخواست را ۴۰۳ می‌کند. <b>حذف</b>: قاعدهٔ
/// <see cref="RugDeletePolicy"/>؛ حذفِ رد‌شده فقط همان ردیف را «نادرست» می‌کند، نه کل درخواست. هر خانهٔ خرید نسخهٔ فرش را
/// جلو می‌برد (<c>UpdatedAt</c>) تا دو ویرایش هم‌زمان خرید دقیقاً یک ۴۰۹/تعارض بدهند.</para>
/// </summary>
public sealed class RugGridService(
    IGridLayoutService layouts,
    RugGridReader reader,
    IIntakeGridRepository repository,
    IRugRepository rugs,
    IRugFieldWriter writer,
    IRugBatchService batches,
    ISerialSuggester serials,
    IRugSkuGenerator skus,
    IUnitOfWork unitOfWork,
    ILedgerLock ledgerLock,
    IConcurrencyGuard concurrency,
    IMoneyVisibility money,
    ICurrentUser currentUser,
    IAuditLog audit,
    ITenantPreferencesRepository preferences) : IRugGridService
{
    /// <summary>بیشترین ردیف در یک ذخیره (چسباندن ۵۰ ردیف از اکسل جای زیادی دارد).</summary>
    public const int MaxOps = 500;

    /// <summary>کلید خطاهایی که به خانهٔ خاصی تعلق ندارند (حذف، ردیف).</summary>
    public const string RowField = "row";

    // وابستگی‌هایی که تلاش ذخیره (کلاس تو در تو) لازم دارد؛ پارامتر سازندهٔ اصلی از نمونهٔ دیگر دیده نمی‌شود
    private IIntakeGridRepository Repository => repository;
    private ISerialSuggester Serials => serials;
    private IRugSkuGenerator Skus => skus;
    private IRugRepository Rugs => rugs;
    private IAuditLog Audit => audit;
    private IRugFieldWriter Writer => writer;
    private IRugBatchService Batches => batches;
    private IConcurrencyGuard Concurrency => concurrency;

    // ═════════════════════════════════════════════════════════════════════
    // خواندن
    // ═════════════════════════════════════════════════════════════════════

    public async Task<GridDataDto> LoadAsync(
        Guid tenantId, string gridId, GridLoadRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = layouts.Source(gridId);
        var viewer = Viewer(tenantId, await PreferencesAsync(tenantId, ct));
        var layout = await layouts.EffectiveAsync(viewer, source, ct);
        return await source.LoadAsync(viewer, request, layout.Columns, layout.AllColumns, ct);
    }

    public async Task<IntakePageDto> IntakePageAsync(Guid tenantId, DateOnly? day, CancellationToken ct = default)
    {
        var today = IranDay.Today();
        var date = day ?? today;
        var prefs = await PreferencesAsync(tenantId, ct);
        var ofDay = await repository.ListBatchesAsync(tenantId, date, date, ct);
        var seller = ofDay.Where(b => b.Rows > 0).Select(b => b.Batch.SellerName).FirstOrDefault();

        var title = date == today ? "ورودی امروز"
            : date == today.AddDays(-1) ? "ورودی دیروز"
            : "ورودی " + PersianText.ToPersianDigits(IranDay.JalaliLatin(date));

        return new IntakePageDto(
            date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            title, seller, (int)prefs.SerialMode, prefs.MaxPlausibleSideM);
    }

    // ═════════════════════════════════════════════════════════════════════
    // ذخیرهٔ دسته‌ای (G.6)
    // ═════════════════════════════════════════════════════════════════════

    public async Task<GridBatchResultDto> SaveAsync(Guid tenantId, GridBatchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = layouts.Source(request.GridId);
        if (!source.EditsRugs)
            throw new AppValidationException(Messages.GridUnknown.Text);

        var ops = request.Ops ?? [];
        if (ops.Count > MaxOps)
            throw new AppValidationException(Messages.GridTooManyOps.Format(("n", MaxOps)));
        if (ops.Any(o => o is null || o.ClientRowId == Guid.Empty)
            || ops.Select(o => o.ClientRowId).Distinct().Count() != ops.Count)
            throw new AppValidationException(Messages.GridClientRowInvalid.Text);

        // کلید پولی از کسی که Business نیست کل درخواست را ۴۰۳ می‌کند (G.6 #5)، حتی در «مقدار پیشین»؛ جدول انباردار ستون
        // پولی ندارد، پس این فقط درخواستِ دست‌کاری‌شده را می‌گیرد
        foreach (var op in ops)
        {
            writer.EnsureMayWrite(op.Values);
            writer.EnsureMayWrite(op.BaseValues);
        }

        var prefs = await PreferencesAsync(tenantId, ct);
        var viewer = Viewer(tenantId, prefs);
        var layout = await layouts.EffectiveAsync(viewer, source, ct);
        var builder = await reader.BuilderAsync(viewer, layout.AllColumns, ct);
        var day = request.Batch?.ReceivedAt ?? IranDay.Today();

        // ورودیِ صریح باید مال همین کارگاه باشد؛ ورودی کارگاه دیگر مثل ورودی ناموجود ۴۰۴ است و هیچ چیزی نوشته نمی‌شود
        if (request.Batch?.Id is { } explicitBatch
            && await repository.GetBatchAsync(tenantId, explicitBatch, ct) is null)
            throw new KeyNotFoundException(Messages.IntakeBatchNotFound.Text);

        var plan = new SavePlan(tenantId, prefs, viewer, builder, day, request.Batch, DateTimeOffset.UtcNow);
        var outcome = new SaveOutcome(ops.Count);
        var items = ops.Select((op, i) => (Index: i, Op: op)).ToList();

        try
        {
            var run = await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await ledgerLock.AcquireAsync(tenantId, token);
                var attempt = new SaveRun(this, plan);
                await attempt.ApplyAsync(items, token);
                await unitOfWork.SaveChangesAsync(token);
                return attempt;
            }, ct);
            run.CopyTo(outcome);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or UnauthorizedAccessException))
        {
            // ذخیرهٔ یک‌جا شکست خورد (و برگشت): هر ردیف در تراکنش خودش، تا فقط ردیف خطادار جدا شود (G.6 #3)
            outcome = new SaveOutcome(ops.Count);
            foreach (var item in items)
                await SaveOneAsync(plan, item, outcome, ct);
        }

        var results = await ResultsAsync(plan, ops, outcome, ct);
        var totals = source.GridId == TenantGridLayout.IntakeGridId
            ? builder.Totals(await repository.ListIntakeDayAsync(tenantId, day, request.Batch?.Id, ct), groupByBatch: true)
            : null;

        return new GridBatchResultDto(results, totals, await serials.SuggestAsync(tenantId, ct));
    }

    /// <summary>
    /// یک ردیف در تراکنش خودش (مسیر جایگزین). خطای «مال همین ردیف» نتیجهٔ همان ردیف می‌شود؛ ساختی که با کلید تکراری
    /// ClientRowId رد شد (همان ردیف هم‌زمان از جای دیگری رسید) یک بار دیگر اجرا می‌شود و حالا فرش ساخته‌شده را پیدا می‌کند.
    /// خطای ناشناخته بالا می‌رود (۵۰۰؛ ردیف‌ها روی دستگاه می‌مانند و دوباره فرستاده می‌شوند).
    /// </summary>
    private async Task SaveOneAsync(SavePlan plan, (int Index, GridOp Op) item, SaveOutcome outcome, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var run = await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await ledgerLock.AcquireAsync(plan.TenantId, token);
                    var single = new SaveRun(this, plan);
                    await single.ApplyAsync([item], token);
                    await unitOfWork.SaveChangesAsync(token);
                    return single;
                }, ct);
                run.CopyTo(outcome);
                return;
            }
            catch (DuplicateKeyException) when (attempt == 0 && item.Op.RugId is null && !item.Op.Delete)
            {
                // دوباره، این بار با فرشی که همان ClientRowId را دارد
            }
            catch (Exception ex) when (IsRowFailure(ex))
            {
                outcome.Failures[item.Index] = await FailureAsync(plan, item.Op, ex, ct);
                return;
            }
        }
    }

    private static bool IsRowFailure(Exception ex) => ex is DomainConflictException or ConcurrencyConflictException
        or DuplicateKeyException or AppValidationException or DomainValidationException or KeyNotFoundException;

    /// <summary>نتیجهٔ ردیفی که ذخیره‌اش (در تراکنش خودش) خطا داد.</summary>
    private async Task<GridOpResult> FailureAsync(SavePlan plan, GridOp op, Exception ex, CancellationToken ct)
    {
        switch (ex)
        {
            case SerialOverlapException overlap:
                return Invalid(op, [new FieldErrorDto(RugFieldKeys.Serial, overlap.Message)],
                    overlap.NextFree ?? await serials.NextFreeAsync(plan.TenantId, ct));

            case ConcurrencyConflictException:
            {
                var rugId = op.RugId;
                if (rugId is null)
                    return Invalid(op, [new FieldErrorDto(RowField, Messages.GridConflict.Text)]);
                var current = (await repository.ListByIdsAsync(plan.TenantId, [rugId.Value], ct)).SingleOrDefault();
                return current is null
                    ? Gone(op)
                    : new GridOpResult(op.ClientRowId, current.Id, current.Version, current.Sku, GridRowStatus.Conflict,
                        plan.Builder.Derived(current, canDelete: false), [new FieldErrorDto(RowField, Messages.GridConflict.Text)],
                        plan.Builder.Values(current));
            }

            case KeyNotFoundException:
                return Gone(op);

            default:
                return Invalid(op, Errors(ex));
        }
    }

    /// <summary>نتیجهٔ نهایی هر ردیف: ذخیره‌شده‌ها و تعارض‌ها با مقدار تازهٔ سرور (یک پرس‌وجو برای همه).</summary>
    private async Task<IReadOnlyList<GridOpResult>> ResultsAsync(
        SavePlan plan, IReadOnlyList<GridOp> ops, SaveOutcome outcome, CancellationToken ct)
    {
        var reread = outcome.Saved.Values.Concat(outcome.Conflicts.Values.Select(c => c.RugId)).Distinct().ToList();
        var snapshots = (await repository.ListByIdsAsync(plan.TenantId, reread, ct)).ToDictionary(s => s.Id);
        var canDelete = await reader.CanDeleteAsync(plan.Viewer, reread, ct);

        var results = new GridOpResult[ops.Count];
        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            if (outcome.Failures.TryGetValue(i, out var failure))
            {
                results[i] = failure;
            }
            else if (outcome.Deleted.TryGetValue(i, out var deleted))
            {
                results[i] = new GridOpResult(op.ClientRowId, deleted.RugId, null, deleted.Sku, GridRowStatus.Deleted, null, null, null);
            }
            else if (outcome.Conflicts.TryGetValue(i, out var conflict) && snapshots.TryGetValue(conflict.RugId, out var server))
            {
                results[i] = new GridOpResult(op.ClientRowId, server.Id, server.Version, server.Sku, GridRowStatus.Conflict,
                    plan.Builder.Derived(server, canDelete.Contains(server.Id)),
                    conflict.Keys.Select(k => new FieldErrorDto(k, Messages.GridConflict.Text)).ToList(),
                    plan.Builder.Values(server));
            }
            else if (outcome.Saved.TryGetValue(i, out var rugId) && snapshots.TryGetValue(rugId, out var saved))
            {
                results[i] = new GridOpResult(op.ClientRowId, saved.Id, saved.Version, saved.Sku, GridRowStatus.Saved,
                    plan.Builder.Derived(saved, canDelete.Contains(saved.Id)), null, plan.Builder.Values(saved));
            }
            else
            {
                results[i] = Gone(op);
            }
        }

        return results;
    }

    // ═════════════════════════════════════════════════════════════════════
    // برگرداندن (E6)
    // ═════════════════════════════════════════════════════════════════════

    public async Task<GridRowDto> RestoreAsync(
        Guid tenantId, Guid rugId, string? gridId = null, CancellationToken ct = default)
    {
        var source = layouts.Source(string.IsNullOrWhiteSpace(gridId) ? TenantGridLayout.IntakeGridId : gridId);
        var rug = await rugs.GetDeletedAsync(rugId, tenantId, ct);
        if (rug is null)
        {
            // فرش فعالِ همین کارگاه: «حذف نشده»؛ نبود یا کارگاه دیگر: ۴۰۴ (وجودش لو نمی‌رود)
            if ((await repository.ListByIdsAsync(tenantId, [rugId], ct)).Count > 0)
                throw new AppValidationException(Messages.GridRestoreNotDeleted.Text);
            throw new KeyNotFoundException(Messages.GridRowGone.Text);
        }

        var prefs = await PreferencesAsync(tenantId, ct);
        var viewer = Viewer(tenantId, prefs);
        var facts = await repository.DeleteFactsAsync(tenantId, viewer.UserId, [rugId], ct);
        var now = DateTimeOffset.UtcNow;
        if (!viewer.CanSeeMoney
            && !(facts.TryGetValue(rugId, out var fact) && RugDeletePolicy.MayDelete(fact, business: false, now)))
            throw new UnauthorizedAccessException(Messages.GridRestoreForbidden.Text);

        rug.DeletedAt = null;
        rug.DeletedByUserId = null;
        rug.UpdatedAt = now;
        audit.Record(AuditAction.Restored, nameof(Rug), rug.Id,
            $"فرش {Label(rug)} از سطل زباله بازگردانده شد (جدول).", rug.Sku);

        // پاس سازگاری شماره را دوباره می‌سنجد: اگر حالا مال فرش دیگری است ۴۰۹ و فرش در سطل می‌ماند
        await unitOfWork.SaveChangesAsync(ct);

        var layout = await layouts.EffectiveAsync(viewer, source, ct);
        var builder = await reader.BuilderAsync(viewer, layout.AllColumns, ct);
        var snapshot = (await repository.ListByIdsAsync(tenantId, [rugId], ct)).Single();
        return (await reader.RowsAsync(builder, [snapshot], ct)).Single();
    }

    // ═════════════════════════════════════════════════════════════════════
    // ابزار
    // ═════════════════════════════════════════════════════════════════════

    private async Task<TenantPreferences> PreferencesAsync(Guid tenantId, CancellationToken ct)
        => (await preferences.GetAsync(tenantId, ct))?.Preferences ?? TenantPreferences.CreateDefault(tenantId);

    private GridViewer Viewer(Guid tenantId, TenantPreferences prefs)
        => new(tenantId, currentUser.UserId, money.CanSeeMoney, BaleTerms.For(prefs.BaleTerm));

    private static string Label(Rug rug) => PersianText.ToPersianDigits(rug.SerialNo ?? rug.Sku);

    private static GridOpResult Invalid(GridOp op, IReadOnlyList<FieldErrorDto> errors, int? nextFree = null)
        => new(op.ClientRowId, op.RugId, null, null, GridRowStatus.Invalid, null, errors, null, NextFree: nextFree);

    private static GridOpResult Gone(GridOp op)
        => new(op.ClientRowId, op.RugId, null, null, GridRowStatus.Deleted, null,
            [new FieldErrorDto(RowField, Messages.GridRowGone.Text)], null);

    /// <summary>خطاهای یک استثنا به خطای هر خانه؛ کلید دامنه (<c>rug.quantity</c>) به کلید جدول (<c>rug.qty</c>).</summary>
    private static IReadOnlyList<FieldErrorDto> Errors(Exception ex)
    {
        IReadOnlyDictionary<string, string[]>? fields = ex switch
        {
            AppValidationException app => app.Errors,
            DomainValidationException domain => domain.Errors,
            _ => null
        };

        if (fields is { Count: > 0 })
        {
            return fields
                .Where(kv => kv.Value.Length > 0)
                .Select(kv => new FieldErrorDto(RugFieldKeys.FromDomainField(kv.Key), kv.Value[0]))
                .ToList();
        }

        var field = ex is DomainConflictException conflict && !string.IsNullOrEmpty(conflict.Field)
            ? RugFieldKeys.FromDomainField(conflict.Field)
            : RowField;
        return [new FieldErrorDto(field, ex.Message)];
    }

    // ═════════════════════════════════════════════════════════════════════
    // اجرای یک تلاش
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>آنچه برای همهٔ تلاش‌های یک درخواست ثابت است.</summary>
    private sealed record SavePlan(
        Guid TenantId,
        TenantPreferences Preferences,
        GridViewer Viewer,
        RugGridRowBuilder Builder,
        DateOnly Day,
        GridBatchContext? Batch,
        DateTimeOffset Now);

    /// <summary>نتیجهٔ ردیف‌ها، جمع همهٔ تلاش‌ها.</summary>
    private sealed class SaveOutcome(int count)
    {
        public int Count { get; } = count;
        public Dictionary<int, Guid> Saved { get; } = [];
        public Dictionary<int, (Guid RugId, IReadOnlyList<string> Keys)> Conflicts { get; } = [];
        public Dictionary<int, (Guid? RugId, string? Sku)> Deleted { get; } = [];
        public Dictionary<int, GridOpResult> Failures { get; } = [];
    }

    /// <summary>
    /// یک تلاش (همهٔ ردیف‌ها، یا یک ردیف در مسیر جایگزین): ردیف‌ها روی DbContext نوشته می‌شوند و فراخواننده ذخیره می‌کند.
    /// چون تلاش شکست‌خورده برمی‌گردد و ردیابی پاک می‌شود، هر تلاش همه‌چیز را از نو می‌خواند.
    /// </summary>
    private sealed class SaveRun(RugGridService service, SavePlan plan)
    {
        private readonly Dictionary<int, Guid> _saved = [];
        private readonly Dictionary<int, (Guid, IReadOnlyList<string>)> _conflicts = [];
        private readonly Dictionary<int, (Guid?, string?)> _deleted = [];
        private readonly Dictionary<int, GridOpResult> _failures = [];
        private readonly List<Rug> _created = [];

        /// <summary>شمارهٔ خودکار بعدی (حالت «خودکار»)؛ زیر قفل کارگاه از دیتابیس و بعد ردیف‌به‌ردیف جلو می‌رود.</summary>
        private int? _nextAuto;

        public void CopyTo(SaveOutcome outcome)
        {
            foreach (var (k, v) in _saved) outcome.Saved[k] = v;
            foreach (var (k, v) in _conflicts) outcome.Conflicts[k] = v;
            foreach (var (k, v) in _deleted) outcome.Deleted[k] = v;
            foreach (var (k, v) in _failures) outcome.Failures[k] = v;
        }

        public async Task ApplyAsync(IReadOnlyList<(int Index, GridOp Op)> items, CancellationToken ct)
        {
            var tenantId = plan.TenantId;

            // ردیف‌های تازه‌ای که قبلاً ساخته شده‌اند (تلاش دوباره) و فرش‌های هدف — هر کدام یک پرس‌وجو برای همه
            var clientRows = await service.Repository.FindByClientRowIdsAsync(
                tenantId, items.Where(x => x.Op.RugId is null).Select(x => x.Op.ClientRowId).ToList(), ct);

            Guid? TargetOf(GridOp op)
                => op.RugId ?? (clientRows.TryGetValue(op.ClientRowId, out var found) && !found.Deleted ? found.RugId : null);

            var targetIds = items.Select(x => TargetOf(x.Op)).OfType<Guid>().Distinct().ToList();
            var loaded = (await service.Repository.LoadForWriteAsync(tenantId, targetIds, ct)).ToDictionary(r => r.Id);

            var deleteIds = items.Where(x => x.Op.Delete).Select(x => TargetOf(x.Op)).OfType<Guid>().ToList();
            var facts = deleteIds.Count > 0 && !plan.Viewer.CanSeeMoney
                ? await service.Repository.DeleteFactsAsync(tenantId, plan.Viewer.UserId, deleteIds, ct)
                : new Dictionary<Guid, RugDeleteFacts>();

            if (plan.Preferences.SerialMode == SerialMode.Auto && items.Any(x => !x.Op.Delete && TargetOf(x.Op) is null))
                _nextAuto = await service.Serials.NextFreeAsync(tenantId, ct);

            foreach (var (index, op) in items)
            {
                var target = TargetOf(op);
                if (op.Delete)
                {
                    Delete(index, op, target is { } id && loaded.TryGetValue(id, out var doomed) ? doomed : null, facts);
                }
                else if (target is null)
                {
                    // ردیفی که ساخته و بعد حذف شده، یا فرشی که دیگر نیست: «حذف شد»
                    if (op.RugId is not null || clientRows.ContainsKey(op.ClientRowId))
                        _failures[index] = Gone(op);
                    else
                        await CreateAsync(index, op, ct);
                }
                else if (loaded.TryGetValue(target.Value, out var rug))
                {
                    await UpdateAsync(index, op, rug, ct);
                }
                else
                {
                    _failures[index] = Gone(op); // کارگاه دیگر یا سطل زباله؛ وجودش لو نمی‌رود
                }
            }

            // کد انبار بعد از همهٔ سنجش‌ها و یک‌جا، تا ردیفِ ردشده عددی از شمارنده نسوزاند
            if (_created.Count > 0)
            {
                var codes = await service.Skus.NextAsync(tenantId, _created.Count, ct);
                for (var i = 0; i < _created.Count; i++)
                {
                    var rug = _created[i];
                    rug.Sku = codes[i];
                    await service.Rugs.AddAsync(rug, ct);
                    service.Audit.Record(AuditAction.Created, nameof(Rug), rug.Id,
                        $"فرش {Label(rug)} در جدول ورود ثبت شد.", rug.Sku);
                }
            }
        }

        // ── ساختن ───────────────────────────────────────────────────────────

        private async Task CreateAsync(int index, GridOp op, CancellationToken ct)
        {
            var values = Clean(op.Values, null);
            var sellerGiven = values.Remove(GridKeys.Seller, out var sellerValue);
            var rug = new Rug { TenantId = plan.TenantId, Status = RugStatus.Draft, ClientRowId = op.ClientRowId };

            try
            {
                var written = await service.Writer.ApplyAsync(rug, values, Context(isNew: true), ct);

                RugBatch batch;
                if (sellerGiven)
                    batch = await service.Batches.ResolveForDayAsync(plan.TenantId, plan.Day, SellerText(sellerValue), ct);
                else if (plan.Batch?.Id is { } batchId)
                    batch = await service.Repository.GetBatchAsync(plan.TenantId, batchId, ct)
                            ?? throw new KeyNotFoundException(Messages.IntakeBatchNotFound.Text);
                else
                    batch = await service.Batches.ResolveForDayAsync(plan.TenantId, plan.Day, plan.Batch?.SellerName, ct);

                rug.BatchId = batch.Id;
                rug.Batch = batch;

                if (written.NeedsAutoSerial)
                {
                    // حالت «خودکار» (G.4، E2): شماره زیر قفل کارگاه و پیش از flush روی همین فرش؛ دسته بازهٔ کامل می‌گیرد
                    var next = _nextAuto ??= await service.Serials.NextFreeAsync(plan.TenantId, ct);
                    var serial = SerialSuggester.ForNewRow(next, rug.Quantity);
                    rug.SetSerialAndQuantity(serial, rug.Quantity);
                }

                BumpAuto(rug);
                _created.Add(rug);
                _saved[index] = rug.Id;
            }
            catch (Exception ex) when (ex is AppValidationException or DomainValidationException or DomainConflictException
                                           or KeyNotFoundException)
            {
                _failures[index] = Invalid(op, Errors(ex));
            }
        }

        // ── ویرایش ──────────────────────────────────────────────────────────

        private async Task UpdateAsync(int index, GridOp op, Rug rug, CancellationToken ct)
        {
            var version = service.Concurrency.VersionOf(rug);
            var server = plan.Builder.Values(GridRugSnapshot.FromTracked(rug, version, rug.PurchaseLines));
            var incoming = Clean(op.Values, rug);

            var (toWrite, conflicts) = Merge(op, version, incoming, server);
            if (conflicts.Count > 0)
            {
                _conflicts[index] = (rug.Id, conflicts);
                return;
            }

            var sellerGiven = toWrite.Remove(GridKeys.Seller, out var sellerValue);
            try
            {
                var written = await service.Writer.ApplyAsync(rug, toWrite, Context(isNew: false), ct);

                var moved = false;
                if (sellerGiven)
                {
                    // روز ورودیِ تازه: روز سربرگ جدول ورود، وگرنه روز ورودی فعلی همین فرش
                    var day = plan.Batch?.ReceivedAt ?? rug.Batch?.ReceivedOn ?? IranDay.Of(rug.CreatedAt);
                    var batch = await service.Batches.ResolveForDayAsync(plan.TenantId, day, SellerText(sellerValue), ct);
                    if (rug.BatchId != batch.Id)
                    {
                        rug.BatchId = batch.Id;
                        rug.Batch = batch;
                        moved = true;
                    }
                }

                if (written.ChangedKeys.Count > 0 || moved)
                {
                    // خودِ ردیف فرش همیشه نوشته می‌شود تا بررسی نسخه (WHERE xmin) انجام شود و نسخه جلو برود — حتی وقتی فقط
                    // سطر خرید عوض شده (G.2 «Concurrency»)
                    rug.UpdatedAt = plan.Now;
                    service.Concurrency.Expect(rug, version);
                }

                BumpAuto(rug);
                _saved[index] = rug.Id;
            }
            catch (Exception ex) when (ex is AppValidationException or DomainValidationException or DomainConflictException)
            {
                _failures[index] = new GridOpResult(op.ClientRowId, rug.Id, version, rug.Sku, GridRowStatus.Invalid, null,
                    Errors(ex), null);
            }
        }

        /// <summary>
        /// ادغام سه‌طرفه: نسخهٔ همان → هر چه آمده؛ نسخهٔ کهنه → هر خانه با «مقدار پیشین» (<see cref="GridOp.BaseValues"/>)
        /// سنجیده می‌شود. خانه‌ای که مقدار پیشین ندارد و با سرور فرق دارد هم تعارض است (نمی‌دانیم چه کسی عوضش کرد).
        /// </summary>
        private static (Dictionary<string, System.Text.Json.JsonElement?> ToWrite, IReadOnlyList<string> Conflicts) Merge(
            GridOp op, uint version, Dictionary<string, System.Text.Json.JsonElement?> incoming,
            IReadOnlyDictionary<string, System.Text.Json.JsonElement?> server)
        {
            if (op.Version is null || op.Version == version)
                return (incoming, []);

            var toWrite = new Dictionary<string, System.Text.Json.JsonElement?>(StringComparer.Ordinal);
            var conflicts = new List<string>();
            foreach (var (key, mine) in incoming)
            {
                // کلیدی که جزو خانه‌های این جدول نیست مقدار سرورش این‌جا نیست؛ همان نوشتن کاربر
                if (!server.TryGetValue(key, out var current))
                {
                    toWrite[key] = mine;
                    continue;
                }

                if (GridValueComparer.Same(key, mine, current))
                    continue; // همان است (شاید هر دو یک چیز نوشتند)

                System.Text.Json.JsonElement? baseValue = null;
                var hasBase = op.BaseValues is not null && op.BaseValues.TryGetValue(key, out baseValue);
                if (hasBase && GridValueComparer.Same(key, current, baseValue))
                    toWrite[key] = mine; // فقط این کاربر عوضش کرد
                else if (hasBase && GridValueComparer.Same(key, mine, baseValue))
                    continue; // فقط دیگری عوضش کرد؛ مقدار سرور می‌ماند
                else
                    conflicts.Add(key);
            }

            return (toWrite, conflicts);
        }

        // ── حذف ─────────────────────────────────────────────────────────────

        private void Delete(int index, GridOp op, Rug? rug, IReadOnlyDictionary<Guid, RugDeleteFacts> facts)
        {
            if (rug is null)
            {
                // هرگز ذخیره نشده، یا پیش از این حذف شده: همان «حذف شد»
                _deleted[index] = (op.RugId, null);
                return;
            }

            if (rug.Sale is not null)
            {
                _failures[index] = Invalid(op, [new FieldErrorDto(RowField, Messages.GridDeleteSold.Text)]);
                return;
            }

            if (!plan.Viewer.CanSeeMoney
                && !(facts.TryGetValue(rug.Id, out var fact) && RugDeletePolicy.MayDelete(fact, business: false, plan.Now)))
            {
                // «۴۰۳» فقط برای همین ردیف (G.6 #6)، نه کل درخواست
                _failures[index] = Invalid(op, [new FieldErrorDto(RowField, Messages.RugDeleteForbidden.Text)]);
                return;
            }

            rug.DeletedAt = plan.Now;
            rug.DeletedByUserId = plan.Viewer.UserId;
            rug.UpdatedAt = plan.Now;
            service.Audit.Record(AuditAction.Deleted, nameof(Rug), rug.Id,
                $"فرش {Label(rug)} از جدول به سطل زباله رفت.", rug.Sku);
            _deleted[index] = (rug.Id, rug.Sku);
        }

        // ── ابزار ───────────────────────────────────────────────────────────

        private RugWriteContext Context(bool isNew) => new(
            plan.TenantId, isNew, plan.Preferences.SerialMode, plan.Preferences.MoneyScale, plan.Now, plan.Viewer.UserId);

        /// <summary>شمارهٔ خودکار بعدی از آخر هر شماره‌ای که در همین درخواست نوشته شد جلوتر می‌رود.</summary>
        private void BumpAuto(Rug rug)
        {
            if (_nextAuto is { } next && rug.Serial?.NextAfter is { } after && after > next)
                _nextAuto = after;
        }

        /// <summary>
        /// ورودی پاک‌شده برای نویسنده: کلیدهای حساب‌شده کنار می‌روند (جزیره ممکن است ردیف کامل را پس بفرستد)، و خانهٔ خرید
        /// به شکلی که نویسنده می‌فهمد: پژواک خانهٔ «چند سطر» نادیده؛ <c>{rate: null, basis}</c> روی سطری که هست یعنی «فقط
        /// واحد عوض شد» (نرخ همان می‌ماند) و روی خانهٔ خالی یعنی «هنوز چیزی ننوشته» (نادیده).
        /// </summary>
        private Dictionary<string, System.Text.Json.JsonElement?> Clean(
            IReadOnlyDictionary<string, System.Text.Json.JsonElement?>? values, Rug? rug)
        {
            var cleaned = new Dictionary<string, System.Text.Json.JsonElement?>(StringComparer.Ordinal);
            if (values is null) return cleaned;

            foreach (var (rawKey, value) in values)
            {
                var key = rawKey?.Trim() ?? string.Empty;
                if (key.Length == 0 || RugGridRowBuilder.DerivedKeys.Contains(key)
                    || key.StartsWith("row.", StringComparison.Ordinal))
                    continue;

                if (key.StartsWith(RugFieldKeys.PurchasePrefix, StringComparison.Ordinal)
                    && value is { ValueKind: System.Text.Json.JsonValueKind.Object } cell)
                {
                    var (rate, basis, multi) = GridValueComparer.PurchaseParts(cell);
                    if (multi) continue;
                    if (rate is null)
                    {
                        if (basis is null) continue;
                        var category = plan.Builder.Catalog.PurchaseCategoryOf(key);
                        var lines = category is null || rug is null
                            ? []
                            : rug.PurchaseLines.Where(l => l.CategoryId == category.Id).ToList();
                        if (lines.Count == 0) continue;
                        if (lines.Count == 1)
                        {
                            cleaned[key] = System.Text.Json.JsonSerializer.SerializeToElement(
                                new Dictionary<string, object> { ["rate"] = lines[0].Rate, ["basis"] = (int)basis.Value });
                            continue;
                        }
                    }
                }

                cleaned[key] = value;
            }

            return cleaned;
        }

        private static string? SellerText(System.Text.Json.JsonElement? value)
            => RugFieldWriter.TryReadText(value, out var text) ? text : null;
    }
}
