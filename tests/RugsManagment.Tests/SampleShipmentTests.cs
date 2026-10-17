using System.Text.Json;
using RugsManagment.Domain.Common;

namespace RugsManagment.Tests;

/// <summary>
/// همهٔ ۱۶۱ ردیف اظهارنامهٔ نمونهٔ بازرگان (<c>tests/fixtures/sample-shipment.json</c>): مساحت هر تخته با
/// <c>AreaMath</c> باید دقیقاً ستون J فایل و دلار هر تخته ستون L آن باشد؛ جمع هر نگله با جدول خلاصهٔ فایل و
/// جمع کل با ۲۴۲ تخته / ۴۶۴٫۷۴ متر / ۲۰٬۳۲۲٫۵۰ دلار برابر است (spec L.1، E.10، erratum E14).
/// </summary>
public class SampleShipmentTests
{
    private sealed record Line(int Row, int Bale, string Serial, int Qty, decimal Length, decimal Width,
        decimal UsdPerM2, decimal CachedArea, decimal CachedUsd);

    private static IReadOnlyList<Line> Lines()
    {
        using var document = Fixtures.SampleShipment();
        return document.RootElement.GetProperty("lines").EnumerateArray()
            .Select(e => new Line(
                e.GetProperty("row").GetInt32(),
                e.GetProperty("bale").GetInt32(),
                e.GetProperty("serial").GetString()!,
                e.GetProperty("qty").GetInt32(),
                e.GetProperty("length").GetDecimal(),
                e.GetProperty("width").GetDecimal(),
                e.GetProperty("usdPerM2").GetDecimal(),
                e.GetProperty("cachedArea").GetDecimal(),
                e.GetProperty("cachedUsd").GetDecimal()))
            .ToList();
    }

    [Fact]
    public void Fixture_Has161LinesIn36Bales_And242Pieces()
    {
        var lines = Lines();
        Assert.Equal(161, lines.Count);
        Assert.Equal(36, lines.Select(l => l.Bale).Distinct().Count());
        Assert.Equal(242, lines.Sum(l => l.Qty));
        Assert.Equal(Enumerable.Range(1, 36), lines.Select(l => l.Bale).Distinct().Order());
    }

    [Fact]
    public void EveryLine_PieceArea_ReproducesColumnJ()
    {
        var mismatches = Lines()
            .Where(l => AreaMath.PieceArea(l.Length, l.Width) != l.CachedArea)
            .Select(l => $"R{l.Row} {l.Serial}: {AreaMath.PieceArea(l.Length, l.Width)} ≠ {l.CachedArea}")
            .ToList();

        Assert.Empty(mismatches);
    }

    [Fact]
    public void EveryLine_PieceUsd_ReproducesColumnL()
    {
        var mismatches = Lines()
            .Where(l => AreaMath.PieceUsd(AreaMath.PieceArea(l.Length, l.Width), l.UsdPerM2) != l.CachedUsd)
            .Select(l => $"R{l.Row} {l.Serial}")
            .ToList();

        Assert.Empty(mismatches);
    }

    [Fact]
    public void BaleTotals_MatchTheFilesSummaryTable()
    {
        var computed = Lines()
            .GroupBy(l => l.Bale)
            .ToDictionary(g => g.Key, g => (
                Pieces: g.Sum(l => l.Qty),
                Area: g.Sum(l => AreaMath.LineArea(AreaMath.PieceArea(l.Length, l.Width), l.Qty)),
                Usd: g.Sum(l => AreaMath.LineUsd(AreaMath.PieceUsd(AreaMath.PieceArea(l.Length, l.Width), l.UsdPerM2), l.Qty))));

        using var document = Fixtures.SampleShipment();
        foreach (var row in document.RootElement.GetProperty("summary").EnumerateArray())
        {
            var bale = row.GetProperty("bale").GetInt32();
            var (pieces, area, usd) = computed[bale];
            Assert.True(row.GetProperty("pieces").GetInt32() == pieces, $"نگلهٔ {bale}: تعداد");
            Assert.True(row.GetProperty("areaM2").GetDecimal() == area, $"نگلهٔ {bale}: متراژ {area}");
            Assert.True(row.GetProperty("usd").GetDecimal() == usd, $"نگلهٔ {bale}: دلار {usd}");
        }
    }

    [Fact]
    public void GrandTotals_Are242Pieces_464_74Square_20322_50Usd()
    {
        var lines = Lines();
        var area = lines.Sum(l => AreaMath.LineArea(AreaMath.PieceArea(l.Length, l.Width), l.Qty));
        var usd = lines.Sum(l => AreaMath.LineUsd(AreaMath.PieceUsd(AreaMath.PieceArea(l.Length, l.Width), l.UsdPerM2), l.Qty));

        Assert.Equal(464.74m, area);
        Assert.Equal(20_322.50m, usd);

        using var document = Fixtures.SampleShipment();
        var totals = document.RootElement.GetProperty("totals");
        Assert.Equal(242, totals.GetProperty("pieces").GetInt32());
        Assert.Equal(area, totals.GetProperty("areaM2").GetDecimal());
        Assert.Equal(usd, totals.GetProperty("usd").GetDecimal());
    }

    [Theory]
    [InlineData("3789", "2.90")]
    [InlineData("3816", "3.80")]
    [InlineData("3853", "0.76")]
    [InlineData("3882", "0.39")]
    [InlineData("3901", "0.75")]
    [InlineData("4009", "0.40")]
    public void MidpointRows_CacheTheAwayFromZeroValue(string serial, string expected)
    {
        var line = Lines().Single(l => l.Serial == serial);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            AreaMath.PieceArea(line.Length, line.Width));
    }

    [Fact]
    public void Lots_Bale29And30_FollowTheLotRule()
    {
        var lines = Lines();

        var bale29 = lines.Single(l => l.Bale == 29);
        Assert.Equal(("3911 - 3960", 50), (bale29.Serial, bale29.Qty));
        Assert.Equal(8.00m, AreaMath.LineArea(AreaMath.PieceArea(bale29.Length, bale29.Width), bale29.Qty));
        Assert.Equal(360.00m, AreaMath.LineUsd(AreaMath.PieceUsd(0.16m, 45m), 50));

        var bale30 = lines.Where(l => l.Bale == 30).ToList();
        Assert.Equal(34, bale30.Sum(l => l.Qty));
        Assert.Equal(6.24m, bale30.Sum(l => AreaMath.LineArea(AreaMath.PieceArea(l.Length, l.Width), l.Qty)));
    }

    [Fact]
    public void Quirks_4004IsInBale31_AndBale33KeepsItsNumber()
    {
        var lines = Lines();
        Assert.Equal(31, lines.Single(l => l.Serial == "4004").Bale);
        Assert.Equal(new[] { "4016", "4017", "4018", "4019" },
            lines.Where(l => l.Bale == 33).Select(l => l.Serial));
    }

    [Fact]
    public void Sizes_AreStoredWithAtMostTwoDecimals()
        => Assert.All(Lines(), l =>
        {
            Assert.True(Num.HasAtMostDecimals(l.Length, 2));
            Assert.True(Num.HasAtMostDecimals(l.Width, 2));
        });

    [Fact]
    public void LotRows_HaveNoColourOrPattern_AndSplitOriginMaterial()
    {
        using var document = Fixtures.SampleShipment();
        var lots = document.RootElement.GetProperty("lines").EnumerateArray()
            .Where(e => e.GetProperty("qty").GetInt32() > 1)
            .ToList();

        Assert.Equal(3, lots.Count);
        Assert.All(lots, e =>
        {
            Assert.Equal(JsonValueKind.Null, e.GetProperty("bg_color").ValueKind);
            Assert.Equal(JsonValueKind.Null, e.GetProperty("pattern").ValueKind);
            Assert.Equal(JsonValueKind.String, e.GetProperty("origin").ValueKind);
            Assert.Equal(JsonValueKind.String, e.GetProperty("material").ValueKind);
        });
    }
}
