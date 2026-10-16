using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// «نوع‌های هزینه» (spec B.7، I.2 <c>/api/cost-categories</c>): کاشی‌های پنل، «+ نوع تازه» از پنل (Business) و صفحهٔ تنظیم
/// (Configure). سیاست را کنترلر می‌سنجد؛ گروهِ نوعِ به کار رفته را قفل پاس (<c>CostCategoryGuard</c>) نگه می‌دارد.
/// </summary>
public interface ICostCategoryService
{
    /// <summary>نوع‌ها؛ <paramref name="purchaseOnly"/> برای کسی که Business نیست (I.2: Floor فقط گروه خرید).</summary>
    Task<IReadOnlyList<CostCategoryDto>> ListAsync(
        Guid tenantId, bool purchaseOnly, bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>«+ نوع تازه» با پیش‌فرض‌های گروه؛ نام تکراریِ همان گروه همان نوع را برمی‌گرداند (و فعالش می‌کند).</summary>
    Task<CostCategoryDto> QuickCreateAsync(Guid tenantId, QuickCostCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CostCategoryDto> CreateAsync(Guid tenantId, SaveCostCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>ویرایش با نسخه؛ تغییر گروهِ نوعِ به کار رفته ۴۰۹ (B.7).</summary>
    Task<CostCategoryDto> UpdateAsync(Guid tenantId, Guid categoryId, SaveCostCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>حذف؛ نوعِ به کار رفته فقط غیرفعال می‌شود (B.13).</summary>
    Task<DeleteCostCategoryResultDto> DeleteAsync(Guid tenantId, Guid categoryId, uint? version, CancellationToken cancellationToken = default);
}

/// <summary>پیاده‌سازی نوع‌های هزینه.</summary>
public sealed class CostCategoryService(
    ICostEntryRepository entries,
    IConcurrencyGuard concurrency,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    TimeProvider? time = null) : ICostCategoryService
{
    /// <summary>آیکون پیش‌فرض نوع تازه (باید در <c>icons.svg</c> باشد).</summary>
    public const string DefaultExpenseIcon = "coins";

    public const string DefaultPurchaseIcon = "receipt";

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public Task<IReadOnlyList<CostCategoryDto>> ListAsync(
        Guid tenantId, bool purchaseOnly, bool includeInactive, CancellationToken cancellationToken = default)
        => entries.ListCategoriesAsync(tenantId, purchaseOnly ? CostGroup.Purchase : null, includeInactive, cancellationToken);

    public async Task<CostCategoryDto> QuickCreateAsync(
        Guid tenantId, QuickCostCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ValidName(request.Name);
        if (!Enum.IsDefined(request.Group))
            throw AppValidationException.ForField("group", Messages.InvalidValue.Format(("label", "گروه")));

        var existing = (await entries.ListCategoriesAsync(tenantId, request.Group, includeInactive: true, cancellationToken))
            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
        if (existing is not null)
        {
            if (existing.IsActive) return existing;
            var tracked = await entries.GetCategoryTrackedAsync(tenantId, existing.Id, cancellationToken)
                          ?? throw new KeyNotFoundException("نوع هزینه یافت نشد.");
            tracked.IsActive = true;
            tracked.UpdatedAt = _time.GetUtcNow();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await RequireAsync(tenantId, tracked.Id, cancellationToken);
        }

        var category = Defaults(tenantId, name, request.Group);
        category.SortOrder = await entries.MaxCategorySortAsync(tenantId, cancellationToken) + 10;
        await entries.AddCategoryAsync(category, cancellationToken);
        audit.Record(AuditAction.CostCategoryChanged, nameof(CostCategory), category.Id, $"نوع هزینهٔ «{name}» ساخته شد.", name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await RequireAsync(tenantId, category.Id, cancellationToken);
    }

    public async Task<CostCategoryDto> CreateAsync(Guid tenantId, SaveCostCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var category = new CostCategory { TenantId = tenantId, CreatedAt = _time.GetUtcNow() };
        await ApplyAsync(tenantId, category, request, cancellationToken);
        await entries.AddCategoryAsync(category, cancellationToken);
        audit.Record(AuditAction.CostCategoryChanged, nameof(CostCategory), category.Id, $"نوع هزینهٔ «{category.Name}» ساخته شد.", category.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await RequireAsync(tenantId, category.Id, cancellationToken);
    }

    public async Task<CostCategoryDto> UpdateAsync(
        Guid tenantId, Guid categoryId, SaveCostCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var category = await entries.GetCategoryTrackedAsync(tenantId, categoryId, cancellationToken)
                       ?? throw new KeyNotFoundException("نوع هزینه یافت نشد.");
        if (request.Version is not { } version)
            throw AppValidationException.ForField("version", Messages.Required.Format(("label", "نسخه")));
        concurrency.Expect(category, version);

        await ApplyAsync(tenantId, category, request, cancellationToken);
        category.UpdatedAt = _time.GetUtcNow();
        audit.Record(AuditAction.CostCategoryChanged, nameof(CostCategory), category.Id, $"نوع هزینهٔ «{category.Name}» ویرایش شد.", category.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await RequireAsync(tenantId, category.Id, cancellationToken);
    }

    public async Task<DeleteCostCategoryResultDto> DeleteAsync(
        Guid tenantId, Guid categoryId, uint? version, CancellationToken cancellationToken = default)
    {
        var category = await entries.GetCategoryTrackedAsync(tenantId, categoryId, cancellationToken)
                       ?? throw new KeyNotFoundException("نوع هزینه یافت نشد.");
        if (version is { } v)
            concurrency.Expect(category, v);

        if (await entries.CategoryUsedAsync(tenantId, categoryId, cancellationToken))
        {
            category.IsActive = false;
            category.UpdatedAt = _time.GetUtcNow();
            audit.Record(AuditAction.CostCategoryChanged, nameof(CostCategory), category.Id,
                $"نوع هزینهٔ «{category.Name}» غیرفعال شد (به کار رفته بود).", category.Name);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new DeleteCostCategoryResultDto(false, true);
        }

        entries.RemoveCategory(category);
        audit.Record(AuditAction.CostCategoryChanged, nameof(CostCategory), category.Id, $"نوع هزینهٔ «{category.Name}» حذف شد.", category.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeleteCostCategoryResultDto(true, false);
    }

    /// <summary>
    /// پیش‌فرض‌های نوع تازه از گروه (I.2 quick): هزینه «کل، به نسبت متراژ، اندازهٔ مؤثر»؛ خرید «کل، به تعداد، اندازهٔ اول».
    /// </summary>
    public static CostCategory Defaults(Guid tenantId, string name, CostGroup group) => new()
    {
        TenantId = tenantId,
        Name = name,
        Group = group,
        Icon = group == CostGroup.Purchase ? DefaultPurchaseIcon : DefaultExpenseIcon,
        IsActive = true,
        DefaultBasis = CostBasis.Total,
        DefaultMethod = group == CostGroup.Purchase ? AllocationMethod.ByCount : AllocationMethod.ByArea,
        DefaultAreaBasis = group == CostGroup.Purchase ? AreaBasis.Intake : AreaBasis.Effective,
        DefaultScope = null,
        ShowInIntake = false
    };

    private async Task ApplyAsync(Guid tenantId, CostCategory category, SaveCostCategoryRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var name = CostEntryService.Clean(request.Name);
        if (name is null)
            errors["name"] = [Messages.Required.Format(("label", "نام"))];
        else if (name.Length > CostCategory.NameMaxLength)
            errors["name"] = [Messages.TooLong.Format(("label", "نام"), ("n", CostCategory.NameMaxLength))];
        if (!Enum.IsDefined(request.Group))
            errors["group"] = [Messages.InvalidValue.Format(("label", "گروه"))];
        if (!Enum.IsDefined(request.DefaultBasis) || (request.DefaultBasis == CostBasis.Percent && request.Group != CostGroup.Purchase))
            errors["defaultBasis"] = [Messages.CostPercentBasis.Text];
        if (!Enum.IsDefined(request.DefaultMethod)
            || (request.Group == CostGroup.Purchase && request.DefaultMethod == AllocationMethod.ByPurchaseCost))
            errors["defaultMethod"] = [Messages.CostCircular.Text];
        if (!Enum.IsDefined(request.DefaultAreaBasis))
            errors["defaultAreaBasis"] = [Messages.InvalidValue.Format(("label", "پایهٔ متراژ"))];
        if (request.DefaultScope is { } scope && !Enum.IsDefined(scope))
            errors["defaultScope"] = [Messages.InvalidValue.Format(("label", "برای"))];
        if (request.DefaultCurrency is { } currency && !Enum.IsDefined(currency))
            errors["defaultCurrency"] = [Messages.CostCurrencyInvalid.Text];
        if (request.DefaultRate is { } rate && (Math.Abs(rate) > CostEntryService.MaxRate || !Num.HasAtMostDecimals(rate, 4)))
            errors["defaultRate"] = [Messages.CostTooLarge.Text];
        var icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        if (icon is { Length: > CostCategory.IconMaxLength })
            errors["icon"] = [Messages.TooLong.Format(("label", "آیکون"), ("n", CostCategory.IconMaxLength))];
        if (request.StepTypeId is { } stepType && !await entries.StepTypeVisibleAsync(tenantId, stepType, cancellationToken))
            throw new KeyNotFoundException("نوع مرحله یافت نشد.");
        if (errors.Count > 0)
            throw new AppValidationException(Messages.ValidationFailed.Text, errors);

        if (await entries.CategoryNameTakenAsync(tenantId, request.Group, name!, category.Id, cancellationToken))
            throw new DomainConflictException(Messages.CostCategoryNameTaken.Format(("category", name!)), "name");

        category.Name = name!;
        category.Group = request.Group;
        category.Icon = icon;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.DefaultBasis = request.DefaultBasis;
        category.DefaultRate = request.DefaultRate;
        category.DefaultCurrency = request.DefaultCurrency;
        category.DefaultMethod = request.DefaultMethod;
        category.DefaultAreaBasis = request.DefaultAreaBasis;
        category.DefaultScope = request.DefaultScope;
        category.ShowInIntake = request.ShowInIntake;
        category.StepTypeId = request.StepTypeId;
    }

    private static string ValidName(string? value)
    {
        var name = CostEntryService.Clean(value);
        if (name is null)
            throw AppValidationException.ForField("name", Messages.Required.Format(("label", "نام")));
        if (name.Length > CostCategory.NameMaxLength)
            throw AppValidationException.ForField("name", Messages.TooLong.Format(("label", "نام"), ("n", CostCategory.NameMaxLength)));
        return name;
    }

    private async Task<CostCategoryDto> RequireAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => (await entries.ListCategoriesAsync(tenantId, null, includeInactive: true, cancellationToken)).FirstOrDefault(c => c.Id == id)
           ?? throw new InvalidOperationException("بارگذاری نوع هزینه بعد از ذخیره ناموفق بود.");
}
