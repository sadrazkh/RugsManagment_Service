namespace RugsManagment.Application.Fields;

/// <summary>
/// کلیدهای فیلد فرش — یک جا، تا کاتالوگ (<see cref="RugFieldContributor"/>)، نویسندهٔ فیلدها
/// (<c>RugFieldWriter</c>)، جدول و قالب‌های سند همان رشته را به کار ببرند و غلط تایپی یک‌جا شکسته شود.
/// </summary>
public static class RugFieldKeys
{
    // ── نوشتنی ───────────────────────────────────────────────────────────
    public const string Serial = "rug.serial";
    public const string Quantity = "rug.qty";
    public const string Title = "rug.title";
    public const string Origin = "rug.origin";
    public const string Pattern = "rug.pattern";
    public const string Material = "rug.material";
    public const string KnotDensity = "rug.knotDensity";
    public const string IntakeLength = "rug.intakeLength";
    public const string IntakeWidth = "rug.intakeWidth";
    public const string FinalLength = "rug.finalLength";
    public const string FinalWidth = "rug.finalWidth";
    public const string Notes = "rug.notes";

    /// <summary>پیشوند فیلدهای سفارشی: <c>rug.custom.{CustomFieldDefinition.Key}</c>.</summary>
    public const string CustomPrefix = "rug.custom.";

    // ── خواندنی ──────────────────────────────────────────────────────────
    public const string Sku = "rug.sku";
    public const string AreaM2 = "rug.areaM2";
    public const string TotalAreaM2 = "rug.totalAreaM2";
    public const string IntakeAreaM2 = "rug.intakeAreaM2";

    // ── پول (فقط Business) ───────────────────────────────────────────────

    /// <summary>قیمت فروش هدف — تنها کلید پولیِ نوشتنیِ خودِ فرش.</summary>
    public const string TargetSalePrice = "rug.targetSalePrice";

    /// <summary>جمع سطرهای خرید خودِ فرش (جدول ورود «جمع خرید»؛ خواندنی).</summary>
    public const string PurchaseTotal = "rug.purchaseTotal";

    public const string PurchaseCost = "rug.purchaseCost";
    public const string StepCost = "rug.stepCost";
    public const string ExpenseCost = "rug.expenseCost";
    public const string TotalCost = "rug.totalCost";
    public const string CostPerM2 = "rug.costPerM2";
    public const string CostPerPiece = "rug.costPerPiece";
    public const string RevenueShare = "rug.revenueShare";

    /// <summary>
    /// پیشوند خانه‌های خرید جدول ورود (G.2، دامنهٔ <c>purchase</c>): <c>SeedKey</c> نوع خرید
    /// (<c>purchase.price</c>، <c>purchase.broker</c>) یا <c>purchase.cat:{Id}</c> برای نوعی که بی SeedKey ساخته شد.
    /// </summary>
    public const string PurchasePrefix = "purchase.";

    /// <summary>شکل کلید خرید برای نوعی که SeedKey ندارد.</summary>
    public const string PurchaseCategoryIdPrefix = "purchase.cat:";

    /// <summary>کلید خانهٔ جدول برای یک نوع خرید.</summary>
    public static string ForPurchaseCategory(Guid categoryId, string? seedKey)
        => !string.IsNullOrEmpty(seedKey) && seedKey.StartsWith(PurchasePrefix, StringComparison.Ordinal)
            ? seedKey
            : PurchaseCategoryIdPrefix + categoryId.ToString("D");

    /// <summary>کلید خانهٔ یک فیلد سفارشی.</summary>
    public static string ForCustomField(string customKey) => CustomPrefix + customKey;

    /// <summary>کلیدهای پولیِ خودِ فرش (به‌جز خانه‌های <see cref="PurchasePrefix"/>).</summary>
    public static readonly IReadOnlySet<string> MoneyKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        TargetSalePrice, PurchaseTotal, PurchaseCost, StepCost, ExpenseCost, TotalCost, CostPerM2, CostPerPiece,
        RevenueShare
    };

    /// <summary>
    /// آیا کلید پولی است: هر کلید <see cref="MoneyKeys"/> یا هر خانهٔ خرید. کلید پولی از کاربری که Business نیست کل
    /// درخواست را ۴۰۳ می‌کند (A.5، G.6 #5) — حتی با مقدار null، چون پاک کردن قیمت خرید هم نوشتن پول است.
    /// </summary>
    public static bool IsMoney(string key)
        => MoneyKeys.Contains(key) || key.StartsWith(PurchasePrefix, StringComparison.Ordinal);

    /// <summary>
    /// کلید فیلدی که خطای دامنه برمی‌گرداند به کلید کاتالوگ (<c>rug.quantity</c> → <c>rug.qty</c>)، تا جزیره خطا را
    /// زیر همان خانه نشان دهد. بقیهٔ کلیدهای دامنه (<c>rug.serial</c>، <c>rug.intakeLength</c>، …) همان کلید کاتالوگ‌اند.
    /// </summary>
    public static string FromDomainField(string field) => field == "rug.quantity" ? Quantity : field;
}
