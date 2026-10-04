using RugsManagment.Application.Common;

namespace RugsManagment.Tests;

/// <summary>
/// عدد به حروف — خط «ده میلیون تومان» زیر هر خانهٔ مبلغ. <c>CA/lib/numWords.ts</c> باید برای همین ورودی‌ها
/// دقیقاً همین متن را بسازد (قاعده‌ها در توضیح <see cref="NumberWords"/>).
/// </summary>
public class NumberWordsTests
{
    [Theory]
    [InlineData("0", "صفر")]
    [InlineData("1", "یک")]
    [InlineData("11", "یازده")]
    [InlineData("19", "نوزده")]
    [InlineData("20", "بیست")]
    [InlineData("25", "بیست و پنج")]
    [InlineData("100", "صد")]
    [InlineData("125", "صد و بیست و پنج")]
    [InlineData("1000", "هزار")]
    [InlineData("1100", "هزار و صد")]
    [InlineData("2001", "دو هزار و یک")]
    [InlineData("85000", "هشتاد و پنج هزار")]
    [InlineData("1000000", "یک میلیون")]
    [InlineData("1250000", "یک میلیون و دویست و پنجاه هزار")]
    [InlineData("10000000", "ده میلیون")]
    [InlineData("450000000", "چهارصد و پنجاه میلیون")]
    [InlineData("1000000000", "یک میلیارد")]
    [InlineData("38272700", "سی و هشت میلیون و دویست و هفتاد و دو هزار و هفتصد")]
    [InlineData("-15", "منفی پانزده")]
    [InlineData("2.5", "دو و پنج دهم")]
    [InlineData("0.25", "بیست و پنج صدم")]
    [InlineData("0.50", "پنج دهم")]
    [InlineData("3.125", "سه و صد و بیست و پنج هزارم")]
    [InlineData("1.0005", "یک و یک هزارم")] // بیش از سه رقم: دور از صفر گرد می‌شود
    [InlineData("0.0004", "صفر")]
    public void ToWords(string value, string expected)
        => Assert.Equal(expected, NumberWords.ToWords(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Money_AppendsTheCurrencyLabel()
    {
        Assert.Equal("ده میلیون تومان", NumberWords.Money(10_000_000m, "تومان"));
        Assert.Equal("هشتاد و پنج هزار ریال", NumberWords.Money(85_000m, "ریال"));
        Assert.Equal("صد", NumberWords.Money(100m, " "));
    }

    [Fact]
    public void BeyondQuadrillions_FallsBackToPersianDigits()
        => Assert.Equal(Messages.FormatNumber(1_000_000_000_000_000_000m), NumberWords.ToWords(1_000_000_000_000_000_000m));
}
