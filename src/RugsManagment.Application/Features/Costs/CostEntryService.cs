using System.Globalization;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Costs.Ledger;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// دفتر «هزینه‌ها» (spec D.2، D.3، H.1، H.2، I.2): ثبت، ویرایش، حذف نرم و «برگردان»، پیش‌نمایش و فهرست. فقط Business؛
/// کنترلر سیاست را می‌سنجد و این سرویس مبلغ را پنهان نمی‌کند.
/// </summary>
public interface ICostEntryService
{
    /// <summary>فهرست صفحه‌بندی‌شده با فیلترهای I.2 (تراشهٔ «تقسیم‌نشده»: <see cref="CostEntryQuery.NotFullyAllocated"/>).</summary>
    Task<PagedResult<CostEntryListItemDto>> ListAsync(Guid tenantId, CostEntryQuery query, CancellationToken cancellationToken = default);

    /// <summary>یک ثبت (حذف‌شده هم)؛ کارگاه دیگر → <see cref="KeyNotFoundException"/>.</summary>
    Task<CostEntryDto> GetAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ثبت تازه؛ با همان <see cref="SaveCostEntryRequest.ClientKey"/> همان ثبت قبلی برمی‌گردد (تلاش دوبارهٔ پنل بعد از قطع اینترنت
    /// ثبت دوم نمی‌سازد). سهم‌ها را پاس سازگاری در همین ذخیره می‌نویسد، پس پاسخ نتیجهٔ تقسیم را دارد.
    /// </summary>
    Task<CostEntryDto> CreateAsync(Guid tenantId, SaveCostEntryRequest request, CancellationToken cancellationToken = default);

    /// <summary>ویرایش با نسخه (<see cref="SaveCostEntryRequest.Version"/> الزامی؛ کهنه → ۴۰۹).</summary>
    Task<CostEntryDto> UpdateAsync(Guid tenantId, Guid entryId, SaveCostEntryRequest request, CancellationToken cancellationToken = default);

    /// <summary>حذف نرم («هزینهٔ شمارهٔ ۱۲ حذف شد [برگردان]»)؛ نسخه اختیاری.</summary>
    Task<CostEntryDto> DeleteAsync(Guid tenantId, Guid entryId, uint? version, CancellationToken cancellationToken = default);

    /// <summary>«برگردان».</summary>
    Task<CostEntryDto> RestoreAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>پیش‌نمایش تقسیم بی ذخیره (<c>POST /api/costs/preview</c>)؛ مبلغِ خالی صفر حساب می‌شود و فهرست خالی خطا نیست.</summary>
    Task<AllocationPreviewDto> PreviewAsync(Guid tenantId, SaveCostEntryRequest request, CancellationToken cancellationToken = default);

    /// <summary>«به‌روزرسانی مبلغ» رسید منجمد (D.3): مبلغ با اندازه‌های امروز دوباره منجمد می‌شود.</summary>
    Task<CostEntryDto> RefreezeAsync(Guid tenantId, Guid entryId, uint version, CancellationToken cancellationToken = default);

    /// <summary>«فرش‌های این ورودی را دوباره بگیر» (D.2): عکس فرش‌های خرید یکجای ورودی از نو گرفته می‌شود.</summary>
    Task<CostEntryDto> ResnapshotAsync(Guid tenantId, Guid entryId, uint version, CancellationToken cancellationToken = default);

    /// <summary>سهم یک فرش از ثبت‌های هزینه (ریز هزینهٔ فرش).</summary>
    Task<IReadOnlyList<AllocationLineDto>> AllocationsOfRugAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);
}

/// <summary>
/// پیاده‌سازی دفتر هزینه. خودِ سهم‌ها این‌جا نوشته نمی‌شوند: سرویس فقط ثبت و هدف‌ها و مبلغ‌های ثابت را می‌نویسد و
/// <c>PurchaseEntryStage</c>/<c>ExpenseEntryStage</c> در همان ذخیره (پاس سازگاری) تقسیم می‌کنند؛ پیش‌نمایش همان
/// <see cref="CostAllocator"/> را روی اعضای فعلی صدا می‌زند، پس عدد پیش‌نمایش همان عدد ذخیره است.
///
/// <para>مبلغ پایه: «کل» = ROUND(مبلغ × نرخ، s) یک بار؛ «هر متر»/«هر تخته» با رسید یا طرف حساب منجمد می‌شود (کلِ همین
/// لحظه)، بی آن زنده است (D.3). مبلغ‌های ثابت به ارز کارگاه‌اند و با همان مبلغ پایه سنجیده می‌شوند.</para>
/// </summary>
public sealed class CostEntryService(
    ICostEntryRepository entries,
    ITenantCounters counters,
    IConcurrencyGuard concurrency,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    TimeProvider? time = null,
    ILedgerLock? ledgerLock = null) : ICostEntryService
{
    /// <summary>بزرگ‌ترین مبلغ numeric(18,2).</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>بزرگ‌ترین نرخ numeric(18,4).</summary>
    public const decimal MaxRate = 99_999_999_999_999.9999m;

    /// <summary>بزرگ‌ترین نرخ ارز numeric(18,6).</summary>
    public const decimal MaxFx = 999_999_999_999.999999m;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    // ═════════════════════════════════════════════════════════════════════
    // خواندن
    // ═════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<CostEntryListItemDto>> ListAsync(
        Guid tenantId, CostEntryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
        var page = await entries.ListAsync(tenantId, query.Sanitized(), cancellationToken);
        return new PagedResult<CostEntryListItemDto>(
            page.Items.Select(r => ToListItem(r, settings)).ToList(), page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<CostEntryDto> GetAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var detail = await entries.GetDetailAsync(tenantId, entryId, cancellationToken)
                     ?? throw new KeyNotFoundException("هزینه یافت نشد.");
        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
        return ToDto(detail, settings);
    }

    public Task<IReadOnlyList<AllocationLineDto>> AllocationsOfRugAsync(
        Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
        => entries.AllocationsOfRugAsync(tenantId, rugId, cancellationToken);

    public async Task<AllocationPreviewDto> PreviewAsync(
        Guid tenantId, SaveCostEntryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
        var plan = await PlanAsync(tenantId, request, settings, preview: true, existing: null, cancellationToken);
        var now = _time.GetUtcNow();
        var members = await entries.MembersAsync(tenantId, plan.ScopeSpec, plan.Category.StepTypeId, now, cancellationToken);

        decimal baseAmount;
        decimal? amount = plan.Amount;
        if (plan.Basis == CostBasis.Total)
        {
            baseAmount = CostAllocator.BaseOfTotal(plan.Amount ?? 0m, plan.Currency, settings.Currency, plan.Fx, settings.MoneyScale);
        }
        else
        {
            var (live, original) = CostAllocator.LiveTotal(
                plan.Basis, plan.Rate ?? 0m, plan.Currency, plan.Fx, plan.AreaBasis, members, settings.Currency, settings.MoneyScale);
            baseAmount = live;
            amount = original;
        }

        ValidatePins(plan, baseAmount, settings);
        var serials = await entries.SerialsAsync(tenantId, plan.Pins.Select(p => p.RugId).ToList(), cancellationToken);
        var allocationEntry = new AllocationEntry(
            plan.Category.Group, plan.Basis, plan.Rate, amount, plan.Currency, plan.Fx, baseAmount, plan.IsReceipt && plan.Basis != CostBasis.Total,
            plan.AreaBasis, plan.Method, LateWithoutMembers: plan.Shipment is { Status: ShipmentStatus.Finalized });
        var pins = plan.Pins.Select(p => new AllocationPin(p.RugId, p.Amount, serials.GetValueOrDefault(p.RugId))).ToList();
        var result = CostAllocator.Allocate(allocationEntry, members, pins, settings.Currency, settings.MoneyScale);

        var bySerial = members.ToDictionary(m => m.RugId);
        var shares = result.Shares.Select(s => new ShareDto(
                s.RugId, bySerial.TryGetValue(s.RugId, out var m) ? m.Serial : null, s.WeightUnits, result.WeightUnit, s.Amount,
                s.IsPinned, s.PerPiece))
            .ToList();

        return BuildPreview(result, shares, plan.Basis, plan.Rate, plan.Currency, plan.AreaBasis, plan.Method,
            plan.IsReceipt && plan.Basis != CostBasis.Total, plan.Category.Name, settings);
    }

    // ═════════════════════════════════════════════════════════════════════
    // نوشتن
    // ═════════════════════════════════════════════════════════════════════

    public Task<CostEntryDto> CreateAsync(Guid tenantId, SaveCostEntryRequest request, CancellationToken cancellationToken = default)
        => LockedAsync(tenantId, ct => CreateCoreAsync(tenantId, request, ct), cancellationToken);

    private async Task<CostEntryDto> CreateCoreAsync(Guid tenantId, SaveCostEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ClientKey is { } key && await entries.FindByClientKeyAsync(tenantId, key, cancellationToken) is { } existingId)
            return await GetAsync(tenantId, existingId, cancellationToken);

        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
        var plan = await PlanAsync(tenantId, request, settings, preview: false, existing: null, cancellationToken);
        var now = _time.GetUtcNow();

        var entry = new CostEntry
        {
            TenantId = tenantId,
            CreatedAt = now,
            ClientKey = request.ClientKey
        };
        await ApplyAsync(entry, plan, settings, now, keepFrozen: false, cancellationToken);

        entry.Number = checked((int)await counters.NextBlockAsync(tenantId, CostEntry.CounterKey, 1, cancellationToken));
        await entries.AddAsync(entry, cancellationToken);
        await SyncChildrenAsync(entry, plan, cancellationToken);

        audit.Record(AuditAction.CostEntryCreated, nameof(CostEntry), entry.Id,
            $"هزینهٔ شمارهٔ {Persian(entry.Number)} ({plan.Category.Name}) ثبت شد.", plan.Category.Name,
            new { number = entry.Number, scope = plan.Scope, basis = plan.Basis, method = plan.Method });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException) when (request.ClientKey is { } retryKey)
        {
            // همان کلید هم‌زمان از درخواست دیگری ذخیره شد (دو کلیک، تلاش دوباره): همان ثبت برمی‌گردد
            unitOfWork.Reset();
            if (await entries.FindByClientKeyAsync(tenantId, retryKey, cancellationToken) is { } raced)
                return await GetAsync(tenantId, raced, cancellationToken);
            throw;
        }

        return await GetAsync(tenantId, entry.Id, cancellationToken);
    }

    public Task<CostEntryDto> UpdateAsync(Guid tenantId, Guid entryId, SaveCostEntryRequest request, CancellationToken cancellationToken = default)
        => LockedAsync(tenantId, ct => UpdateCoreAsync(tenantId, entryId, request, ct), cancellationToken);

    private async Task<CostEntryDto> UpdateCoreAsync(
        Guid tenantId, Guid entryId, SaveCostEntryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entry = await RequireTrackedAsync(tenantId, entryId, cancellationToken);
        if (entry.DeletedAt is not null)
            throw new DomainConflictException(Messages.CostIsDeleted.Text);
        if (request.Version is not { } version)
            throw AppValidationException.ForField("version", Messages.Required.Format(("label", "نسخه")));
        concurrency.Expect(entry, version);

        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
        var plan = await PlanAsync(tenantId, request, settings, preview: false, existing: entry, cancellationToken);
        var now = _time.GetUtcNow();

        var keepFrozen = entry.FrozenAt is not null && SamePricing(entry, plan);
        await ApplyAsync(entry, plan, settings, now, keepFrozen, cancellationToken);
        await SyncChildrenAsync(entry, plan, cancellationToken);
        entry.UpdatedAt = now;

        audit.Record(AuditAction.CostEntryUpdated, nameof(CostEntry), entry.Id,
            $"هزینهٔ شمارهٔ {Persian(entry.Number)} ({plan.Category.Name}) ویرایش شد.", plan.Category.Name,
            new { number = entry.Number });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(tenantId, entry.Id, cancellationToken);
    }

    public async Task<CostEntryDto> DeleteAsync(Guid tenantId, Guid entryId, uint? version, CancellationToken cancellationToken = default)
    {
        var entry = await RequireTrackedAsync(tenantId, entryId, cancellationToken);
        if (version is { } v)
            concurrency.Expect(entry, v);
        if (entry.DeletedAt is null)
        {
            var now = _time.GetUtcNow();
            entry.DeletedAt = now;
            entry.UpdatedAt = now;
            audit.Record(AuditAction.CostEntryDeleted, nameof(CostEntry), entry.Id,
                $"هزینهٔ شمارهٔ {Persian(entry.Number)} حذف شد.", null, new { number = entry.Number });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(tenantId, entry.Id, cancellationToken);
    }

    public async Task<CostEntryDto> RestoreAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await RequireTrackedAsync(tenantId, entryId, cancellationToken);
        if (entry.DeletedAt is not null)
        {
            entry.DeletedAt = null;
            entry.UpdatedAt = _time.GetUtcNow();
            audit.Record(AuditAction.CostEntryRestored, nameof(CostEntry), entry.Id,
                $"هزینهٔ شمارهٔ {Persian(entry.Number)} برگشت.", null, new { number = entry.Number });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(tenantId, entry.Id, cancellationToken);
    }

    public Task<CostEntryDto> RefreezeAsync(Guid tenantId, Guid entryId, uint version, CancellationToken cancellationToken = default)
        => LockedAsync(tenantId, ct => RefreezeCoreAsync(tenantId, entryId, version, ct), cancellationToken);

    private async Task<CostEntryDto> RefreezeCoreAsync(Guid tenantId, Guid entryId, uint version, CancellationToken cancellationToken)
    {
        var entry = await RequireTrackedAsync(tenantId, entryId, cancellationToken);
        if (entry.DeletedAt is not null)
            throw new DomainConflictException(Messages.CostIsDeleted.Text);
        concurrency.Expect(entry, version);

        if (entry.FrozenAt is not null && entry.Rate is { } rate)
        {
            var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
            var category = await entries.GetCategoryAsync(tenantId, entry.CategoryId, cancellationToken)
                           ?? throw new KeyNotFoundException("نوع هزینه یافت نشد.");
            var now = _time.GetUtcNow();
            var members = await entries.MembersAsync(tenantId, SpecOf(entry, category.Group), category.StepTypeId, entry.CreatedAt, cancellationToken);
            var (live, original) = CostAllocator.LiveTotal(
                entry.Basis, rate, entry.Currency, entry.FxRate, entry.AreaBasis, members, settings.Currency, settings.MoneyScale);
            EnsureFits(live, "amount");
            ValidatePins(entry.Pins.Select(p => new PinDto(p.RugId, p.Amount)).ToList(), live, settings);
            entry.Amount = original;
            entry.BaseAmount = live;
            entry.FrozenAt = now;
            entry.UpdatedAt = now;
            audit.Record(AuditAction.CostEntryUpdated, nameof(CostEntry), entry.Id,
                $"مبلغ رسید هزینهٔ شمارهٔ {Persian(entry.Number)} با اندازه‌های امروز به‌روز شد.", category.Name,
                new { number = entry.Number });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(tenantId, entry.Id, cancellationToken);
    }

    public Task<CostEntryDto> ResnapshotAsync(Guid tenantId, Guid entryId, uint version, CancellationToken cancellationToken = default)
        => LockedAsync(tenantId, ct => ResnapshotCoreAsync(tenantId, entryId, version, ct), cancellationToken);

    private async Task<CostEntryDto> ResnapshotCoreAsync(Guid tenantId, Guid entryId, uint version, CancellationToken cancellationToken)
    {
        var entry = await RequireTrackedAsync(tenantId, entryId, cancellationToken);
        if (entry.DeletedAt is not null)
            throw new DomainConflictException(Messages.CostIsDeleted.Text);
        concurrency.Expect(entry, version);

        var category = await entries.GetCategoryAsync(tenantId, entry.CategoryId, cancellationToken)
                       ?? throw new KeyNotFoundException("نوع هزینه یافت نشد.");
        if (entry.Scope == CostScope.IntakeBatch && category.Group == CostGroup.Purchase && entry.BatchId is { } batchId)
        {
            var batch = await entries.GetBatchAsync(tenantId, batchId, cancellationToken)
                        ?? throw new KeyNotFoundException("ورودی یافت نشد.");
            var members = await entries.BatchMemberIdsAsync(tenantId, batchId, cancellationToken);
            await ReplaceRugTargetsAsync(entry, members, cancellationToken);
            entry.ScopeNote = Messages.CostScopeNoteBatch.Format(("n", members.Count), ("number", batch.Number));
            entry.UpdatedAt = _time.GetUtcNow();
            audit.Record(AuditAction.CostEntryUpdated, nameof(CostEntry), entry.Id,
                $"فرش‌های خرید یکجای هزینهٔ شمارهٔ {Persian(entry.Number)} دوباره از ورودی {Persian(batch.Number)} گرفته شد.",
                category.Name, new { number = entry.Number, rugs = members.Count });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(tenantId, entry.Id, cancellationToken);
    }

    // ═════════════════════════════════════════════════════════════════════
    // اعتبارسنجی و برنامه
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>درخواستِ سنجیده و یکسان‌شده.</summary>
    private sealed record Plan(
        CostCategory Category,
        DateOnly Date,
        string? Description,
        string? ReceiptNo,
        Guid? ProviderId,
        CostBasis Basis,
        decimal? Rate,
        decimal? Amount,
        CurrencyUnit Currency,
        decimal? Fx,
        AreaBasis AreaBasis,
        AllocationMethod Method,
        CostScope Scope,
        CostShipmentRef? Shipment,
        CostBatchRef? Batch,
        IReadOnlyList<Guid> RugIds,
        IReadOnlyList<Guid> BaleIds,
        string? ScopeNote,
        IReadOnlyList<PinDto> Pins)
    {
        public bool IsReceipt => ProviderId is not null || ReceiptNo is not null;

        public CostScopeSpec ScopeSpec => new(Scope, Category.Group, RugIds, BaleIds, Shipment?.Id, Batch?.Id);
    }

    private async Task<Plan> PlanAsync(
        Guid tenantId, SaveCostEntryRequest request, CostTenantSettings settings, bool preview, CostEntry? existing,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string? code = null;
        void Fail(string field, string message, string? errorCode = null)
        {
            if (errors.TryAdd(field, [message])) code ??= errorCode;
        }

        var category = await entries.GetCategoryAsync(tenantId, request.CategoryId, cancellationToken)
                       ?? throw new KeyNotFoundException("نوع هزینه یافت نشد.");
        if (!category.IsActive && !preview && existing?.CategoryId != category.Id)
            Fail("categoryId", Messages.CostCategoryInactive.Text);

        // ── مبلغ ─────────────────────────────────────────────────────────
        if (!Enum.IsDefined(request.Basis) || request.Basis == CostBasis.Percent)
            Fail("basis", Messages.CostPercentBasis.Text);
        if (!Enum.IsDefined(request.Currency))
            Fail("currency", Messages.CostCurrencyInvalid.Text);
        if (!Enum.IsDefined(request.AreaBasis))
            Fail("areaBasis", Messages.InvalidValue.Format(("label", "پایهٔ متراژ")));
        if (!Enum.IsDefined(request.Method))
            Fail("method", Messages.InvalidValue.Format(("label", "روش تقسیم")));
        if (!Enum.IsDefined(request.Scope))
            Fail("scope", Messages.InvalidValue.Format(("label", "برای")));
        if (category.Group == CostGroup.Purchase && request.Method == AllocationMethod.ByPurchaseCost)
            Fail("method", Messages.CostCircular.Text, Messages.CostCircular.Code);
        if (errors.Count > 0)
            throw new AppValidationException(Messages.ValidationFailed.Text, errors, code);

        decimal? fx = null;
        if (request.Currency == CurrencyUnit.Usd)
        {
            if (request.FxRate is not { } usdFx || usdFx <= 0)
                Fail("fxRate", Messages.CostFxRequired.Text, Messages.CostFxRequired.Code);
            else if (usdFx > MaxFx || !Num.HasAtMostDecimals(usdFx, 6))
                Fail("fxRate", Messages.CostTooLarge.Text);
            else
                fx = usdFx;
        }
        else if (request.Currency != settings.Currency && settings.Currency == CurrencyUnit.Usd)
        {
            Fail("currency", Messages.CostCurrencyInvalid.Text);
        }

        decimal? amount = null;
        decimal? rate = null;
        var amountScale = request.Currency == CurrencyUnit.Usd ? AreaMath.UsdDecimals : settings.MoneyScale;
        if (request.Basis == CostBasis.Total)
        {
            if (request.Amount is not { } a)
            {
                if (!preview) Fail("amount", Messages.Required.Format(("label", "مبلغ")));
            }
            else if (Math.Abs(a) > MaxAmount)
                Fail("amount", Messages.CostTooLarge.Text);
            else if (!Num.HasAtMostDecimals(a, amountScale))
                Fail("amount", Messages.CostDecimals.Format(("currency", CurrencyLabel(request.Currency)), ("n", amountScale)));
            else
                amount = a;
        }
        else
        {
            if (request.Rate is not { } r)
            {
                if (!preview) Fail("rate", Messages.Required.Format(("label", "مبلغ")));
            }
            else if (Math.Abs(r) > MaxRate || !Num.HasAtMostDecimals(r, 4))
                Fail("rate", Messages.CostTooLarge.Text);
            else
                rate = r;
        }

        // ── برای ─────────────────────────────────────────────────────────
        CostShipmentRef? shipment = null;
        CostBatchRef? batch = null;
        IReadOnlyList<Guid> rugIds = [];
        IReadOnlyList<Guid> baleIds = [];
        var scopeNote = Trim(request.ScopeNote, CostEntry.ScopeNoteMaxLength);
        switch (request.Scope)
        {
            case CostScope.Selection:
                rugIds = (request.RugIds ?? []).Distinct().ToList();
                if (rugIds.Count == 0 && !preview)
                    Fail("rugIds", Messages.CostEmptySelection.Text, Messages.CostEmptySelection.Code);
                if (rugIds.Count > 0)
                {
                    var found = await entries.GetRugsAsync(tenantId, rugIds, cancellationToken);
                    if (found.Count != rugIds.Count)
                        throw new KeyNotFoundException("فرش یافت نشد.");
                }

                break;

            case CostScope.Bale:
                baleIds = (request.BaleIds ?? []).Distinct().ToList();
                if (baleIds.Count == 0)
                {
                    if (!preview) Fail("baleIds", Messages.CostBaleRequired.Format(settings.Terms));
                }
                else
                {
                    var bales = await entries.GetBalesAsync(tenantId, baleIds, cancellationToken);
                    if (bales.Count != baleIds.Count)
                        throw new KeyNotFoundException("نگله یافت نشد.");
                }

                break;

            case CostScope.Shipment:
                if (request.ShipmentId is not { } shipmentId)
                {
                    if (!preview) Fail("shipmentId", Messages.CostShipmentRequired.Text);
                }
                else
                {
                    shipment = await entries.GetShipmentAsync(tenantId, shipmentId, cancellationToken)
                               ?? throw new KeyNotFoundException("محموله یافت نشد.");
                }

                break;

            case CostScope.IntakeBatch:
                if (request.BatchId is not { } batchId)
                {
                    if (!preview) Fail("batchId", Messages.CostBatchRequired.Text);
                    break;
                }

                batch = await entries.GetBatchAsync(tenantId, batchId, cancellationToken)
                        ?? throw new KeyNotFoundException("ورودی یافت نشد.");
                if (category.Group == CostGroup.Purchase)
                {
                    // خرید یکجا عکس فرش‌های لحظهٔ ذخیره است (E4)؛ ویرایشی که ورودی را عوض نکرده عکس را دست نمی‌زند
                    var keep = existing is { Scope: CostScope.IntakeBatch } && existing.BatchId == batchId;
                    rugIds = keep
                        ? existing!.Targets.Where(t => t.RugId is not null).Select(t => t.RugId!.Value).ToList()
                        : await entries.BatchMemberIdsAsync(tenantId, batchId, cancellationToken);
                    scopeNote = keep && existing!.ScopeNote is not null
                        ? existing.ScopeNote
                        : Messages.CostScopeNoteBatch.Format(("n", rugIds.Count), ("number", batch.Number));
                }

                break;
        }

        // ── مبلغ‌های ثابت ───────────────────────────────────────────────
        var pins = new List<PinDto>();
        var pinRugs = new HashSet<Guid>();
        foreach (var pin in request.Pins ?? [])
        {
            if (!pinRugs.Add(pin.RugId))
            {
                Fail("pins", Messages.InvalidValue.Format(("label", "مبلغ ثابت")));
                continue;
            }

            if (Math.Abs(pin.Amount) > MaxAmount)
                Fail("pins", Messages.CostTooLarge.Text);
            else if (!Num.HasAtMostDecimals(pin.Amount, settings.MoneyScale))
                Fail("pins", Messages.CostDecimals.Format(("currency", CurrencyLabel(settings.Currency)), ("n", settings.MoneyScale)));
            else
                pins.Add(pin);
        }

        if (pins.Count > 0)
        {
            var found = await entries.GetRugsAsync(tenantId, pins.Select(p => p.RugId).ToList(), cancellationToken);
            if (found.Count != pins.Count)
                throw new KeyNotFoundException("فرش یافت نشد.");
        }

        // ── چه و کی ──────────────────────────────────────────────────────
        Guid? providerId = null;
        if (request.ServiceProviderId is { } provider)
        {
            _ = await entries.ProviderNameAsync(tenantId, provider, cancellationToken)
                ?? throw new KeyNotFoundException("طرف خدمات یافت نشد.");
            providerId = provider;
        }

        var description = Clean(request.Description);
        if (description is { Length: > CostEntry.DescriptionMaxLength })
            Fail("description", Messages.TooLong.Format(("label", "توضیح"), ("n", CostEntry.DescriptionMaxLength)));
        var receiptNo = string.IsNullOrWhiteSpace(request.ReceiptNo) ? null : PersianText.Normalize(request.ReceiptNo);
        if (receiptNo is { Length: > CostEntry.ReceiptNoMaxLength })
            Fail("receiptNo", Messages.TooLong.Format(("label", "شمارهٔ رسید"), ("n", CostEntry.ReceiptNoMaxLength)));

        var plan = new Plan(
            category,
            request.Date == default ? IranDay.Today(_time.GetUtcNow()) : request.Date,
            description, receiptNo, providerId,
            request.Basis, rate, amount, request.Currency, fx,
            request.AreaBasis, request.Method, request.Scope,
            shipment, batch, rugIds, baleIds, scopeNote, pins);

        // مبلغ ثابت فقط روی «کل» یا رسید منجمد (E9)
        if (pins.Count > 0 && plan.Basis != CostBasis.Total && !plan.IsReceipt)
            Fail("pins", Messages.CostPinsBasis.Text, Messages.CostPinsBasis.Code);

        if (errors.Count > 0)
            throw new AppValidationException(Messages.ValidationFailed.Text, errors, code);
        return plan;
    }

    /// <summary>مقدارهای برنامه روی ثبت، با مبلغ پایه و قاعدهٔ انجماد (D.3).</summary>
    private async Task ApplyAsync(
        CostEntry entry, Plan plan, CostTenantSettings settings, DateTimeOffset now, bool keepFrozen, CancellationToken cancellationToken)
    {
        entry.CategoryId = plan.Category.Id;
        entry.Date = plan.Date;
        entry.Description = plan.Description;
        entry.ReceiptNo = plan.ReceiptNo;
        entry.ServiceProviderId = plan.ProviderId;
        entry.Basis = plan.Basis;
        entry.Currency = plan.Currency;
        entry.FxRate = plan.Fx;
        entry.AreaBasis = plan.AreaBasis;
        entry.Method = plan.Method;
        entry.Scope = plan.Scope;
        entry.ShipmentId = plan.Shipment?.Id;
        entry.BatchId = plan.Batch?.Id;
        entry.ScopeNote = plan.ScopeNote;

        if (plan.Basis == CostBasis.Total)
        {
            entry.Rate = null;
            entry.Amount = plan.Amount ?? 0m;
            entry.FrozenAt = null;
            entry.BaseAmount = CostAllocator.BaseOfTotal(entry.Amount.Value, plan.Currency, settings.Currency, plan.Fx, settings.MoneyScale);
        }
        else
        {
            entry.Rate = plan.Rate ?? 0m;
            if (!(keepFrozen && plan.IsReceipt))
            {
                var members = await entries.MembersAsync(
                    entry.TenantId, plan.ScopeSpec, plan.Category.StepTypeId, entry.CreatedAt, cancellationToken);
                var (live, original) = CostAllocator.LiveTotal(
                    plan.Basis, entry.Rate.Value, plan.Currency, plan.Fx, plan.AreaBasis, members, settings.Currency, settings.MoneyScale);
                entry.Amount = original;
                entry.BaseAmount = live;
                // رسید (طرف حساب یا شمارهٔ رسید) همین لحظه منجمد می‌شود؛ بی آن زنده است و کلش با فرش‌ها عوض می‌شود
                entry.FrozenAt = plan.IsReceipt ? now : null;
            }
        }

        EnsureFits(entry.BaseAmount, plan.Basis == CostBasis.Total ? "amount" : "rate");
        if (entry.Amount is { } stored)
            EnsureFits(stored, plan.Basis == CostBasis.Total ? "amount" : "rate");
        ValidatePins(plan.Pins, entry.BaseAmount, settings);
    }

    /// <summary>هدف‌ها و مبلغ‌های ثابت با برنامه یکی می‌شوند (فقط فرق‌ها؛ فرزند تازه با AddAsync).</summary>
    private async Task SyncChildrenAsync(CostEntry entry, Plan plan, CancellationToken cancellationToken)
    {
        var wantsRugs = plan.Scope == CostScope.Selection
                        || (plan.Scope == CostScope.IntakeBatch && plan.Category.Group == CostGroup.Purchase);
        await ReplaceRugTargetsAsync(entry, wantsRugs ? plan.RugIds : [], cancellationToken);

        var wantedBales = plan.Scope == CostScope.Bale ? plan.BaleIds.ToHashSet() : [];
        foreach (var target in entry.Targets.Where(t => t.BaleId is not null).ToList())
        {
            if (!wantedBales.Remove(target.BaleId!.Value))
                entries.RemoveTarget(target);
        }

        foreach (var baleId in wantedBales)
            await entries.AddTargetAsync(new CostEntryTarget { TenantId = entry.TenantId, EntryId = entry.Id, BaleId = baleId }, cancellationToken);

        var wantedPins = plan.Pins.ToDictionary(p => p.RugId, p => p.Amount);
        foreach (var pin in entry.Pins.ToList())
        {
            if (wantedPins.Remove(pin.RugId, out var amount))
                pin.Amount = amount;
            else
                entries.RemovePin(pin);
        }

        foreach (var (rugId, amount) in wantedPins)
            await entries.AddPinAsync(new CostEntryPin { TenantId = entry.TenantId, EntryId = entry.Id, RugId = rugId, Amount = amount }, cancellationToken);
    }

    private async Task ReplaceRugTargetsAsync(CostEntry entry, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
    {
        var wanted = rugIds.ToHashSet();
        foreach (var target in entry.Targets.Where(t => t.RugId is not null).ToList())
        {
            if (!wanted.Remove(target.RugId!.Value))
                entries.RemoveTarget(target);
        }

        foreach (var rugId in wanted)
            await entries.AddTargetAsync(new CostEntryTarget { TenantId = entry.TenantId, EntryId = entry.Id, RugId = rugId }, cancellationToken);
    }

    /// <summary>ورودی‌های قیمت (و فرش‌ها) همان‌اند؛ پس رسید منجمد همان مبلغ را نگه می‌دارد.</summary>
    private static bool SamePricing(CostEntry entry, Plan plan)
    {
        if (entry.Basis != plan.Basis || entry.Rate != plan.Rate || entry.Currency != plan.Currency || entry.FxRate != plan.Fx
            || entry.AreaBasis != plan.AreaBasis || entry.Scope != plan.Scope || entry.ShipmentId != plan.Shipment?.Id
            || entry.BatchId != plan.Batch?.Id)
            return false;

        var rugs = entry.Targets.Where(t => t.RugId is not null).Select(t => t.RugId!.Value).ToHashSet();
        var bales = entry.Targets.Where(t => t.BaleId is not null).Select(t => t.BaleId!.Value).ToHashSet();
        var planRugs = plan.Scope == CostScope.Selection || (plan.Scope == CostScope.IntakeBatch && plan.Category.Group == CostGroup.Purchase)
            ? plan.RugIds.ToHashSet()
            : [];
        return rugs.SetEquals(planRugs) && bales.SetEquals(plan.Scope == CostScope.Bale ? plan.BaleIds : []);
    }

    private static void ValidatePins(Plan plan, decimal baseAmount, CostTenantSettings settings)
        => ValidatePins(plan.Pins, baseAmount, settings);

    /// <summary>
    /// <c>E_PINS_EXCEED</c> (D.3): هر مبلغ ثابت هم‌علامت کل (یا صفر) و جمعشان در قدر مطلق بیشتر از کل نباشد — سنجش به ارز کارگاه،
    /// بعد از تبدیل دلار یا ریال.
    /// </summary>
    private static void ValidatePins(IReadOnlyCollection<PinDto> pins, decimal baseAmount, CostTenantSettings settings)
    {
        if (pins.Count == 0) return;
        var sum = pins.Sum(p => p.Amount);
        var wrongSign = pins.Any(p => p.Amount != 0m && Math.Sign(p.Amount) != Math.Sign(baseAmount));
        if (wrongSign || Math.Abs(sum) > Math.Abs(baseAmount))
        {
            var message = Messages.CostPinsExceed.Format(
                ("pins", Money(sum, settings.Currency)), ("total", Money(baseAmount, settings.Currency)));
            throw AppValidationException.ForField("pins", message, Messages.CostPinsExceed.Code);
        }
    }

    private static void EnsureFits(decimal value, string field)
    {
        if (Math.Abs(value) > MaxAmount)
            throw AppValidationException.ForField(field, Messages.CostTooLarge.Text);
    }

    private async Task<CostEntry> RequireTrackedAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken)
        => await entries.GetTrackedAsync(tenantId, entryId, cancellationToken)
           ?? throw new KeyNotFoundException("هزینه یافت نشد.");

    private static CostScopeSpec SpecOf(CostEntry entry, CostGroup group)
        => new(entry.Scope, group,
            entry.Targets.Where(t => t.RugId is not null).Select(t => t.RugId!.Value).ToList(),
            entry.Targets.Where(t => t.BaleId is not null).Select(t => t.BaleId!.Value).ToList(),
            entry.ShipmentId, entry.BatchId);

    // ═════════════════════════════════════════════════════════════════════
    // DTOها
    // ═════════════════════════════════════════════════════════════════════

    private static CostEntryDto ToDto(CostEntryDetail detail, CostTenantSettings settings)
    {
        var entry = detail.Entry;
        var request = new SaveCostEntryRequest(
            entry.CategoryId, entry.Date, entry.Description, entry.ReceiptNo, entry.ServiceProviderId, entry.Basis, entry.Rate,
            entry.Amount, entry.Currency, entry.FxRate, entry.AreaBasis, entry.Method, entry.Scope, entry.ShipmentId, entry.BatchId,
            detail.TargetRugIds, detail.TargetBaleIds, entry.ScopeNote, detail.Pins, detail.Version, entry.ClientKey);

        var frozen = entry.IsFrozen;
        var unit = WeightUnitOf(entry);
        var shares = detail.Shares
            .Select(s => new ShareDto(s.RugId, SerialText(s.SerialNo, s.Sku), s.WeightUnits, s.IsPinned ? ShareWeightUnits.Amount : unit,
                s.Amount, s.IsPinned, s.Quantity > 1 ? Num.Round(s.Amount / s.Quantity, settings.MoneyScale) : null))
            .ToList();

        var stored = detail.Result;
        var warnings = stored is null ? [] : CostWarningJson.Parse(stored.WarningsJson);
        var baseAmount = stored?.BaseAmount ?? entry.BaseAmount;
        var result = new AllocationResult(
            baseAmount,
            entry.IsLive && entry.Currency == settings.Currency ? baseAmount : entry.Amount ?? baseAmount,
            stored?.Lines ?? 0, stored?.Pieces ?? 0, stored?.AreaM2 ?? 0m, stored?.TotalWeightUnits ?? 0,
            stored?.Allocated ?? 0m, stored?.Unallocated ?? baseAmount,
            stored?.State ?? AllocationState.Unallocated, [], warnings, unit);

        var preview = BuildPreview(result, shares, entry.Basis, entry.Rate, entry.Currency, entry.AreaBasis, entry.Method, frozen,
            detail.CategoryName, settings);

        return new CostEntryDto(entry.Id, entry.Number, request, detail.Version, preview, entry.CreatedAt,
            detail.CategoryName, detail.Group, entry.FrozenAt, entry.DeletedAt is not null, detail.ServiceProviderName, detail.CategoryIcon);
    }

    private static string WeightUnitOf(CostEntry entry)
    {
        if (entry.IsLive)
            return entry.Basis == CostBasis.PerM2 ? ShareWeightUnits.AreaHundredths : ShareWeightUnits.Pieces;
        var method = entry.IsFrozen ? (entry.Basis == CostBasis.PerM2 ? AllocationMethod.ByArea : AllocationMethod.ByCount) : entry.Method;
        return method switch
        {
            AllocationMethod.ByArea => ShareWeightUnits.AreaHundredths,
            AllocationMethod.ByCount => ShareWeightUnits.Pieces,
            AllocationMethod.Equal => ShareWeightUnits.Equal,
            AllocationMethod.ByDeclaredValue => ShareWeightUnits.UsdCents,
            AllocationMethod.ByPurchaseCost => ShareWeightUnits.PurchaseCents,
            _ => ShareWeightUnits.Amount
        };
    }

    /// <summary>پیش‌نمایش/نتیجه با جمله و پیام‌های فارسی (واژهٔ نگله و واحد پول کارگاه).</summary>
    internal static AllocationPreviewDto BuildPreview(
        AllocationResult result, IReadOnlyList<ShareDto> shares, CostBasis basis, decimal? rate, CurrencyUnit currency,
        AreaBasis areaBasis, AllocationMethod method, bool frozen, string categoryName, CostTenantSettings settings)
    {
        var perM2 = RugCostMath.CostPerM2(result.BaseAmount, result.AreaM2, settings.MoneyScale);
        var warnings = result.Warnings.Select(w => ToWarning(w, categoryName, settings)).ToList();
        var sentence = Sentence(result, basis, rate, currency, areaBasis, method, frozen, categoryName, settings);
        return new AllocationPreviewDto(result.OriginalAmount, result.BaseAmount, result.Lines, result.Pieces, result.AreaM2, perM2,
            result.State, result.Unallocated, warnings, shares, sentence);
    }

    /// <summary>یک هشدار با پیام فارسی.</summary>
    internal static WarningDto ToWarning(AllocationWarning warning, string categoryName, CostTenantSettings settings)
    {
        var template = Messages.Find(warning.Code) ?? Messages.ServerError;
        var text = template.Format(settings.Terms,
            ("n", warning.Count),
            ("category", categoryName),
            ("amount", warning.Amount is { } a ? Messages.FormatNumber(a) : string.Empty),
            ("currency", CurrencyLabel(settings.Currency)),
            ("serial", PersianText.ToPersianDigits(warning.Serial ?? string.Empty)));
        return new WarningDto(warning.Code, text, warning.Count);
    }

    /// <summary>جملهٔ پنل (H.2).</summary>
    internal static string Sentence(
        AllocationResult result, CostBasis basis, decimal? rate, CurrencyUnit currency, AreaBasis areaBasis,
        AllocationMethod method, bool frozen, string categoryName, CostTenantSettings settings)
    {
        if (basis != CostBasis.Total && !frozen)
        {
            var template = basis == CostBasis.PerM2 ? Messages.CostSentencePerM2 : Messages.CostSentencePerPiece;
            return template.Format(
                ("rate", Messages.FormatNumber(rate ?? 0m)),
                ("currency", CurrencyLabel(currency)),
                ("area", result.AreaM2),
                ("pieces", result.Pieces),
                ("amount", Messages.FormatNumber(result.OriginalAmount)));
        }

        var effective = frozen ? (basis == CostBasis.PerM2 ? AllocationMethod.ByArea : AllocationMethod.ByCount) : method;
        var methodText = MethodLabel(effective);
        if (effective == AllocationMethod.ByArea && areaBasis == AreaBasis.Intake)
            methodText += " " + Messages.CostAreaBasisIntake.Text;

        var sentence = frozen ? Messages.CostSentenceReceipt : Messages.CostSentenceTotal;
        return sentence.Format(
            ("amount", Messages.FormatNumber(frozen ? result.BaseAmount : result.OriginalAmount)),
            ("currency", CurrencyLabel(frozen ? settings.Currency : currency)),
            ("category", categoryName),
            ("pieces", result.Pieces),
            ("method", methodText));
    }

    /// <summary>برچسب روش برای جمله و انتخابگر.</summary>
    public static string MethodLabel(AllocationMethod method) => method switch
    {
        AllocationMethod.ByArea => Messages.CostMethodByArea.Text,
        AllocationMethod.ByCount => Messages.CostMethodByCount.Text,
        AllocationMethod.Equal => Messages.CostMethodEqual.Text,
        AllocationMethod.ByDeclaredValue => Messages.CostMethodByDeclared.Text,
        AllocationMethod.ByPurchaseCost => Messages.CostMethodByPurchase.Text,
        _ => Messages.CostMethodManual.Text
    };

    /// <summary>برچسب واحد پول.</summary>
    public static string CurrencyLabel(CurrencyUnit currency) => currency switch
    {
        CurrencyUnit.Rial => "ریال",
        CurrencyUnit.Usd => "دلار",
        _ => "تومان"
    };

    private static CostEntryListItemDto ToListItem(CostEntryListRow row, CostTenantSettings settings)
    {
        var result = row.Result;
        var pieces = result?.Pieces ?? 0;
        var targetDeleted = (row.Scope == CostScope.Shipment && row.ShipmentNumber is null)
                            || (row.Scope == CostScope.Bale && row.BaleNumbers.Count == 0)
                            || (row.Scope == CostScope.IntakeBatch && row.BatchNumber is null && row.Group == CostGroup.Expense);
        var text = targetDeleted
            ? Messages.CostUnallocatedChip.Text
            : row.Scope switch
            {
                CostScope.Shipment => Messages.CostTargetShipment.Format(("number", row.ShipmentNumber), ("pieces", pieces)),
                CostScope.Bale => Messages.CostTargetBales.Format(settings.Terms,
                    ("numbers", string.Join("، ", row.BaleNumbers.Order().Select(n => PersianText.ToPersianDigits(n.ToString(CultureInfo.InvariantCulture))))),
                    ("pieces", pieces)),
                CostScope.IntakeBatch when row.Group == CostGroup.Purchase && row.ScopeNote is not null => row.ScopeNote,
                CostScope.IntakeBatch => Messages.CostTargetBatch.Format(("number", row.BatchNumber), ("pieces", pieces)),
                _ => row.ScopeNote ?? Messages.CostTargetRugs.Format(("n", result?.Lines ?? row.TargetRugCount), ("pieces", pieces))
            };

        var baseAmount = result?.BaseAmount ?? row.BaseAmount;
        decimal? original = row.Currency != settings.Currency ? row.Amount : null;
        var warnings = result is null ? [] : CostWarningJson.Parse(result.WarningsJson).Select(w => w.Code).ToList();
        return new CostEntryListItemDto(row.Id, row.Number, row.Date, row.Category, row.Group, row.Description, text, baseAmount,
            original, row.Currency, result?.State ?? AllocationState.Unallocated, result?.Unallocated ?? baseAmount, row.Deleted,
            row.Version, row.CategoryIcon, row.Basis, row.FrozenAt is not null, warnings);
    }

    private static string? SerialText(string? serial, string sku) => serial ?? sku;

    private static string Money(decimal value, CurrencyUnit currency) => $"{Messages.FormatNumber(value)} {CurrencyLabel(currency)}";

    private static string? Trim(string? value, int max)
    {
        var cleaned = Clean(value);
        return cleaned is { Length: > 0 } && cleaned.Length > max ? cleaned[..max] : cleaned;
    }

    /// <summary>متن یکسان‌شده (ی و ک، NFC)؛ نیم‌فاصله می‌ماند. خالی → null.</summary>
    internal static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim()
            .Replace(PersianText.ArabicYeh, PersianText.PersianYeh, StringComparison.Ordinal)
            .Replace(PersianText.ArabicKaf, PersianText.PersianKaf, StringComparison.Ordinal)
            .Normalize(System.Text.NormalizationForm.FormC);
        return text.Length == 0 ? null : text;
    }

    /// <summary>انجماد رسید و عکس خرید باید اعضای همان تراکنش را ببینند؛ قفلِ دیرهنگامِ Save جلوی عکس کهنه را نمی‌گیرد.</summary>
    private Task<CostEntryDto> LockedAsync(Guid tenantId, Func<CancellationToken, Task<CostEntryDto>> work, CancellationToken ct)
        => ledgerLock is null ? work(ct) : unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ledgerLock.AcquireAsync(tenantId, token);
            return await work(token);
        }, ct);

    private static string Persian(int number) => PersianText.ToPersianDigits(number.ToString(CultureInfo.InvariantCulture));
}

/// <summary>ارز، رقم اعشار و واژهٔ نگلهٔ کارگاه برای پیام‌ها و حساب.</summary>
public sealed record CostTenantSettings(CurrencyUnit Currency, int MoneyScale, BaleTerm BaleTerm)
{
    public BaleTermWords Terms => BaleTerms.For(BaleTerm);
}
