using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// عضوهای پولی فاز ۲ هم به انباردار نمی‌رسند (A.5): سطرهای خرید روی فرش کامل، بها هر متر روی ردیف فهرست (هر دو
/// overload)، ردیفِ داخل «پیدا کردن با شماره» و هر دو نیمهٔ برش دسته. هر redact برای Business همان شیء را برمی‌گرداند.
/// </summary>
public class P2MoneyRedactionTests
{
    private static readonly IMoneyVisibility Operator = MoneyRedactionTests.Operator;
    private static readonly IMoneyVisibility Merchant = MoneyRedactionTests.Merchant;

    private static RugDto RugWithLines() => MoneyRedactionTests.Rug() with
    {
        SerialNo = "3911 - 3960",
        Quantity = 50,
        TotalAreaM2 = 8m,
        PurchaseLines = [new PurchaseLineDto(Guid.NewGuid(), null, "قیمت خرید", CostBasis.PerM2, 45.55m, 350m, 0, null)],
        UsdPerM2 = 45.55m,
        LineUsd = 364.5m
    };

    private static RugListItemDto Item() => new(
        Guid.NewGuid(), "RUG-202610-0001", null, null, null, 0.4m, 0.4m, 0.16m, RugStatus.Draft, null, null, null, null,
        null, 357m, 0, 0, DateTimeOffset.UtcNow, "3911 - 3960", 50, 8m, true, 45m, RugLocation.InStock, null, null, 7);

    [Fact]
    public void Rug_ForOperator_LosesPurchaseLines_ButKeepsSerialQuantityAndDeclaredUsd()
    {
        var redacted = Operator.Redact(RugWithLines());

        Assert.Null(redacted.PurchaseLines);
        Assert.Null(redacted.PurchaseCost);
        Assert.Null(redacted.Costs);
        Assert.Equal(("3911 - 3960", 50, 8m), (redacted.SerialNo, redacted.Quantity, redacted.TotalAreaM2));
        // دلار گمرکی هزینهٔ داخلی نیست (A.5)
        Assert.Equal((45.55m, 364.5m), (redacted.UsdPerM2, redacted.LineUsd));
    }

    [Fact]
    public void ListRow_ForOperator_LosesCostPerM2_InBothOverloads()
    {
        var item = Item();

        var single = Operator.Redact(item);
        Assert.Equal(((decimal?)null, (decimal?)null), (single.TotalInvestment, single.CostPerM2));
        Assert.True(single.CostComputed);

        var paged = Assert.Single(Operator.Redact(new PagedResult<RugListItemDto>([item], 1, 1, 25)).Items);
        Assert.Equal(((decimal?)null, (decimal?)null), (paged.TotalInvestment, paged.CostPerM2));

        Assert.Same(item, Merchant.Redact(item));
        Assert.Equal(45m, Assert.Single(Merchant.Redact(new PagedResult<RugListItemDto>([item], 1, 1, 25)).Items).CostPerM2);
    }

    [Fact]
    public void BySerialHit_ForOperator_HasNoMoney_ButKeepsTheMatch()
    {
        var hit = new RugBySerialDto(Item(), RugLocation.InStock, SerialMatchType.InRange);

        var redacted = Operator.Redact(hit);

        Assert.Null(redacted.Rug.TotalInvestment);
        Assert.Null(redacted.Rug.CostPerM2);
        Assert.Equal((RugLocation.InStock, SerialMatchType.InRange, "3911 - 3960"),
            (redacted.Location, redacted.MatchType, redacted.Rug.SerialNo));
        Assert.Same(hit, Merchant.Redact(hit));
    }

    [Fact]
    public void SplitResult_ForOperator_RedactsBothHalves()
    {
        var result = new SplitRugResultDto(RugWithLines(), RugWithLines() with { SerialNo = "3941 - 3960", Quantity = 20 });

        var redacted = Operator.Redact(result);

        Assert.All(new[] { redacted.Original, redacted.Created }, rug =>
        {
            Assert.Null(rug.PurchaseLines);
            Assert.Null(rug.PurchaseCost);
            Assert.Null(rug.TargetSalePrice);
            Assert.Null(rug.Costs);
            Assert.Null(Assert.Single(rug.WorkflowSteps).EffectiveCost);
        });
        Assert.Equal("3941 - 3960", redacted.Created.SerialNo);
        Assert.Same(result, Merchant.Redact(result));
    }
}
