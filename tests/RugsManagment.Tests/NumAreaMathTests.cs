using System.Globalization;
using System.Text.Json;
using RugsManagment.Domain.Common;

namespace RugsManagment.Tests;

/// <summary>
/// گرد کردن و قاعدهٔ دسته — هر عددی که روی اظهارنامه می‌رود از این‌جا می‌گذرد. اگر یک جا «بانکی» (ToEven)
/// گرد شود یا اول ضرب و بعد گرد، جمع اظهار با اکسل بازرگان نمی‌خواند (spec G.4، L.1).
/// </summary>
public class NumAreaMathTests
{
    private static decimal D(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("2.675", 2, "2.68")]
    [InlineData("-2.675", 2, "-2.68")]
    [InlineData("0.005", 2, "0.01")]
    [InlineData("2.665", 2, "2.67")] // ToEven می‌داد ۲٫۶۶
    [InlineData("0.385", 2, "0.39")] // ToEven می‌داد ۰٫۳۸ (ردیف ۱۴۹ نمونه)
    [InlineData("0.5", 0, "1")]
    [InlineData("-0.5", 0, "-1")]
    [InlineData("2.5", 0, "3")]
    public void Round_IsAlwaysHalfAwayFromZero(string value, int decimals, string expected)
        => Assert.Equal(D(expected), Num.Round(D(value), decimals));

    [Fact]
    public void Round_Nullable_KeepsNull()
    {
        Assert.Null(Num.Round((decimal?)null, 2));
        Assert.Equal(2.68m, Num.Round((decimal?)2.675m, 2));
    }

    [Theory]
    [InlineData(2.2999999999999998, "2.30")]
    [InlineData(2.0699999999999998, "2.07")]
    [InlineData(0.56000000000000005, "0.56")]
    [InlineData(13.510000000000002, "13.51")]
    public void Snap_CleansExcelFloatNoise(double raw, string expected)
        => Assert.Equal(D(expected), Num.Snap(raw));

    [Fact]
    public void Snap_RejectsNaNAndInfinity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Num.Snap(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Num.Snap(double.PositiveInfinity));
    }

    [Theory]
    [InlineData("2.08", 2, true)]
    [InlineData("2.081", 2, false)]
    [InlineData("12", 0, true)]
    [InlineData("12.00", 0, true)]
    [InlineData("0.000001", 6, true)]
    [InlineData("0.0000001", 6, false)]
    public void HasAtMostDecimals(string value, int decimals, bool expected)
        => Assert.Equal(expected, Num.HasAtMostDecimals(D(value), decimals));

    /// <summary>ردیف‌های نیمه‌راهِ نمونه و چهار جفت دیگر از L.1.</summary>
    [Theory]
    [InlineData("1.93", "1.50", "2.90")] // R17
    [InlineData("2.30", "1.65", "3.80")] // R57
    [InlineData("1.51", "0.50", "0.76")] // R107
    [InlineData("0.77", "0.50", "0.39")] // R149
    [InlineData("1.49", "0.50", "0.75")] // R176
    [InlineData("0.79", "0.50", "0.40")] // R197
    [InlineData("2.50", "0.37", "0.93")]
    [InlineData("1.50", "0.83", "1.25")]
    [InlineData("1.05", "0.50", "0.53")]
    [InlineData("0.45", "0.45", "0.20")]
    public void PieceArea_RoundsTheProductHalfAwayFromZero(string length, string width, string expected)
        => Assert.Equal(D(expected), AreaMath.PieceArea(D(length), D(width)));

    [Fact]
    public void PieceArea_SnapsSidesFirst_SoTheImportedFloatGives380()
        => Assert.Equal(3.80m, AreaMath.PieceArea(Num.Snap(2.2999999999999998), Num.Snap(1.65)));

    [Fact]
    public void LotRule_RoundsPerPieceThenMultiplies()
    {
        var piece = AreaMath.PieceArea(0.40m, 0.40m);
        Assert.Equal(0.16m, piece);
        Assert.Equal(8.00m, AreaMath.LineArea(piece, 50));

        // دلار: ۰٫۱۶ × ۴۵٫۵۵ = ۷٫۲۸۸ → ۷٫۲۹ برای هر تخته → ۳۶۴٫۵۰ (نه ۳۶۴٫۴۰)
        var pieceUsd = AreaMath.PieceUsd(piece, 45.55m);
        Assert.Equal(7.29m, pieceUsd);
        Assert.Equal(364.50m, AreaMath.LineUsd(pieceUsd, 50));
    }

    [Fact]
    public void Money_PerM2_RoundsPerPieceAtMoneyScale()
    {
        // مقیاس ۰: ROUND(7.288, 0) = 7 → ۳۵۰؛ مقیاس ۲: ۷٫۲۹ × ۵۰ = ۳۶۴٫۵۰
        Assert.Equal(350m, AreaMath.LineMoney(AreaMath.PieceMoney(45.55m, 0.16m, 0), 50));
        Assert.Equal(364.50m, AreaMath.LineMoney(AreaMath.PieceMoney(45.55m, 0.16m, 2), 50));

        // ۹٬۰۰۰٬۰۰۰ برای هر متر روی ۰٫۱۶ → ۱٬۴۴۰٬۰۰۰ برای هر تخته
        var piece = AreaMath.PieceMoney(9_000_000m, 0.16m, 0);
        Assert.Equal(1_440_000m, piece);
        Assert.Equal(72_000_000m, AreaMath.LineMoney(piece, 50));
    }

    [Fact]
    public void Money_PerPiece_RoundsTheRateOnce()
        => Assert.Equal(37_038m, AreaMath.LineMoney(AreaMath.PieceMoneyPerPiece(12_345.5m, 0), 3));

    [Fact]
    public void Fx_IsAppliedPerPiece_NotToTheLineTotal()
    {
        var piece = AreaMath.ConvertPiece(7.29m, 105_000.5m, 0);
        Assert.Equal(765_454m, piece);
        Assert.Equal(38_272_700m, AreaMath.LineMoney(piece, 50));

        // اگر نرخ روی جمع ردیف زده می‌شد: ROUND(364.50 × 105,000.5) = 38,272,682 — همان خطایی که نباید رخ دهد
        Assert.NotEqual(Num.Round(364.50m * 105_000.5m, 0), AreaMath.LineMoney(piece, 50));
    }

    [Fact]
    public void SquareFeet_UseTheExactFactor()
        => Assert.Equal(10.7639104m, AreaMath.ToSquareFeet(1m));

    [Fact]
    public void NegativeQuantity_IsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => AreaMath.LineArea(1m, -1));

    // ═════════════════════════════════════════════════════════════════════
    // بردارهای مشترک (tests/fixtures/math-vectors.json — همان که vitest می‌خواند)
    // ═════════════════════════════════════════════════════════════════════

    public static TheoryData<string> MathVectorIds()
    {
        var data = new TheoryData<string>();
        foreach (var vector in Fixtures.MathVectors().RootElement.GetProperty("vectors").EnumerateArray())
            data.Add(vector.GetProperty("id").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(MathVectorIds))]
    public void SharedVector(string id)
    {
        using var document = Fixtures.MathVectors();
        var v = document.RootElement.GetProperty("vectors").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == id);

        var kind = v.GetProperty("kind").GetString();
        Assert.Contains(kind, new[] { "area", "usd", "money" });
        var scale = v.GetProperty("scale").GetInt32();
        decimal Str(string name) => D(v.GetProperty(name).GetString()!);
        int Qty() => v.GetProperty("qty").GetInt32();

        switch (v.GetProperty("op").GetString())
        {
            case "round":
                Assert.Equal(Str("expected"), Num.Round(Str("value"), scale));
                break;

            case "snap":
                Assert.Equal(Str("expected"), Num.Snap(v.GetProperty("raw").GetDouble(), scale));
                break;

            case "pieceArea":
            {
                var piece = AreaMath.PieceArea(Str("length"), Str("width"));
                Assert.Equal(Str("piece"), piece);
                Assert.Equal(Str("line"), AreaMath.LineArea(piece, Qty()));
                break;
            }

            case "snapArea":
            {
                var piece = AreaMath.PieceArea(
                    Num.Snap(v.GetProperty("lengthRaw").GetDouble()), Num.Snap(v.GetProperty("widthRaw").GetDouble()));
                Assert.Equal(Str("piece"), piece);
                Assert.Equal(Str("line"), AreaMath.LineArea(piece, Qty()));
                break;
            }

            case "pieceUsd":
            {
                var piece = AreaMath.PieceUsd(Str("area"), Str("rate"));
                Assert.Equal(Str("piece"), piece);
                Assert.Equal(Str("line"), AreaMath.LineUsd(piece, Qty()));
                break;
            }

            case "pieceMoney":
            {
                var piece = AreaMath.PieceMoney(Str("rate"), Str("area"), scale);
                Assert.Equal(Str("piece"), piece);
                Assert.Equal(Str("line"), AreaMath.LineMoney(piece, Qty()));
                break;
            }

            case "perPiece":
            {
                var piece = AreaMath.PieceMoneyPerPiece(Str("rate"), scale);
                Assert.Equal(Str("piece"), piece);
                Assert.Equal(Str("line"), AreaMath.LineMoney(piece, Qty()));
                break;
            }

            case "convertPiece":
            {
                var piece = AreaMath.ConvertPiece(Str("amount"), Str("rate"), scale);
                Assert.Equal(Str("piece"), piece);
                Assert.Equal(Str("line"), AreaMath.LineMoney(piece, Qty()));
                break;
            }

            default:
                Assert.Fail($"عمل ناشناخته در بردار {id}");
                break;
        }
    }
}
