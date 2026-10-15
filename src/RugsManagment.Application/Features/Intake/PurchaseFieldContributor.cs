using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Intake;

/// <summary>
/// خانه‌های خرید جدول ورود در کاتالوگ فیلدها (E.3، دامنهٔ <c>purchase</c>؛ G.2 «Purchase columns»): برای هر نوع خریدِ فعال
/// با «ستون در جدول ورود» یک فیلد — <c>purchase.price</c>، <c>purchase.broker</c> یا <c>purchase.cat:{Id}</c>. نوع درصدی
/// «percent» است و بقیه «money+unit» (مبلغ با چیپ «هر متر | هر تخته | کل»).
///
/// همه پولی‌اند (<see cref="FieldDefDto.Money"/>)، پس کاتالوگ آن‌ها را به کسی که Business نیست نمی‌دهد و انباردار حتی نام
/// این ستون‌ها را نمی‌بیند (A.5). قاعدهٔ خواندن و نوشتنِ خود خانه (یک سطر از آن نوع، «چند سطر» فقط‌خواندنی، پاک کردن =
/// حذف سطر) در <c>RugFieldWriter</c> و <c>RugGridRowBuilder</c> است؛ این‌جا فقط فهرست فیلدهاست. فقط برای جدول‌ها: قالب سند
/// مبلغ خرید را از جمع‌های فرش (<c>rug.purchaseCost</c>) می‌خواند.
/// </summary>
public sealed class PurchaseFieldContributor(IRepository<CostCategory> categories) : IFieldCatalogContributor
{
    /// <summary>بعد از فیلدهای فرش (۱۰۰) و پیش از مبلغ‌های فرش (۲۰۰).</summary>
    public int Order => 160;

    public async Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Purpose != FieldPurpose.Grid)
            return [];

        var tenantId = request.TenantId;
        var purchase = await categories.ListAsync(
            c => c.TenantId == tenantId && c.Group == CostGroup.Purchase && c.IsActive && c.ShowInIntake, cancellationToken);

        return purchase
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Id)
            .Select(c => new FieldDefDto(
                RugFieldKeys.ForPurchaseCategory(c.Id, c.SeedKey),
                c.Name,
                FieldScope.Purchase,
                c.DefaultBasis == CostBasis.Percent ? FieldDataType.Percent : FieldDataType.MoneyUnit,
                c.DefaultBasis == CostBasis.Percent ? "0.##" : "#,0",
                false,
                true,
                true))
            .ToList();
    }
}

/// <summary>
/// «فروشنده» (<c>batch.seller</c>) در کاتالوگ فیلدهای جدول: ستون چسبان جدول ورود که ورودی هر ردیف را تعیین می‌کند
/// (H.1). پیشنهاد مقدارش از <c>/api/rugs/values?field=batch.seller</c> است.
/// </summary>
public sealed class IntakeFieldContributor : IFieldCatalogContributor
{
    public int Order => 150;

    private static readonly IReadOnlyList<FieldDefDto> Fields =
    [
        new(Grid.GridKeys.Seller, "فروشنده", FieldScope.Rug, FieldDataType.Combo, "", false, false, true)
    ];

    public Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(request.Purpose == FieldPurpose.Grid ? Fields : (IReadOnlyList<FieldDefDto>)[]);
    }
}
