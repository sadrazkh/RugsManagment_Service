using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Costs;

/// <summary>یک نوع هزینه (I.3) — کاشی‌های پنل هزینه و صفحهٔ تنظیم «نوع‌های هزینه».</summary>
public record CostCategoryDto(
    Guid Id,
    string Name,
    CostGroup Group,
    string? Icon,
    int SortOrder,
    bool IsActive,
    CostBasis DefaultBasis,
    decimal? DefaultRate,
    CurrencyUnit? DefaultCurrency,
    AllocationMethod DefaultMethod,
    AreaBasis DefaultAreaBasis,
    CostScope? DefaultScope,
    bool ShowInIntake,
    Guid? StepTypeId,
    string? SeedKey,
    uint Version);

/// <summary>
/// «+ نوع تازه» از پنل هزینه (<c>POST /api/cost-categories/quick</c>، Business): فقط نام و گروه؛ پیش‌فرض‌ها از گروه
/// (هزینه: کل، به نسبت متراژ، اندازهٔ مؤثر؛ خرید: کل، به تعداد، اندازهٔ اول).
/// </summary>
public record QuickCostCategoryRequest(string Name, CostGroup Group);

/// <summary>ساخت یا ویرایش نوع هزینه در تنظیمات (<c>POST/PUT /api/cost-categories[/{id}]</c>، Configure).</summary>
/// <param name="Version">نسخه برای PUT؛ در POST نادیده.</param>
public record SaveCostCategoryRequest(
    string Name,
    CostGroup Group,
    string? Icon,
    int SortOrder,
    bool IsActive,
    CostBasis DefaultBasis,
    decimal? DefaultRate,
    CurrencyUnit? DefaultCurrency,
    AllocationMethod DefaultMethod,
    AreaBasis DefaultAreaBasis,
    CostScope? DefaultScope,
    bool ShowInIntake,
    Guid? StepTypeId,
    uint? Version);

/// <summary>نتیجهٔ <c>DELETE /api/cost-categories/{id}</c>: نوعِ به کار رفته حذف نمی‌شود و فقط غیرفعال می‌شود (B.13).</summary>
/// <param name="Deleted">واقعاً حذف شد.</param>
/// <param name="Deactivated">به کار رفته بود و غیرفعال شد.</param>
public record DeleteCostCategoryResultDto(bool Deleted, bool Deactivated);
