using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// <see cref="RugStatusResolver"/> (spec C.10، L.1: فروخته‌شده &gt; صادر شده &gt; گردش کار) و <see cref="PurchaseMath"/>
/// (B.6، I.3، L.1: هر متر روی اندازهٔ اول و برای یک تخته، درصد یک بار، قاعدهٔ مسیر قدیمی).
/// </summary>
public class RugStatusAndPurchaseMathTests
{
    // ── وضعیت ──

    [Theory]
    [InlineData(true, true, RugStatus.InProgress, RugStatus.Sold)]
    [InlineData(true, false, RugStatus.ReadyForSale, RugStatus.Sold)]
    [InlineData(false, true, RugStatus.ReadyForSale, RugStatus.Exported)]
    [InlineData(false, false, RugStatus.ReadyForSale, RugStatus.ReadyForSale)]
    [InlineData(false, false, RugStatus.Draft, RugStatus.Draft)]
    public void Resolve_SoldBeatsExportedBeatsWorkflow(bool hasSale, bool exported, RugStatus workflow, RugStatus expected)
        => Assert.Equal(expected, RugStatusResolver.Resolve(hasSale, exported, workflow));

    [Fact]
    public void ApplyWorkflowStatus_NeverOverwritesExported()
    {
        var rug = new Rug { Status = RugStatus.Exported };
        RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.InProgress);
        Assert.Equal(RugStatus.Exported, rug.Status);
    }

    [Fact]
    public void ApplyWorkflowStatus_ASoldStatusWithoutASaleIsRepaired()
    {
        // دادهٔ قدیمی (فرش نمونهٔ seed): «فروخته‌شده» بدون فروش
        var rug = new Rug { Status = RugStatus.Sold };
        RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.ReadyForSale);
        Assert.Equal(RugStatus.ReadyForSale, rug.Status);
    }

    // ── سطرهای خرید ──

    private static Rug Rug(decimal length, decimal width, int quantity = 1)
    {
        var rug = new Rug();
        rug.SetIntakeSize(length, width);
        rug.SetQuantity(quantity);
        return rug;
    }

    private static RugPurchaseLine Line(CostBasis basis, decimal rate, decimal amount = 0) => new() { Basis = basis, Rate = rate, Amount = amount };

    [Fact]
    public void PerM2_UsesTheIntakeArea_EvenWithAFinalSize()
    {
        // D.7 C: ۸٬۵۰۰٬۰۰۰ × ۸٫۴۴ = ۷۱٬۷۴۰٬۰۰۰؛ اندازهٔ نهایی ۳٫۳۰×۲٫۵۰ قیمت خرید را عوض نمی‌کند (M.2 #12)
        var rug = Rug(3.35m, 2.52m);
        rug.SetFinalSize(3.30m, 2.50m, DateTimeOffset.UtcNow, null);
        var lines = new[] { Line(CostBasis.PerM2, 8_500_000m), Line(CostBasis.Percent, 2m) };

        Assert.Equal([71_740_000m, 1_434_800m], PurchaseMath.Amounts(rug, lines, 0));
        Assert.Equal(73_174_800m, PurchaseMath.Total(rug, lines, 0));
    }

    [Fact]
    public void Lot_PerM2RoundsPerPiece_PercentOnceOnTheSum()
    {
        // ۴۵٫۵۵ × ۰٫۱۶ = ۷٫۲۸۸ → ۷ × ۵۰ = ۳۵۰ (رقم ۰) و ۷٫۲۹ × ۵۰ = ۳۶۴٫۵۰ (رقم ۲)
        var rug = Rug(0.40m, 0.40m, 50);
        Assert.Equal([350m], PurchaseMath.Amounts(rug, [Line(CostBasis.PerM2, 45.55m)], 0));
        Assert.Equal([364.50m], PurchaseMath.Amounts(rug, [Line(CostBasis.PerM2, 45.55m)], 2));

        // D.7 B: هر تخته ۳۰۰٬۰۰۰ × ۵۰ و دلالی ۲٪ یک بار روی جمع
        Assert.Equal([15_000_000m, 300_000m],
            PurchaseMath.Amounts(rug, [Line(CostBasis.PerPiece, 300_000m), Line(CostBasis.Percent, 2m)], 0));
    }

    [Fact]
    public void SizeChange_MovesThePerM2Line()
    {
        var rug = Rug(2.00m, 1.00m);
        var lines = new[] { Line(CostBasis.PerM2, 1_000m) };
        Assert.Equal(2_000m, PurchaseMath.Total(rug, lines, 0));

        rug.SetIntakeSize(3.00m, 1.00m);
        Assert.Equal(3_000m, PurchaseMath.Total(rug, lines, 0));
    }

    [Fact]
    public void NegativeTotalLine_IsKept()
        => Assert.Equal(-500m, PurchaseMath.Total(Rug(1m, 1m), [Line(CostBasis.Total, 1_000m), Line(CostBasis.Total, -1_500m)], 0));

    [Fact]
    public void LegacyPurchaseCost_NeverCollapsesAMultiLinePurchase()
    {
        Assert.Equal(LegacyPurchaseAction.CreateLine, PurchaseMath.ClassifyLegacyPurchaseCost([], 1_000m));
        Assert.Equal(LegacyPurchaseAction.UpdateRate,
            PurchaseMath.ClassifyLegacyPurchaseCost([Line(CostBasis.Total, 900m, 900m)], 1_000m));

        var multi = new[] { Line(CostBasis.PerM2, 9_000_000m, 39_330_000m), Line(CostBasis.Percent, 2m, 786_600m) };
        Assert.Equal(LegacyPurchaseAction.Ignore, PurchaseMath.ClassifyLegacyPurchaseCost(multi, 40_116_600m));
        Assert.Equal(LegacyPurchaseAction.Conflict, PurchaseMath.ClassifyLegacyPurchaseCost(multi, 40_000_000m));

        // یک سطر که «کل» نیست هم چندسطری حساب می‌شود (نرخش متری است، نه مبلغ)
        Assert.Equal(LegacyPurchaseAction.Conflict,
            PurchaseMath.ClassifyLegacyPurchaseCost([Line(CostBasis.PerM2, 1m, 4m)], 5m));
    }
}
