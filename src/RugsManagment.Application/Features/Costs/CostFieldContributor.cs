using RugsManagment.Application.Fields;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// فیلدهای پولیِ «حساب محموله» برای کاتالوگ (spec E.3، D.6؛ <c>Money = true</c>، فقط Business): قالب «حساب محموله»
/// (فاز ۷، نوع سند <c>ShipmentCost</c>) و سرجمع‌های آن از همین کلیدها می‌خوانند. مبلغ‌های هر فرش (خرید، کارگاه، هزینه‌ها، بها،
/// سهم درآمد) از فاز ۲ در <see cref="RugMoneyFieldContributor"/> هستند و این‌جا تکرار نمی‌شوند. هیچ‌کدام نوشتنی نیست.
/// </summary>
public sealed class CostFieldContributor : IFieldCatalogContributor
{
    /// <summary>بعد از فیلدهای محموله (۳۰۰).</summary>
    public int Order => 400;

    public const string ShipmentPurchase = "shipment.purchaseCost";
    public const string ShipmentSteps = "shipment.stepCost";
    public const string ShipmentExpenses = "shipment.expenseCost";
    public const string ShipmentTotalCost = "shipment.totalCost";
    public const string ShipmentCostPerM2 = "shipment.costPerM2";
    public const string ShipmentRevenue = "shipment.revenue";
    public const string ShipmentProfit = "shipment.profit";

    public static IReadOnlyList<FieldDefDto> Fields { get; } =
    [
        new(ShipmentPurchase, "جمع خرید محموله", FieldScope.Shipment, FieldDataType.Money, "#,0", false, true, false),
        new(ShipmentSteps, "جمع کارهای کارگاه محموله", FieldScope.Shipment, FieldDataType.Money, "#,0", false, true, false),
        new(ShipmentExpenses, "جمع هزینه‌های محموله", FieldScope.Shipment, FieldDataType.Money, "#,0", false, true, false),
        new(ShipmentTotalCost, "بهای تمام‌شدهٔ محموله", FieldScope.Shipment, FieldDataType.Money, "#,0", false, true, false),
        new(ShipmentCostPerM2, "قیمت تمام‌شدهٔ هر متر", FieldScope.Shipment, FieldDataType.Money, "#,0", false, true, false),
        new(ShipmentRevenue, "فروش محموله", FieldScope.Shipment, FieldDataType.Money, "#,0", false, true, false),
        new(ShipmentProfit, "سود محموله", FieldScope.Shipment, FieldDataType.Money, "#,0", false, true, false)
    ];

    public Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // فیلد محموله فقط در قالب سند معنا دارد، نه ستون جدول
        return Task.FromResult(request.Purpose == FieldPurpose.Document ? Fields : (IReadOnlyList<FieldDefDto>)[]);
    }
}
