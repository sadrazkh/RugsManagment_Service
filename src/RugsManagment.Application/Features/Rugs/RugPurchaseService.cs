using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>
/// سطرهای خرید یک فرش و «این عدد از کجا آمد؟» (ریز هزینه) — فقط Business (A.5).
/// </summary>
public interface IRugPurchaseService
{
    /// <summary><c>GET /api/rugs/{id}/purchase-lines</c>: سطرها، جمعشان، نسخهٔ فرش و خلاصهٔ بها.</summary>
    Task<PurchaseLinesDto> GetAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /api/rugs/{id}/purchase-lines</c>: همهٔ سطرها یک‌جا جایگزین می‌شوند (سطری که نیامده حذف). با نسخهٔ فرش؛ ذخیره
    /// <c>UpdatedAt</c> فرش را هم می‌نویسد تا نسخه جلو برود و دو PUT با یک نسخه دقیقاً یک ۴۰۹ بگیرند (I.2).
    /// </summary>
    Task<PurchaseLinesDto> ReplaceAsync(Guid tenantId, Guid rugId, ReplacePurchaseLinesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/rugs/{id}/cost-breakdown</c>: سطرهای خرید، مراحل، سهم ثبت‌های هزینه و خلاصه.</summary>
    Task<RugCostBreakdownDto> BreakdownAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);

    /// <summary>نوع‌های فعال گروه «خرید» کارگاه، به ترتیب (گزینه‌های ویرایشگر سطرها).</summary>
    Task<IReadOnlyList<PurchaseCategoryOptionDto>> CategoriesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// ویرایشگر سطرهای خرید و ریز هزینهٔ فرش.
///
/// <para>مبلغ هیچ سطری این‌جا حساب نمی‌شود: درخواست فقط برچسب، نوع، مبنا و نرخ را می‌نویسد و <c>PurchaseLineStage</c>
/// در همان ذخیره (پاس سازگاری) مبلغ‌ها را با قاعدهٔ دسته حساب می‌کند؛ پاسخ همان مبلغ‌های تازه را دارد. اگر مبلغ از کاربر
/// پذیرفته می‌شد، دو منبع برای یک عدد داشتیم.</para>
/// </summary>
public sealed class RugPurchaseService(
    IRugRepository rugs,
    IRepository<RugPurchaseLine> purchaseLines,
    IRepository<CostCategory> categories,
    IUnitOfWork unitOfWork,
    IConcurrencyGuard concurrency,
    IMoneyVisibility money,
    IRugDtoBuilder dtos,
    IAuditLog audit) : IRugPurchaseService
{
    public async Task<PurchaseLinesDto> GetAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
    {
        EnsureBusiness(write: false);
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, cancellationToken)
                  ?? throw new KeyNotFoundException("فرش یافت نشد.");
        return await ResultAsync(rug, cancellationToken);
    }

    public async Task<PurchaseLinesDto> ReplaceAsync(
        Guid tenantId, Guid rugId, ReplacePurchaseLinesRequest request, CancellationToken cancellationToken = default)
    {
        EnsureBusiness(write: true);
        ArgumentNullException.ThrowIfNull(request);

        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, cancellationToken)
                  ?? throw new KeyNotFoundException("فرش یافت نشد.");
        concurrency.Expect(rug, request.Version);

        var existing = (await purchaseLines.ListAsync(l => l.RugId == rug.Id && l.TenantId == tenantId, cancellationToken))
            .ToDictionary(l => l.Id);
        var purchaseCategories = (await categories.ListAsync(
                c => c.TenantId == tenantId && c.Group == CostGroup.Purchase, cancellationToken))
            .ToDictionary(c => c.Id);

        var planned = Validate(request.Lines ?? [], existing, purchaseCategories);
        var now = DateTimeOffset.UtcNow;

        // حذف: هر سطر قبلی که در درخواست نیست
        var kept = planned.Where(p => p.Id is not null).Select(p => p.Id!.Value).ToHashSet();
        foreach (var gone in existing.Values.Where(l => !kept.Contains(l.Id)))
        {
            var tracked = await purchaseLines.GetByIdAsync(gone.Id, cancellationToken)
                          ?? throw new KeyNotFoundException(Messages.PurchaseLineUnknown.Text);
            purchaseLines.Remove(tracked);
        }

        foreach (var line in planned)
        {
            if (line.Id is { } id)
            {
                var before = existing[id];
                if (before.Label == line.Label && before.CategoryId == line.CategoryId && before.Basis == line.Basis
                    && before.Rate == line.Rate && before.SortOrder == line.SortOrder && before.Notes == line.Notes)
                    continue;

                var tracked = await purchaseLines.GetByIdAsync(id, cancellationToken)
                              ?? throw new KeyNotFoundException(Messages.PurchaseLineUnknown.Text);
                tracked.Label = line.Label;
                tracked.CategoryId = line.CategoryId;
                tracked.Basis = line.Basis;
                tracked.Rate = line.Rate;
                tracked.SortOrder = line.SortOrder;
                tracked.Notes = line.Notes;
                tracked.UpdatedAt = now;
                continue;
            }

            await purchaseLines.AddAsync(new RugPurchaseLine
            {
                TenantId = tenantId,
                RugId = rug.Id,
                CategoryId = line.CategoryId,
                Label = line.Label,
                Basis = line.Basis,
                Rate = line.Rate,
                SortOrder = line.SortOrder,
                Notes = line.Notes
            }, cancellationToken);
        }

        // ستون خود فرش نوشته می‌شود تا نسخه سنجیده شود و جلو برود (G.2 «Concurrency»، I.2)
        rug.UpdatedAt = now;
        audit.Record(AuditAction.Updated, nameof(Rug), rug.Id,
            $"قیمت خرید فرش {PersianText.ToPersianDigits(rug.SerialNo ?? rug.Sku)} ویرایش شد ({PersianText.ToPersianDigits(planned.Count.ToString())} سطر).",
            rug.Sku);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ResultAsync(rug, cancellationToken);
    }

    public async Task<RugCostBreakdownDto> BreakdownAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
    {
        EnsureBusiness(write: false);
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, cancellationToken)
                  ?? throw new KeyNotFoundException("فرش یافت نشد.");
        var dto = await dtos.BuildAsync(rug, cancellationToken);

        var steps = rug.WorkflowSteps
            .OrderBy(s => s.OrderIndex)
            .Select(s => new StepCostDto(
                s.Id,
                s.ProcessStepTypeId,
                s.ProcessStepType?.NameFa ?? string.Empty,
                s.OrderIndex,
                s.Status,
                s.ServiceProvider?.Name,
                s.AppliedPricingModel?.ToString(),
                s.AppliedUnitRate,
                s.PricedLength,
                s.PricedWidth,
                s.PricedQuantity,
                // فرمول مبلغ مرحله فقط در RugCostMath است (یک جای فرمول، D.4)؛ همان عددی که در جمع می‌آید، با کف صفر
                s.EffectiveCost,
                RugCostMath.CountsTowardCost(s.Status) ? s.EffectiveCost : 0m))
            .ToList();

        return new RugCostBreakdownDto(
            dto.PurchaseLines ?? [],
            steps,
            [], // سهم ثبت‌های هزینه از فاز هزینه‌ها (P5) پر می‌شود
            dto.Costs ?? EmptySummary(rug));
    }

    public async Task<IReadOnlyList<PurchaseCategoryOptionDto>> CategoriesAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureBusiness(write: false);
        var list = await categories.ListAsync(
            c => c.TenantId == tenantId && c.Group == CostGroup.Purchase && c.IsActive, cancellationToken);
        return list
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => new PurchaseCategoryOptionDto(c.Id, c.Name, c.SeedKey, c.DefaultBasis, c.DefaultRate, c.ShowInIntake, c.SortOrder))
            .ToList();
    }

    // ═════════════════════════════════════════════════════════════════════
    // سنجش
    // ═════════════════════════════════════════════════════════════════════

    private sealed record PlannedLine(Guid? Id, Guid? CategoryId, string Label, CostBasis Basis, decimal Rate, int SortOrder, string? Notes);

    /// <summary>
    /// هر سطر: برچسب لازم (حداکثر ۱۲۰ حرف)، مبنای شناخته، نرخ معتبر (درصد ۰ تا ۱۰۰، نرخ متری/دانه‌ای نامنفی، حداکثر ۴ رقم
    /// اعشار)، نوع از گروه «خرید» همین کارگاه (نوع غیرفعال فقط برای سطری که از قبل همان نوع را داشت)، شناسهٔ سطرِ همین فرش و
    /// بی تکرار. خطاها همه با هم، با کلید <c>lines[i].field</c>.
    /// </summary>
    private static List<PlannedLine> Validate(
        IReadOnlyList<PurchaseLineDto> lines,
        IReadOnlyDictionary<Guid, RugPurchaseLine> existing,
        IReadOnlyDictionary<Guid, CostCategory> purchaseCategories)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Add(int index, string field, string message) => errors.TryAdd($"lines[{index}].{field}", [message]);

        if (lines.Count > RugFieldWriter.MaxPurchaseLines)
            errors["lines"] = [Messages.PurchaseTooManyLines.Format(("n", RugFieldWriter.MaxPurchaseLines))];

        var seen = new HashSet<Guid>();
        var planned = new List<PlannedLine>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line is null)
            {
                errors.TryAdd($"lines[{i}]", [Messages.InvalidValue.Format(("label", "سطر خرید"))]);
                continue;
            }

            if (line.Id is { } id && (!existing.ContainsKey(id) || !seen.Add(id)))
                Add(i, "id", Messages.PurchaseLineUnknown.Text);

            var label = PersianText.Normalize(line.Label);
            if (label.Length == 0)
                Add(i, "label", Messages.Required.Format(("label", "نام سطر")));
            else if (label.Length > RugPurchaseLine.LabelMaxLength)
                Add(i, "label", Messages.TooLong.Format(("label", "نام سطر"), ("n", RugPurchaseLine.LabelMaxLength)));

            if (!Enum.IsDefined(line.Basis))
                Add(i, "basis", Messages.InvalidValue.Format(("label", "مبنا")));
            else if (RugFieldWriter.CheckRate(line.Rate, line.Basis) is { } problem)
                Add(i, "rate", problem);

            if (line.CategoryId is { } categoryId)
            {
                var keepsInactive = line.Id is { } lineId && existing.TryGetValue(lineId, out var before)
                                    && before.CategoryId == categoryId;
                if (!purchaseCategories.TryGetValue(categoryId, out var category) || (!category.IsActive && !keepsInactive))
                    Add(i, "categoryId", Messages.PurchaseCategoryInvalid.Text);
            }

            var notes = string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim();
            if (notes is { Length: > RugFieldWriter.NotesMaxLength })
                Add(i, "notes", Messages.TooLong.Format(("label", "یادداشت"), ("n", RugFieldWriter.NotesMaxLength)));

            planned.Add(new PlannedLine(line.Id, line.CategoryId, label, line.Basis, line.Rate, line.SortOrder, notes));
        }

        if (errors.Count > 0)
            throw new AppValidationException(
                errors.Count == 1 ? errors.Values.First()[0] : Messages.ValidationFailed.Text, errors);

        return planned;
    }

    private async Task<PurchaseLinesDto> ResultAsync(Rug rug, CancellationToken cancellationToken)
    {
        var dto = await dtos.BuildAsync(rug, cancellationToken);
        var lines = dto.PurchaseLines ?? [];
        return new PurchaseLinesDto(lines, lines.Sum(l => l.Amount), concurrency.VersionOf(rug), dto.Costs);
    }

    /// <summary>خلاصهٔ صفر وقتی پاس هنوز این فرش را حساب نکرده (<c>Computed = false</c>).</summary>
    private static RugCostSummaryDto EmptySummary(Rug rug)
        => new(0m, 0m, 0m, rug.TargetSalePrice, null, CostPerPiece: 0m, Computed: false);

    /// <summary>این بخش‌ها پول‌اند؛ کنترلر هم Business است، ولی سرویس خودش هم مطمئن می‌شود (A.5).</summary>
    private void EnsureBusiness(bool write)
    {
        if (!money.CanSeeMoney)
            throw new UnauthorizedAccessException(write ? Messages.MoneyWriteForbidden.Text : Messages.Forbidden.Text);
    }
}
