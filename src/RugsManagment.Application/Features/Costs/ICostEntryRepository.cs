using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// خواندن و بارگذاری دفتر «هزینه‌ها» برای سرویس‌ها (پیاده‌سازی: <c>Infrastructure/Persistence/Repositories/CostEntryRepository</c>).
///
/// قرارداد: هر متد کارگاه را می‌گیرد و بیرون از آن چیزی نمی‌بیند (شناسهٔ کارگاه دیگر = «پیدا نشد» → ۴۰۴)؛ بارگذاری
/// <c>…TrackedAsync</c> موجودیتِ ردیابی‌شده برمی‌گرداند تا تغییرش از پاس سازگاری بگذرد؛ بقیه بی‌ردیابی‌اند و هرگز چیزی
/// نمی‌نویسند. فرزند تازه (هدف، مبلغ ثابت) با <c>Add…Async</c> افزوده می‌شود، نه با افزودن به مجموعهٔ ناوبری (B.1).
/// </summary>
public interface ICostEntryRepository
{
    // ── نوشتن ────────────────────────────────────────────────────────────

    /// <summary>ثبت با هدف‌ها و مبلغ‌های ثابتش (ردیابی‌شده)، حذف‌شده هم؛ null یعنی در این کارگاه نیست.</summary>
    Task<CostEntry?> GetTrackedAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default);

    Task AddAsync(CostEntry entry, CancellationToken cancellationToken = default);

    Task AddTargetAsync(CostEntryTarget target, CancellationToken cancellationToken = default);

    Task AddPinAsync(CostEntryPin pin, CancellationToken cancellationToken = default);

    void RemoveTarget(CostEntryTarget target);

    void RemovePin(CostEntryPin pin);

    /// <summary>نوع هزینه (ردیابی‌شده) برای تنظیمات.</summary>
    Task<CostCategory?> GetCategoryTrackedAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default);

    Task AddCategoryAsync(CostCategory category, CancellationToken cancellationToken = default);

    void RemoveCategory(CostCategory category);

    // ── خواندن ───────────────────────────────────────────────────────────

    /// <summary>ثبتی که با این کلید کلاینت ساخته شد (ساخت تکرارناپذیر)؛ null یعنی نیست.</summary>
    Task<Guid?> FindByClientKeyAsync(Guid tenantId, Guid clientKey, CancellationToken cancellationToken = default);

    /// <summary>نوع هزینه (بی‌ردیابی)؛ null یعنی در این کارگاه نیست.</summary>
    Task<CostCategory?> GetCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>نوع‌های هزینهٔ کارگاه به ترتیب گروه، ترتیب و نام، با نسخه.</summary>
    Task<IReadOnlyList<CostCategoryDto>> ListCategoriesAsync(
        Guid tenantId, CostGroup? group, bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>آیا نوع هزینه به کار رفته (ثبت هزینه، حذف‌شده هم، یا سطر خرید، سطل زباله هم).</summary>
    Task<bool> CategoryUsedAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>آیا نوعی با همین نام در همین گروه هست (جز خودش).</summary>
    Task<bool> CategoryNameTakenAsync(Guid tenantId, CostGroup group, string name, Guid? exceptId, CancellationToken cancellationToken = default);

    /// <summary>بیشترین ترتیب نوع‌ها (برای «+ نوع تازه» در انتها).</summary>
    Task<int> MaxCategorySortAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>نام طرف خدمات این کارگاه؛ null یعنی نیست.</summary>
    Task<string?> ProviderNameAsync(Guid tenantId, Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>آیا نوع مرحله برای این کارگاه (سیستمی یا خودش) هست.</summary>
    Task<bool> StepTypeVisibleAsync(Guid tenantId, Guid stepTypeId, CancellationToken cancellationToken = default);

    /// <summary>محمولهٔ این کارگاه؛ null یعنی نیست.</summary>
    Task<CostShipmentRef?> GetShipmentAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>ورودی این کارگاه؛ null یعنی نیست.</summary>
    Task<CostBatchRef?> GetBatchAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>نگله‌های این کارگاه از میان این شناسه‌ها (نبودِ هر کدام یعنی کارگاه دیگر یا حذف‌شده).</summary>
    Task<IReadOnlyList<CostBaleRef>> GetBalesAsync(Guid tenantId, IReadOnlyCollection<Guid> baleIds, CancellationToken cancellationToken = default);

    /// <summary>فرش‌های این کارگاه از میان این شناسه‌ها (سطل زباله هم) با شماره.</summary>
    Task<IReadOnlyList<CostRugRef>> GetRugsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default);

    /// <summary>فرش‌های فعالِ یک ورودی (برای عکس «خرید یکجای این ورودی»).</summary>
    Task<IReadOnlyList<Guid>> BatchMemberIdsAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// اعضای فعلی یک دامنه با هر چه تقسیم لازم دارد (پیش‌نمایش، انجماد رسید): همان قاعدهٔ عضویت مرحله‌های پاس (D.2)؛
    /// فرش سطل زباله نیست. <paramref name="createdAt"/> برای «هزینهٔ دیرهنگام» (محمولهٔ قطعی‌شده پیش از آن).
    /// </summary>
    Task<IReadOnlyList<AllocationMember>> MembersAsync(
        Guid tenantId, CostScopeSpec scope, Guid? stepTypeId, DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    /// <summary>شمارهٔ فرش‌های مبلغ‌های ثابت (برای پیام «مبلغ ثابتِ فرش … حساب نشد»).</summary>
    Task<IReadOnlyDictionary<Guid, string?>> SerialsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default);

    /// <summary>یک ثبت کامل برای DTO (حذف‌شده هم)؛ null یعنی در این کارگاه نیست.</summary>
    Task<CostEntryDetail?> GetDetailAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>فهرست صفحه‌بندی‌شده، تازه‌ترین تاریخ و بعد بزرگ‌ترین شماره اول.</summary>
    Task<PagedResult<CostEntryListRow>> ListAsync(Guid tenantId, CostEntryQuery query, CancellationToken cancellationToken = default);

    /// <summary>سهم یک فرش از ثبت‌های هزینهٔ حذف‌نشده (ریز هزینهٔ فرش).</summary>
    Task<IReadOnlyList<AllocationLineDto>> AllocationsOfRugAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);

    /// <summary>
    /// «همهٔ انبار» برای resolve-targets: فرش‌های فعالِ نه فروخته و نه در هیچ نگله، به ترتیب شماره.
    /// </summary>
    Task<IReadOnlyList<CostRugRef>> InStockRugsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>فرش‌های فعالی که شماره‌شان این شماره یا بازه را دارد (یکی یا همپوشان)، به ترتیب شماره.</summary>
    Task<IReadOnlyList<CostRugRef>> RugsBySerialAsync(
        Guid tenantId, string? exactText, int? from, int? to, CancellationToken cancellationToken = default);

    /// <summary>ثبت‌های حذف‌نشدهٔ «تقسیم‌نشده» یا ناقص (کارت خانه).</summary>
    Task<int> CountNotFullyAllocatedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>ردیف‌های «حساب محموله»: هر خط با بهای فرشش.</summary>
    Task<ShipmentMoneyData?> ShipmentMoneyAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>ارز، تعداد رقم اعشار و واژهٔ نگلهٔ کارگاه (بی ردیف ترجیحات: تومان، ۰، نگله).</summary>
    Task<CostTenantSettings> TenantCostSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>دامنهٔ یک ثبت (برای پیدا کردن اعضای فعلی).</summary>
/// <param name="Scope">انتخاب، نگله، محموله یا ورودی.</param>
/// <param name="Group">گروه نوع هزینه (ورودی خرید عکس است، ورودی هزینه زنده).</param>
/// <param name="RugIds">فرش‌های صریح (انتخاب، یا عکس ورودی خرید).</param>
/// <param name="BaleIds">نگله‌ها.</param>
/// <param name="ShipmentId">محموله.</param>
/// <param name="BatchId">ورودی.</param>
public sealed record CostScopeSpec(
    CostScope Scope, CostGroup Group, IReadOnlyCollection<Guid> RugIds, IReadOnlyCollection<Guid> BaleIds, Guid? ShipmentId, Guid? BatchId);

/// <summary>محموله برای اعتبارسنجی و متن «برای».</summary>
public sealed record CostShipmentRef(Guid Id, int Number, ShipmentStatus Status, DateTimeOffset? FinalizedAt);

/// <summary>ورودی برای اعتبارسنجی و متن «برای».</summary>
public sealed record CostBatchRef(Guid Id, int Number, string? SellerName);

/// <summary>نگله برای اعتبارسنجی و متن «برای».</summary>
public sealed record CostBaleRef(Guid Id, Guid ShipmentId, int Number);

/// <summary>فرش برای اعتبارسنجی، شماره و جمع‌ها.</summary>
public sealed record CostRugRef(Guid Id, string Sku, string? SerialNo, long? SerialSort, int Quantity, decimal TotalAreaM2, bool Deleted);

/// <summary>یک ثبت کامل با نتیجه و سهم‌ها.</summary>
public sealed record CostEntryDetail(
    CostEntry Entry,
    string CategoryName,
    string? CategoryIcon,
    CostGroup Group,
    string? ServiceProviderName,
    uint Version,
    IReadOnlyList<Guid> TargetRugIds,
    IReadOnlyList<Guid> TargetBaleIds,
    IReadOnlyList<PinDto> Pins,
    CostEntryResult? Result,
    IReadOnlyList<CostShareRow> Shares);

/// <summary>یک سهم ذخیره‌شده با شماره و تعداد فرش.</summary>
public sealed record CostShareRow(Guid RugId, string Sku, string? SerialNo, long? SerialSort, int Quantity, decimal Amount, long WeightUnits, bool IsPinned);

/// <summary>یک ردیف خام فهرست «هزینه‌ها».</summary>
public sealed record CostEntryListRow(
    Guid Id, int Number, DateOnly Date, string Category, string? CategoryIcon, CostGroup Group, string? Description,
    CostScope Scope, CostBasis Basis, decimal? Amount, CurrencyUnit Currency, decimal BaseAmount, DateTimeOffset? FrozenAt,
    bool Deleted, uint Version, string? ScopeNote,
    int? ShipmentNumber, int? BatchNumber, IReadOnlyList<int> BaleNumbers, int TargetRugCount,
    CostEntryResult? Result);

/// <summary>داده‌های «حساب محموله».</summary>
public sealed record ShipmentMoneyData(
    Guid Id, int Number, ShipmentStatus Status, uint Version,
    decimal? RevenueAmount, CurrencyUnit? RevenueCurrency, decimal? RevenueFx,
    IReadOnlyList<ShipmentMoneyLine> Lines, int UnallocatedEntries);

/// <summary>یک خط محموله با بهای فرشش (از <c>RugCostTotal</c>).</summary>
public sealed record ShipmentMoneyLine(
    Guid RugId, string? SerialNo, string Sku, long? SerialSort, int Quantity, decimal LineAreaM2, decimal? LineUsd,
    decimal Purchase, decimal Steps, decimal Expenses, decimal Total);
