using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P4a.P4aChanges;

namespace RugsManagment.Tests.P4a;

/// <summary>
/// ارزش گمرکی (spec C.5، D.3 قاعدهٔ دسته، L.1 «DeclaredValue»): ترتیب «دستی ← جدول ← پیش‌فرض ← ندارد»، یکسان‌سازی کلید
/// («نائین ۹ لا» = «نائين 9 لا»)، دلار هر تخته گرد و بعد × تعداد، و مرحلهٔ ارزش خط (فقط ردیف‌های عوض‌شده، محمولهٔ قطعی منجمد).
/// </summary>
public class DeclaredValueTests
{
    private static readonly IReadOnlyDictionary<string, decimal> Table = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        [DeclaredValueMath.KeyNormalize("گبه")] = 45m,
        [DeclaredValueMath.KeyNormalize("نائین ۹ لا")] = 120m
    };

    [Fact]
    public void Resolve_FollowsOverrideThenTableThenFallbackThenMissing()
    {
        Assert.Equal((50m, DeclaredRateSource.Override), DeclaredValueMath.Resolve(50m, "گبه", Table, 30m));
        Assert.Equal((45m, DeclaredRateSource.Table), DeclaredValueMath.Resolve(null, "گبه", Table, 30m));
        Assert.Equal((30m, DeclaredRateSource.Fallback), DeclaredValueMath.Resolve(null, "کاشان", Table, 30m));
        Assert.Equal((null, DeclaredRateSource.Missing), DeclaredValueMath.Resolve(null, "کاشان", Table, null));
        Assert.Equal((null, DeclaredRateSource.Missing), DeclaredValueMath.Resolve(null, null, Table, null));
        Assert.Equal((0m, DeclaredRateSource.Override), DeclaredValueMath.Resolve(0m, "گبه", Table, null)); // صفر «دستی» است، نه «ندارد»
    }

    [Theory]
    [InlineData("نائین ۹ لا", "نائين 9 لا")]
    [InlineData("كاشان", "کاشان")]
    [InlineData("  گبه   قشقایی ", "گبه قشقایی")]
    [InlineData("Tabriz", "tabriz")]
    public void KeyNormalize_TreatsTheseAsOneKey(string a, string b)
        => Assert.Equal(DeclaredValueMath.KeyNormalize(a), DeclaredValueMath.KeyNormalize(b));

    [Fact]
    public void KeyNormalize_TableLookupUsesIt()
        => Assert.Equal((120m, DeclaredRateSource.Table), DeclaredValueMath.Resolve(null, "نائين 9 لا", Table, null));

    [Fact]
    public void Value_RoundsPerPieceThenMultiplies()
    {
        // ۰٫۱۶ × ۴۵٫۵۵ = ۷٫۲۸۸ → ۷٫۲۹ هر تخته → ۳۶۴٫۵۰ (نه ۳۶۴٫۴۰)
        Assert.Equal((8.00m, 7.29m, 364.50m), DeclaredValueMath.Value(0.16m, 50, 45.55m));
        // D.7 #9: A ۴٫۳۷ × ۴۵ = ۱۹۶٫۶۵؛ B ۰٫۱۶ × ۴۵ = ۷٫۲۰ × ۵۰ = ۳۶۰؛ C (نهایی) ۸٫۲۵ × ۴۵ = ۳۷۱٫۲۵
        Assert.Equal((4.37m, 196.65m, 196.65m), DeclaredValueMath.Value(4.37m, 1, 45m));
        Assert.Equal((8.00m, 7.20m, 360.00m), DeclaredValueMath.Value(0.16m, 50, 45m));
        Assert.Equal((8.25m, 371.25m, 371.25m), DeclaredValueMath.Value(8.25m, 1, 45m));
        // بی نرخ: متراژ هست، دلار نه
        Assert.Equal((8.00m, null, null), DeclaredValueMath.Value(0.16m, 50, null));
    }

    [Theory]
    [InlineData(2.08, 2.10, 2.03, 2.09, -5)]  // طول ۵ سانت کوچک‌تر
    [InlineData(2.08, 2.10, 2.10, 2.10, 2)]
    [InlineData(2.08, 2.10, 2.08, 2.10, 0)]
    [InlineData(3.35, 2.52, 3.30, 2.50, -5)]
    public void SizeChangeCm_IsTheLargerSignedChange(double il, double iw, double fl, double fw, int expected)
        => Assert.Equal(expected, DeclaredValueMath.SizeChangeCm((decimal)il, (decimal)iw, (decimal)fl, (decimal)fw));

    [Fact]
    public void SizeChangeCm_IsNullWithoutAFinalSize()
        => Assert.Null(DeclaredValueMath.SizeChangeCm(2.08m, 2.10m, null, null));

    [Fact]
    public void OverTarget_UsesTheTolerance()
    {
        Assert.False(DeclaredValueMath.IsOverTarget(14.30m, 13m, 10));
        Assert.True(DeclaredValueMath.IsOverTarget(14.31m, 13m, 10));
        Assert.False(DeclaredValueMath.IsOverTarget(100m, null, 10));
    }

    [Fact]
    public void KeyValueOf_ReadsBuiltInAndCustomFields()
    {
        Assert.Equal("گبه", DeclaredValueMath.KeyValueOf("rug.origin", "t", "گبه", "p", "m", null));
        Assert.Equal("p", DeclaredValueMath.KeyValueOf("rug.pattern", "t", "گبه", "p", "m", null));
        Assert.Equal("A", DeclaredValueMath.KeyValueOf("rug.custom.grade", null, null, null, null, """{"grade":"A"}"""));
        Assert.Equal("12", DeclaredValueMath.KeyValueOf("rug.custom.n", null, null, null, null, """{"n":12}"""));
        Assert.Null(DeclaredValueMath.KeyValueOf("rug.custom.grade", null, null, null, null, "not json"));
        Assert.Null(DeclaredValueMath.KeyValueOf("rug.unknown", "t", "o", "p", "m", null));
    }

    // ── مرحلهٔ ارزش خط ──

    private static LineValueInput Input(
        decimal area, int qty, string? key, decimal? manual = null, ShipmentStatus status = ShipmentStatus.Open,
        LineValueRow? stored = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status, area, qty, manual, key,
            stored is not null, stored?.ShipmentId, stored?.UsdPerM2, stored?.RateSource ?? DeclaredRateSource.Missing,
            stored?.PieceUsd, stored?.LineAreaM2 ?? 0m, stored?.LineUsd);

    [Fact]
    public async Task Stage_WritesOnlyChangedRows_AndPropagatesTheirRugs()
    {
        var db = new FakeShipmentLedgerRepository();
        db.Rates[DeclaredValueMath.KeyNormalize("گبه")] = 45m;
        var fresh = Input(0.16m, 50, "گبه");
        var unchangedInput = Input(4.37m, 1, "گبه");
        var unchanged = unchangedInput with
        {
            HasValue = true, StoredShipmentId = unchangedInput.ShipmentId, StoredUsdPerM2 = 45m,
            StoredSource = DeclaredRateSource.Table, StoredPieceUsd = 196.65m, StoredLineAreaM2 = 4.37m, StoredLineUsd = 196.65m
        };
        var frozen = Input(8.25m, 1, "گبه", status: ShipmentStatus.Finalized) with { HasValue = true, StoredLineUsd = 1m };
        db.Inputs.AddRange([fresh, unchanged, frozen]);

        var context = Context();
        foreach (var input in db.Inputs) context.Impact.ShipmentLines.Add(input.LineId);
        await new ShipmentLineValueStage(db).RunAsync(context, CancellationToken.None);

        var row = Assert.Single(db.Written);
        Assert.Equal(fresh.LineId, row.LineId);
        Assert.Equal((45m, DeclaredRateSource.Table, 7.20m, 8.00m, 360.00m), (row.UsdPerM2, row.RateSource, row.PieceUsd, row.LineAreaM2, row.LineUsd));
        Assert.Equal([fresh.RugId], context.LineUsdChangedRugs);
        Assert.Equal(1, context.LinesRecomputed);
    }

    [Fact]
    public async Task Stage_DoesNothingWithoutLines()
    {
        var db = new FakeShipmentLedgerRepository();
        db.Inputs.Add(Input(1m, 1, "گبه"));
        await new ShipmentLineValueStage(db).RunAsync(Context(), CancellationToken.None);
        Assert.Empty(db.Written);
        Assert.Null(db.LastInputRequest);
    }

    [Fact]
    public void Stage_Compute_MissingRateKeepsTheArea()
    {
        var row = ShipmentLineValueStage.Compute(Input(0.16m, 50, "کاشان"), new Dictionary<string, decimal>(), null);
        Assert.Equal((null, DeclaredRateSource.Missing, null, 8.00m, null), (row.UsdPerM2, row.RateSource, row.PieceUsd, row.LineAreaM2, row.LineUsd));

        var manual = ShipmentLineValueStage.Compute(Input(0.16m, 50, "کاشان", manual: 45.55m), new Dictionary<string, decimal>(), null);
        Assert.Equal((DeclaredRateSource.Override, 364.50m), (manual.RateSource, manual.LineUsd));
    }

    [Fact]
    public void StageOrder_IsTheReservedSlot()
        => Assert.Equal(LedgerStageOrder.ShipmentLineValue, new ShipmentLineValueStage(new FakeShipmentLedgerRepository()).Order);
}
