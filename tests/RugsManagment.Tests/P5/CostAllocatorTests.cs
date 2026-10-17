using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Application.Features.Costs.Ledger;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests.P5;

public sealed class CostAllocatorTests
{
    [Fact]
    public void SharedD7AllocationVectors_AreExact()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "math-vectors.json")));
        foreach (var row in fixture.RootElement.GetProperty("costAllocationVectors").EnumerateArray())
        {
            var weights = row.GetProperty("weights").EnumerateArray().Select(w => w.GetInt64()).ToArray();
            var members = weights.Select((weight, i) => new LrMember(new Guid(i + 1, 0, 0, new byte[8]), weight, i)).ToArray();
            var total = decimal.Parse(row.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);
            var expected = row.GetProperty("shares").EnumerateArray().Select(s => decimal.Parse(s.GetString()!, CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(expected, LargestRemainder.Split(total, 0, members).Shares);
        }
    }
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static AllocationMember Member(Guid id, decimal area, int qty = 1, decimal? final = null, decimal? usd = null, decimal purchase = 0m)
        => new(id, id.ToString(), id == A ? 3778 : id == B ? 3911 : 3782, qty, area, final ?? area, usd, purchase, false, false, false);
    private static AllocationEntry Total(decimal amount, AllocationMethod method = AllocationMethod.ByArea, AreaBasis basis = AreaBasis.Effective)
        => new(CostGroup.Expense, CostBasis.Total, null, amount, CurrencyUnit.Toman, null, amount, false, basis, method);
    private static AllocationResult Allocate(AllocationEntry entry, IReadOnlyList<AllocationMember> members, params AllocationPin[] pins)
        => CostAllocator.Allocate(entry, members, pins, CurrencyUnit.Toman, 0);
    private static void Shares(AllocationResult result, params (Guid Id, decimal Value)[] values)
    {
        Assert.Equal(values.Length, result.Shares.Count);
        foreach (var (id, value) in values) Assert.Equal(value, result.Shares.Single(s => s.RugId == id).Amount);
        Assert.Equal(result.BaseAmount, result.Allocated + result.Unallocated);
    }

    [Fact]
    public void D7_AllTwelveWorkedSteps_UseThePublishedExactIntegers()
    {
        var members = new[] { Member(A, 4.37m), Member(B, .16m, 50), Member(C, 8.44m) };
        Assert.Equal(20.81m, members.Sum(m => m.IntakePieceArea * m.Quantity));
        Assert.Equal(52, members.Sum(m => m.Quantity));
        var purchases = new[]
        {
            RugCostMath.PurchaseLineAmount(CostBasis.PerM2, 9_000_000m, 4.37m, 1, 0),
            RugCostMath.PurchaseLineAmount(CostBasis.PerPiece, 300_000m, .16m, 50, 0),
            RugCostMath.PurchaseLineAmount(CostBasis.PerM2, 8_500_000m, 8.44m, 1, 0)
        };
        Assert.Equal(new[] { 39_330_000m, 15_000_000m, 71_740_000m }, purchases);
        purchases = purchases.Select(p => p + RugCostMath.PercentLineAmount(2, p, 0)).ToArray();
        Assert.Equal(new[] { 40_116_600m, 15_300_000m, 73_174_800m }, purchases);
        var washing = new AllocationEntry(CostGroup.Expense, CostBasis.PerM2, 85_000m, null, CurrencyUnit.Toman, null, 0m, false, AreaBasis.Intake, AllocationMethod.ByArea);
        var live = Allocate(washing, members);
        Shares(live, (A, 371_450m), (B, 680_000m), (C, 717_400m));
        Assert.Equal(1_768_850m, live.BaseAmount);
        var frozen = washing with { Frozen = true, Amount = live.OriginalAmount, BaseAmount = live.BaseAmount };
        Shares(Allocate(frozen, members), (A, 371_450m), (B, 680_000m), (C, 717_400m));
        Shares(Allocate(Total(1_768_850m, basis: AreaBasis.Intake), members), (A, 371_450m), (B, 680_000m), (C, 717_400m));
        var freight = Allocate(Total(10_000_000m), members);
        Shares(freight, (A, 2_099_952m), (B, 3_844_306m), (C, 4_055_742m));
        var totals = new[] { 42_588_002m, 19_824_306m, 77_947_942m };
        for (var i = 0; i < 3; i++) Assert.Equal(totals[i], purchases[i] + live.Shares.Single(s => s.RugId == members[i].RugId).Amount + freight.Shares.Single(s => s.RugId == members[i].RugId).Amount);
        Assert.Equal(140_360_250m, totals.Sum());
        Assert.Equal(new decimal?[] { 9_745_538m, 2_478_038m, 9_235_538m }, totals.Select((t, i) => RugCostMath.CostPerM2(t, members[i].EffectivePieceArea * members[i].Quantity, 0)).ToArray());
        Assert.Equal(396_486m, RugCostMath.CostPerPiece(totals[1], 50, 0));
        members[2] = members[2] with { EffectivePieceArea = 8.25m };
        freight = Allocate(Total(10_000_000m), members);
        Shares(freight, (A, 2_119_302m), (B, 3_879_728m), (C, 4_000_970m));
        Assert.Equal(77_893_170m, purchases[2] + 717_400m + freight.Shares.Single(s => s.RugId == C).Amount);
        var remaining = members.Where(m => m.RugId != B).ToArray();
        Shares(Allocate(Total(10_000_000m), remaining), (A, 3_462_758m), (C, 6_537_242m));
        var washingWithoutB = Allocate(frozen, remaining);
        Shares(washingWithoutB, (A, 603_425m), (C, 1_165_425m));
        Assert.Contains(washingWithoutB.Warnings, w => w.Code == CostWarnings.RateDrift);
        Assert.Equal(44_182_783m, purchases[0] + 3_462_758m + 603_425m);
        Assert.Equal(80_877_467m, purchases[2] + 6_537_242m + 1_165_425m);
        Shares(Allocate(frozen, members), (A, 371_450m), (B, 680_000m), (C, 717_400m));
        members[0] = members[0] with { LineUsd = 196.65m }; members[1] = members[1] with { LineUsd = 360m }; members[2] = members[2] with { LineUsd = 371.25m };
        Assert.Equal(927.90m, members.Sum(m => m.LineUsd));
        Shares(Allocate(Total(3_000_000m, AllocationMethod.ByDeclaredValue), members), (A, 635_790m), (B, 1_163_919m), (C, 1_200_291m));
        var revenue = CostAllocator.RevenueBase(1800, CurrencyUnit.Usd, 105_000m, CurrencyUnit.Toman, 0)!.Value;
        var shares = CostAllocator.RevenueShares(revenue, 0, members.Select(m => new RevenueLine(m.RugId, m.LineUsd, m.SerialSort)).ToList());
        Assert.Equal(40_054_801m, shares[A]); Assert.Equal(73_326_867m, shares[B]); Assert.Equal(75_618_332m, shares[C]);
        Assert.Equal(45_639_750m, revenue - 143_360_250m);
        Shares(Allocate(Total(600_000m), members), (A, 127_158m), (B, 232_784m), (C, 240_058m));
        Assert.Equal(45_039_750m, revenue - 143_960_250m);
    }

    [Fact]
    public void Pins_DroppedZeroWeightsManualAndAllPinnedResidual_AreAccountedFor()
    {
        var members = new[] { Member(A, 1), Member(B, 0), Member(C, 0) };
        var result = Allocate(Total(100), members, new AllocationPin(A, 40, null));
        Shares(result, (A, 40), (B, 30), (C, 30));
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.ZeroWeights && w.Count == 2);
        result = Allocate(Total(100), members.Take(1).ToArray(), new AllocationPin(A, 40, null), new AllocationPin(B, 30, "B"));
        Shares(result, (A, 40));
        Assert.Equal(AllocationState.Partial, result.State); Assert.Equal(60, result.Unallocated);
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.PinsResidual);
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.PinDropped && w.Serial == "B");
        result = Allocate(Total(100, AllocationMethod.Manual), members, new AllocationPin(A, 40, null));
        Shares(result, (A, 40), (B, 0), (C, 0)); Assert.Equal(60, result.Unallocated);
        result = Allocate(Total(100), []);
        Assert.Equal(AllocationState.Unallocated, result.State); Assert.Equal(100, result.Unallocated);
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.EmptyTarget);
        result = Allocate(Total(100) with { TargetDeleted = true }, []);
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.TargetDeleted);
    }

    [Fact]
    public void NegativeMissingAndZeroWeights_AndNegativeAmounts_UseLargestRemainder()
    {
        var members = new[] { Member(A, 1, purchase: -20), Member(B, 1, purchase: 10), Member(C, 1, purchase: 30) };
        var result = Allocate(Total(-100, AllocationMethod.ByPurchaseCost), members);
        Shares(result, (A, 0), (B, -25), (C, -75));
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.NegativeWeight && w.Count == 1);
        result = Allocate(Total(-100, AllocationMethod.ByDeclaredValue), members);
        // SerialSort breaks the equal tie: A (3778), C (3782), B (3911).
        Shares(result, (A, -34), (B, -33), (C, -33));
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.NoDeclaredValue && w.Count == 3);
        Assert.Contains(result.Warnings, w => w.Code == CostWarnings.ZeroWeights);
    }

    [Theory]
    [InlineData(CostBasis.PerM2, CurrencyUnit.Usd, 45.55, 105000.5, 765454, 38272700)]
    [InlineData(CostBasis.PerPiece, CurrencyUnit.Rial, 85, 0, 9, 450)]
    [InlineData(CostBasis.PerM2, CurrencyUnit.Rial, 45.55, 0, 1, 50)]
    [InlineData(CostBasis.PerPiece, CurrencyUnit.Toman, 12345.5, 0, 12346, 617300)]
    public void ForeignCurrencyAndLots_RoundEveryPieceBeforeMultiplying(CostBasis basis, CurrencyUnit currency, decimal rate, decimal fx, decimal piece, decimal line)
    {
        var e = new AllocationEntry(CostGroup.Expense, basis, rate, null, currency, fx == 0 ? null : fx, 0, false, AreaBasis.Intake, AllocationMethod.ByArea);
        var result = Allocate(e, [Member(B, .16m, 50)]);
        Assert.Equal(line, result.BaseAmount); Assert.Equal(piece, result.Shares.Single().PerPiece);
        Assert.Equal(9m, CostAllocator.BaseOfTotal(85, CurrencyUnit.Rial, CurrencyUnit.Toman, null, 0));
        Assert.Equal(850m, CostAllocator.BaseOfTotal(85, CurrencyUnit.Toman, CurrencyUnit.Rial, null, 0));
    }

    [Fact]
    public void WarningsAreRebuiltAndDiffDoesNotTouchUnchangedMoney()
    {
        var member = Member(A, 1) with { HasLinkedStep = true, HasPurchaseLines = true, InLateShipment = true };
        var e = Total(100) with { Group = CostGroup.Purchase };
        var input = new EntryInput(Guid.NewGuid(), 1, false, e.Method, e, [member], [], [], null);
        var first = EntryRunner.Compute(input, CurrencyUnit.Toman, 0);
        Assert.Contains(first.Warnings, w => w.Code == CostWarnings.StepDouble);
        Assert.Contains(first.Warnings, w => w.Code == CostWarnings.PurchaseDouble);
        Assert.Contains(first.Warnings, w => w.Code == CostWarnings.LateCost);
        input = input with { Stored = first.Shares.Select(s => new StoredAllocation(s.RugId, s.Amount, s.WeightUnits, s.IsPinned)).ToList(), Result = EntryRunner.ToStored(first) };
        Assert.Null(EntryRunner.Plan(input, CurrencyUnit.Toman, 0).Write);
        input = input with { Members = [member with { HasLinkedStep = false, HasPurchaseLines = false, InLateShipment = false }] };
        var (write, changed) = EntryRunner.Plan(input, CurrencyUnit.Toman, 0);
        Assert.NotNull(write); Assert.Empty(changed); Assert.Empty(write.Upserts); Assert.Empty(write.Result!.Warnings);
        input = input with { Deleted = true };
        (write, changed) = EntryRunner.Plan(input, CurrencyUnit.Toman, 0);
        Assert.Equal(new[] { A }, changed); Assert.Equal(new[] { A }, write!.RemovedRugs);
    }

    [Fact]
    public void OneThousandRandomAllocations_ConserveMoneyAndAreOrderIndependent()
    {
        var random = new Random(5102026);
        for (var trial = 0; trial < 1000; trial++)
        {
            var count = random.Next(1, 2001);
            var members = Enumerable.Range(0, count).Select(i => Member(new Guid(i + 1, 0, 0, new byte[8]), 1, purchase: random.NextInt64(-1_000_000_000, 1_000_000_001))).ToArray();
            var amount = random.NextInt64(-1_000_000_000_000_000, 1_000_000_000_000_001);
            var pins = random.Next(2) == 0 ? new[] { new AllocationPin(members[0].RugId, decimal.Truncate(amount / 5m), null) } : [];
            var entry = Total(amount, AllocationMethod.ByPurchaseCost);
            var first = Allocate(entry, members, pins);
            Assert.Equal(amount, first.Allocated + first.Unallocated);
            Assert.All(first.Shares, s => { Assert.True(s.Amount == 0 || Math.Sign(s.Amount) == Math.Sign(amount)); Assert.True(Math.Abs(s.Amount) <= Math.Abs(amount)); });
            var reverse = Allocate(entry, members.Reverse().ToArray(), pins);
            Assert.Equal(first.Shares.OrderBy(s => s.RugId), reverse.Shares.OrderBy(s => s.RugId));
            Assert.Equal(first.Shares, Allocate(entry, members, pins).Shares);
        }
    }

    [Fact]
    public void GuidTiesFollowOrdinalTextAndTenThousandMembersStayPractical()
    {
        var left = Guid.Parse("80000000-0000-0000-0000-000000000000");
        var right = Guid.Parse("00000000-0000-0000-0000-000000000000");
        var members = new[] { Member(left, 1) with { SerialSort = null }, Member(right, 1) with { SerialSort = null } };
        Shares(Allocate(Total(1), members), (left, 0), (right, 1));
        var tenK = Enumerable.Range(1, 10_000).Select(i => Member(new Guid(i, 0, 0, new byte[8]), 1)).ToArray();
        _ = Allocate(Total(1), tenK); // JIT and warm-up are not allocation time.
        var watch = Stopwatch.StartNew();
        var result = Allocate(Total(123_456_789m), tenK);
        watch.Stop(); Assert.Equal(123_456_789m, result.Shares.Sum(s => s.Amount));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"10k allocations took {watch.Elapsed}");
    }
}
