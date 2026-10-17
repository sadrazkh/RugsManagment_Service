using System.Text.RegularExpressions;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;

namespace RugsManagment.Tests;

/// <summary>
/// شمارهٔ بازرگان و ضلع فرش (spec G.4، L.1 «Dimension / SerialNumber»). شکل‌های پذیرفته و کدهای خطا در
/// <c>math-vectors.json</c> (<see cref="CostVectorsTests"/>) هستند؛ این‌جا رفتارهای دیگر: ساختن از عدد، همپوشانی، برش
/// دسته، بازسازی از ستون‌ها، و متن پیام‌ها.
/// </summary>
public partial class SerialNumberTests
{
    [Fact]
    public void Range_BuildsTheDisplayForm_AndSameEndsIsASingle()
    {
        var lot = SerialNumber.Range(3911, 3960);
        Assert.Equal("3911 - 3960", lot.Display);
        Assert.True(lot.IsRange);
        Assert.Equal(50, lot.Count);
        Assert.Equal(3961, lot.NextAfter);

        var single = SerialNumber.Range(3778, 3778);
        Assert.Equal("3778", single.Display);
        Assert.False(single.IsRange);
        Assert.Equal(SerialNumber.Single(3778), single);
    }

    [Fact]
    public void Range_RejectsReversedAndTooLong()
    {
        Assert.Equal(SerialNumber.ReversedErrorMessage,
            Assert.Throws<DomainValidationException>(() => SerialNumber.Range(3960, 3911)).Message);
        Assert.Equal(SerialNumber.SpanErrorMessage,
            Assert.Throws<DomainValidationException>(() => SerialNumber.Range(1, 10_001)).Message);
        Assert.Equal(10_000, SerialNumber.Range(1, 10_000).Count);
    }

    [Fact]
    public void ForLot_CoversExactlyTheQuantity()
    {
        // G.4: ردیفی که فقط تعداد ۵۰ گرفته بازهٔ From..From + qty − 1 می‌گیرد
        var lot = SerialNumber.ForLot(3911, 50);
        Assert.Equal("3911 - 3960", lot.Display);
        Assert.True(lot.Covers(50));
        Assert.False(lot.Covers(51));
        Assert.Equal(SerialNumber.Single(4004), SerialNumber.ForLot(4004, 1));
    }

    [Fact]
    public void SplitLast_GivesTheTailToTheNewRug()
    {
        var (kept, taken) = SerialNumber.Range(3911, 3960).SplitLast(20);
        Assert.Equal("3911 - 3940", kept.Display);
        Assert.Equal("3941 - 3960", taken.Display);
        Assert.False(kept.Overlaps(taken));

        var (one, last) = SerialNumber.Range(10, 11).SplitLast(1);
        Assert.Equal("10", one.Display);
        Assert.Equal("11", last.Display);

        Assert.Throws<ArgumentOutOfRangeException>(() => SerialNumber.Range(1, 5).SplitLast(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => SerialNumber.Range(1, 5).SplitLast(0));
        Assert.Throws<InvalidOperationException>(() => SerialNumber.Single(3778).SplitLast(1));
        Assert.Throws<InvalidOperationException>(() => SerialNumber.Parse("A-12")!.SplitLast(1));
    }

    [Theory]
    [InlineData("3911 - 3960", "3950 - 3970", true)] // acceptance: «3950-3970» با دستهٔ موجود همپوشانی دارد
    [InlineData("3911 - 3960", "3960", true)]
    [InlineData("3911 - 3960", "3961 - 3970", false)]
    [InlineData("3778", "3778", true)]
    [InlineData("0123", "123", true)] // صفر پیشوند همان عدد است
    [InlineData("A-12", "A-12", true)]
    [InlineData("A-12", "A-13", false)]
    [InlineData("A-12", "12", false)]
    public void Overlaps_NumericByRange_TextByExactText(string a, string b, bool expected)
    {
        var first = SerialNumber.Parse(a)!;
        var second = SerialNumber.Parse(b)!;
        Assert.Equal(expected, first.Overlaps(second));
        Assert.Equal(expected, second.Overlaps(first));
    }

    [Fact]
    public void Contains_FindsANumberInsideALot()
    {
        var lot = SerialNumber.Parse("3911 - 3960")!;
        Assert.True(lot.Contains(3911));
        Assert.True(lot.Contains(3935));
        Assert.True(lot.Contains(3960));
        Assert.False(lot.Contains(3961));
        Assert.False(SerialNumber.Parse("A-12")!.Contains(12));
    }

    [Fact]
    public void FromStored_RebuildsWithoutValidation()
    {
        Assert.Null(SerialNumber.FromStored(null, null, null));
        Assert.Null(SerialNumber.FromStored("", 1, 1));
        Assert.Equal(SerialNumber.Range(3911, 3960), SerialNumber.FromStored("3911 - 3960", 3911, 3960));
        Assert.Equal(SerialNumber.Parse("A-12"), SerialNumber.FromStored("A-12", null, null));

        // SerialTo خالی برای شمارهٔ تک (دادهٔ قدیمی) همان SerialFrom حساب می‌شود
        var legacy = SerialNumber.FromStored("3778", 3778, null)!;
        Assert.Equal(3778, legacy.To);
        Assert.Equal(3779, legacy.NextAfter);
    }

    [Fact]
    public void Parse_StripsDirectionMarksAndNonBreakingSpaces()
    {
        Assert.Equal("3911 - 3960", SerialNumber.Parse("‏3911 - 3960‎")!.Display);
        Assert.Equal("3911 - 3960", SerialNumber.Parse("3911‌-‌3960")!.Display);
        Assert.Equal("3911 - 3960", SerialNumber.Parse("3911— 3960")!.Display);
        Assert.Equal("3911 - 3960", SerialNumber.Parse("3911 − 3960")!.Display);
    }

    [Fact]
    public void Parse_NormalisesArabicLettersInTextSerials()
        => Assert.Equal(SerialNumber.Parse("ک-۱۲")!.Display, SerialNumber.Parse("ك-12")!.Display);

    [Fact]
    public void Parse_OfNullOrBlank_IsNoSerial()
    {
        Assert.Null(SerialNumber.Parse(null));
        Assert.Null(SerialNumber.Parse(""));
        Assert.Null(SerialNumber.Parse(" ‌ "));
    }

    [Fact]
    public void QuantityError_FillsTheTokensWithPersianDigits()
    {
        var error = SerialNumber.Parse("3911 - 3960")!.QuantityError(51);
        Assert.Equal(SerialNumber.QuantityErrorCode, error.Code);
        Assert.Contains("۳۹۱۱ - ۳۹۶۰", error.Message, StringComparison.Ordinal);
        Assert.Contains("۵۰", error.Message, StringComparison.Ordinal);
        Assert.Contains("۵۱", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{", error.Message, StringComparison.Ordinal);
    }

    /// <summary>پیام‌های دامنه به کاربر می‌رسند؛ همان قاعدهٔ Messages.fa: هیچ حرف لاتینی بیرون از نشانه‌ها.</summary>
    [Fact]
    public void DomainMessages_ArePersian()
    {
        var messages = new[]
        {
            SerialNumber.FormatErrorMessage, SerialNumber.ReversedErrorMessage, SerialNumber.SpanErrorMessage,
            SerialNumber.TooLongErrorMessage, SerialNumber.QuantityErrorTemplate, Dimension.ErrorMessage,
            Domain.Entities.Rug.QuantityErrorMessage, Domain.Entities.Rug.FinalSizeSetErrorMessage
        };
        Assert.All(messages, m => Assert.DoesNotMatch(LatinLetter(), TokenPattern().Replace(m, "")));
    }

    [Fact]
    public void DimensionMessage_IsTheSameAsMessagesFa()
    {
        Assert.Equal(Dimension.ErrorCode, Messages.Dimension.Code);
        Assert.Equal(Dimension.ErrorMessage, Messages.Dimension.Text);
    }

    // ═════════════════════════════════════════════════════════════════════
    // Dimension
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("0.05", true)]
    [InlineData("30", true)]
    [InlineData("2.08", true)]
    [InlineData("2.080", true)]
    [InlineData("0.04", false)]
    [InlineData("30.01", false)]
    [InlineData("2.081", false)] // بیش از دو رقم اعشار
    [InlineData("0", false)]
    [InlineData("-2", false)]
    public void Dimension_HardLimits(string value, bool valid)
    {
        var meters = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(valid, Dimension.IsValid(meters));
        Assert.Equal(valid, Dimension.TryFrom(meters, out _));
        if (valid)
        {
            Assert.Equal(meters, Dimension.From(meters).Meters);
        }
        else
        {
            var thrown = Assert.Throws<DomainValidationException>(() => Dimension.From(meters, "rug.intakeLength"));
            Assert.Equal(Dimension.ErrorMessage, thrown.Message);
            Assert.True(thrown.Errors.ContainsKey("rug.intakeLength"));
        }
    }

    [Fact]
    public void Dimension_IsStoredWithTwoDecimals()
    {
        Assert.Equal("2.08", Dimension.From(2.080m).ToString());
        decimal meters = Dimension.From(0.4m);
        Assert.Equal(0.40m, meters);
    }

    [GeneratedRegex(@"\{[A-Za-z][A-Za-z0-9]*\}")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinLetter();
}
