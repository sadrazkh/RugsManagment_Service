using System.Diagnostics;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// قاعده‌های پولی فرش و تقسیم «بزرگ‌ترین باقی‌مانده» (spec D.1، D.3، L.1 «RugCostMath» و ویژگی‌های LR). مقادیر جدول‌ها
/// در <c>math-vectors.json</c> سنجیده می‌شوند (<see cref="CostVectorsTests"/>)؛ این‌جا مرزها، خطاها و ویژگی‌های کلی.
/// </summary>
public class RugCostMathTests
{
    [Fact]
    public void PercentLine_CannotBeComputedAlone()
        => Assert.Throws<ArgumentException>(() => RugCostMath.PurchaseLineAmount(CostBasis.Percent, 2m, 1m, 1, 0));

    [Fact]
    public void PurchaseLineAmounts_RecomputeWhenTheIntakeSizeChanges()
    {
        var lines = new[] { new PurchaseLineInput(CostBasis.PerM2, 9_000_000m), new PurchaseLineInput(CostBasis.Percent, 2m) };
        Assert.Equal(new[] { 39_330_000m, 786_600m }, RugCostMath.PurchaseLineAmounts(lines, 4.37m, 1, 0));
        Assert.Equal(new[] { 39_330_000m + 900_000m, 804_600m }, RugCostMath.PurchaseLineAmounts(lines, 4.47m, 1, 0));
    }

    [Theory]
    [InlineData(CurrencyUnit.Toman, CurrencyUnit.Toman, null, null)]
    [InlineData(CurrencyUnit.Rial, CurrencyUnit.Toman, null, "0.1")]
    [InlineData(CurrencyUnit.Toman, CurrencyUnit.Rial, null, "10")]
    [InlineData(CurrencyUnit.Usd, CurrencyUnit.Toman, "105000.5", "105000.5")]
    public void FxFactor_TomanRialAndUsd(CurrencyUnit from, CurrencyUnit to, string? usdFx, string? expected)
    {
        decimal? Dec(string? s) => s is null ? null : decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Dec(expected), RugCostMath.FxFactor(from, to, Dec(usdFx)));
    }

    [Fact]
    public void FxFactor_UsdWithoutARate_Throws()
        => Assert.Throws<ArgumentException>(() => RugCostMath.FxFactor(CurrencyUnit.Usd, CurrencyUnit.Toman, null));

    [Fact]
    public void FxAppliedPerPiece_NotOnTheLine()
    {
        // D.3: ROUND(7.29 × 105,000.5) × 50 = 38,272,700 — نه ROUND(364.50 × 105,000.5) = 38,272,682
        var line = RugCostMath.EntryLineAmount(CostBasis.PerM2, 45.55m, 0.16m, 50, 0, 105_000.5m);
        Assert.Equal(38_272_700m, line);
        Assert.NotEqual(RugCostMath.BaseAmount(364.50m, 105_000.5m, 0), line);
    }

    [Fact]
    public void EntryPieceAmount_OnlyForPerM2AndPerPiece()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RugCostMath.EntryPieceAmount(CostBasis.Total, 1m, 1m, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RugCostMath.EntryPieceAmount(CostBasis.Percent, 1m, 1m, 0));
    }

    [Fact]
    public void StepPrice_LotRule_IsPerPieceThenTimesQuantity()
    {
        // ROUND(45.55 × 0.16) = 7 برای هر تخته؛ ROUND(45.55 × 8.00) = 364 اگر اول ضرب می‌شد
        Assert.Equal(350m, RugCostMath.StepLinePrice(StepPricingModel.PerSquareMeter, 45.55m, 0.40m, 0.40m, 50, 0));
        Assert.Equal(7m, RugCostMath.StepPiecePrice(StepPricingModel.PerSquareMeter, 45.55m, 0.40m, 0.40m, 0));
    }

    [Fact]
    public void StepPrice_NestedCombinedItem_ActsAsFixed()
    {
        var items = new[] { new StepPricingItem(StepPricingModel.Combined, 100.4m) };
        Assert.Equal(100m, RugCostMath.StepPiecePrice(StepPricingModel.Combined, 0m, 1m, 1m, 0, items));
    }

    [Fact]
    public void StepCost_OfEntities_MatchesTheInputOverload()
    {
        var steps = new[]
        {
            new RugWorkflowStep { Status = WorkflowStepStatus.Completed, CalculatedCost = 1_000m },
            new RugWorkflowStep { Status = WorkflowStepStatus.InProgress, ManualCostOverride = 500m, Adjustment = -100m },
            new RugWorkflowStep { Status = WorkflowStepStatus.Pending, CalculatedCost = 9_999m },
            new RugWorkflowStep { Status = WorkflowStepStatus.Completed, CalculatedCost = 10m, Adjustment = -50m }
        };
        Assert.Equal(1_400m, RugCostMath.StepCost(steps));
        Assert.True(RugCostMath.CountsTowardCost(WorkflowStepStatus.InProgress));
        Assert.False(RugCostMath.CountsTowardCost(WorkflowStepStatus.Skipped));
        Assert.False(RugCostMath.CountsTowardCost(WorkflowStepStatus.Cancelled));
    }

    [Fact]
    public void CostPerPiece_NeedsAPositiveQuantity()
        => Assert.Throws<ArgumentOutOfRangeException>(() => RugCostMath.CostPerPiece(100m, 0, 0));

    [Fact]
    public void Compose_NegativePurchase_StaysNegative()
    {
        var breakdown = RugCostMath.Compose(-50_000m, 0m, 10_000m, 0m, 1m, 1, 0);
        Assert.Equal(-40_000m, breakdown.Total);
        Assert.Equal(-40_000m, breakdown.CostPerM2);
    }

    // ═════════════════════════════════════════════════════════════════════
    // Largest remainder
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Lr_RejectsEmptyMembersDuplicateIdsAndUnroundedTotals()
    {
        Assert.Throws<ArgumentException>(() => LargestRemainder.Split(100m, 0, []));
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => LargestRemainder.Split(100m, 0, [new LrMember(id, 1), new LrMember(id, 2)]));
        Assert.Throws<ArgumentException>(() => LargestRemainder.Split(100.5m, 0, [new LrMember(Guid.NewGuid(), 1)]));
        Assert.Equal(100.5m, LargestRemainder.Split(100.5m, 1, [new LrMember(Guid.NewGuid(), 1)]).Shares.Single());
    }

    [Fact]
    public void Lr_ZeroTotal_GivesZeroShares()
        => Assert.All(LargestRemainder.Split(0m, 0, [new LrMember(Guid.NewGuid(), 3), new LrMember(Guid.NewGuid(), 5)]).Shares,
            s => Assert.Equal(0m, s));

    [Fact]
    public void Lr_TieBreak_UsesTheOrdinalIdString()
    {
        // هر دو وزن ۱، بی شماره: یک واحد اضافه به شناسه‌ای می‌رسد که رشتهٔ کوچکش ordinal کوچک‌تر است
        var a = Guid.Parse("0a000000-0000-0000-0000-000000000000");
        var b = Guid.Parse("a0000000-0000-0000-0000-000000000000");
        var shares = LargestRemainder.Split(1m, 0, [new LrMember(b, 1), new LrMember(a, 1)]).Shares;
        Assert.Equal(new[] { 0m, 1m }, shares);
    }

    /// <summary>
    /// ویژگی‌ها (L.1): ۱٬۰۰۰ مورد تصادفیِ قطعی — جمع دقیق، علامت، |سهم| ≤ |کل|، |سهم| در {⌊دقیق⌋، ⌈دقیق⌉}،
    /// وزن منفی صفر می‌شود، و ترتیب ورودی بی‌اثر است.
    /// </summary>
    [Fact]
    public void Lr_Properties_HoldOnRandomCases()
    {
        var random = new Random(20261004);
        for (var c = 0; c < 1_000; c++)
        {
            var count = random.Next(1, 200);
            var members = Enumerable.Range(0, count)
                .Select(_ => new LrMember(
                    Guid.NewGuid(),
                    random.Next(10) == 0 ? -random.NextInt64(1, 1_000_000_000) : random.NextInt64(0, 1_000_000_000),
                    random.Next(3) == 0 ? null : random.NextInt64(1, 10_000)))
                .ToList();
            var scale = random.Next(0, 3);
            var units = random.NextInt64(-1_000_000_000_000_000, 1_000_000_000_000_000);
            var total = units / Pow10(scale);

            var result = LargestRemainder.Split(total, scale, members);
            Assert.Equal(total, result.Shares.Sum());

            var weights = members.Select(m => Math.Max(0, m.Weight)).ToList();
            var weightSum = weights.Sum(w => (decimal)w);
            Assert.Equal(members.Any(m => m.Weight < 0), result.ClampedNegative);
            Assert.Equal(weightSum == 0, result.ZeroWeightsUsedEqual);

            for (var i = 0; i < count; i++)
            {
                var share = result.Shares[i];
                Assert.True(share == 0 || Math.Sign(share) == Math.Sign(total), $"case {c}: sign");
                Assert.True(Math.Abs(share) <= Math.Abs(total), $"case {c}: bound");

                if (weightSum > 0)
                {
                    // |exact| در واحد 10^-scale؛ سهم یکی از کف یا سقف آن است
                    var exactUnits = (System.Numerics.BigInteger)Math.Abs(units) * weights[i];
                    var floor = exactUnits / (System.Numerics.BigInteger)weightSum;
                    var shareUnits = (System.Numerics.BigInteger)(Math.Abs(share) * Pow10(scale));
                    Assert.True(shareUnits == floor || shareUnits == floor + 1, $"case {c}: floor/ceil");
                }
            }

            var shuffled = members.OrderBy(_ => random.Next()).ToList();
            var again = LargestRemainder.Split(total, scale, shuffled).Shares;
            var byId = shuffled.Select((m, i) => (m.Id, Share: again[i])).ToDictionary(x => x.Id, x => x.Share);
            for (var i = 0; i < count; i++)
                Assert.Equal(result.Shares[i], byId[members[i].Id]);
        }
    }

    [Fact]
    public void Lr_TenThousandMembers_IsFast()
    {
        var random = new Random(7);
        var members = Enumerable.Range(0, 10_000)
            .Select(_ => new LrMember(Guid.NewGuid(), random.NextInt64(0, 1_000_000_000), random.NextInt64(1, 10_000)))
            .ToList();
        LargestRemainder.Split(123_456_789m, 0, members); // گرم کردن JIT

        var watch = Stopwatch.StartNew();
        var result = LargestRemainder.Split(987_654_321_012m, 0, members);
        watch.Stop();

        Assert.Equal(987_654_321_012m, result.Shares.Sum());
        Assert.True(watch.ElapsedMilliseconds < 50, $"{watch.ElapsedMilliseconds} ms");
    }

    private static decimal Pow10(int scale) => scale switch { 0 => 1m, 1 => 10m, 2 => 100m, _ => throw new ArgumentOutOfRangeException(nameof(scale)) };
}
