namespace RugsManagment.Application.Fields;

/// <summary>
/// مبلغ‌های فرش برای کاتالوگ (E.3، دامنهٔ <c>rug</c> با <see cref="FieldDefDto.Money"/> = true): همه از
/// <c>RugCostTotal</c> (پاس سازگاری) خوانده می‌شوند و جز «قیمت فروش هدف» نوشتنی نیستند. کاتالوگ این‌ها را برای کسی که
/// Business نیست برنمی‌گرداند (<see cref="FieldCatalog"/>)، پس قالب سندی که یکی از این‌ها را دارد «قالب پولی» است.
/// </summary>
public sealed class RugMoneyFieldContributor : IFieldCatalogContributor
{
    public int Order => 200;

    public static IReadOnlyList<FieldDefDto> Fields { get; } =
    [
        new(RugFieldKeys.PurchaseTotal, "جمع خرید", FieldScope.Rug, FieldDataType.Money, "#,0", true, true, false),
        new(RugFieldKeys.PurchaseCost, "قیمت خرید", FieldScope.Rug, FieldDataType.Money, "#,0", true, true, false),
        new(RugFieldKeys.StepCost, "کارهای کارگاه", FieldScope.Rug, FieldDataType.Money, "#,0", true, true, false),
        new(RugFieldKeys.ExpenseCost, "هزینه‌ها", FieldScope.Rug, FieldDataType.Money, "#,0", true, true, false),
        new(RugFieldKeys.TotalCost, "بهای تمام‌شده", FieldScope.Rug, FieldDataType.Money, "#,0", true, true, false),
        new(RugFieldKeys.CostPerM2, "بها هر متر", FieldScope.Rug, FieldDataType.Money, "#,0", false, true, false),
        new(RugFieldKeys.CostPerPiece, "بها هر تخته", FieldScope.Rug, FieldDataType.Money, "#,0", false, true, false),
        new(RugFieldKeys.RevenueShare, "سهم درآمد صادرات", FieldScope.Rug, FieldDataType.Money, "#,0", true, true, false),
        new(RugFieldKeys.TargetSalePrice, "قیمت فروش هدف", FieldScope.Rug, FieldDataType.Money, "#,0", true, true, true)
    ];

    public Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Fields);
}
