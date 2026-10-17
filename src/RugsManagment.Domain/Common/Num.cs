namespace RugsManagment.Domain.Common;

/// <summary>
/// گردکردن و تمیزکاری اعداد — تنها جای گردکردن در کد سرور.
///
/// چرا جدا: <c>Math.Round</c> بدون آرگومان «بانکی» (ToEven) گرد می‌کند، ولی اکسلِ بازرگان (ROUND)،
/// پستگرس (<c>round(numeric, n)</c>) و فرانت (<c>lib/num.ts</c>) نصفه را «دور از صفر» گرد می‌کنند.
/// اگر حتی یک جا ToEven بماند، متراژ ۲٫۸۹۵ یک‌جا ۲٫۸۹ و جای دیگر ۲٫۹۰ می‌شود و جمع اظهارنامه با اکسل
/// نمی‌خواند. پس همه‌جا فقط از این کلاس گرد کنید.
/// </summary>
public static class Num
{
    /// <summary>گرد به <paramref name="decimals"/> رقم اعشار، نصفه همیشه دور از صفر (۲٫۶۷۵ → ۲٫۶۸ و −۲٫۶۷۵ → −۲٫۶۸).</summary>
    public static decimal Round(decimal value, int decimals)
        => Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>همان <see cref="Round(decimal, int)"/> برای مقدار اختیاری؛ null همان null می‌ماند.</summary>
    public static decimal? Round(decimal? value, int decimals)
        => value is { } v ? Round(v, decimals) : null;

    /// <summary>
    /// عددی را که از اکسل یا JSON به‌صورت double آمده (مثل 2.2999999999999998) به decimal تمیز
    /// برمی‌گرداند: اول تبدیل استاندارد (۱۵ رقم معنادار، که خطای ممیز شناور را می‌بُرد) و بعد گرد به
    /// <paramref name="decimals"/> رقم. بدون این، 2.2999999999999998 × 1.65 به‌جای ۳٫۸۰ می‌شد ۳٫۷۹.
    /// </summary>
    public static decimal Snap(double value, int decimals = 2)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "عدد نامعتبر است.");

        return Round((decimal)value, decimals);
    }

    /// <summary>آیا عدد بیش از <paramref name="decimals"/> رقم اعشارِ غیرصفر ندارد (۲٫۰۸ برای ۲ رقم بله، ۲٫۰۸۱ نه).</summary>
    public static bool HasAtMostDecimals(decimal value, int decimals)
        => decimal.Round(value, decimals) == value;
}
