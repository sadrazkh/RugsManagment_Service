using System.Globalization;
using System.Text;

namespace RugsManagment.Domain.Common;

/// <summary>
/// کمک‌کار کوچک برای پیام‌های فارسی خودِ دامنه و یکسان‌سازی رقم‌ها.
///
/// چرا این‌جا و نه <c>Messages.fa</c>: دامنه به لایهٔ Application دسترسی ندارد، ولی استثناهایش (۴۰۰/۴۰۹) مستقیم
/// به کاربر می‌رسند؛ پس متن پیام همین‌جا ساخته می‌شود — با همان شکل عددی که <c>Messages.Fill</c> می‌سازد (ارقام
/// فارسی، جداکنندهٔ «٬»، فرهنگ Invariant و هرگز fa که علامت منفی‌اش ASCII نیست).
/// </summary>
internal static class DomainText
{
    /// <summary>عدد صحیح برای متن فارسی: ۱۰٬۰۰۰.</summary>
    public static string Number(long value)
    {
        var invariant = value.ToString("#,0", CultureInfo.InvariantCulture);
        var builder = new StringBuilder(invariant.Length);
        foreach (var c in invariant)
        {
            builder.Append(c switch
            {
                >= '0' and <= '9' => (char)(c - '0' + '۰'),
                ',' => '٬',
                _ => c
            });
        }

        return builder.ToString();
    }

    /// <summary>ارقام لاتینِ یک متن را فارسی می‌کند (برای نشان دادن شماره‌ای مثل «3911 - 3960» در پیام).</summary>
    public static string PersianDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
            builder.Append(c is >= '0' and <= '9' ? (char)(c - '0' + '۰') : c);
        return builder.ToString();
    }

    /// <summary>
    /// ارقام فارسی (۰–۹) و عربی (٠–٩) را لاتین می‌کند. دیتابیس شماره‌ها را با ارقام لاتین نگه می‌دارد (spec B.1)
    /// تا جست‌وجو و ایندکس یکتا هر دو شکلِ تایپ‌شده را یکی ببینند.
    /// </summary>
    public static char LatinDigit(char c) => c switch
    {
        >= '۰' and <= '۹' => (char)(c - '۰' + '0'),
        >= '٠' and <= '٩' => (char)(c - '٠' + '0'),
        _ => c
    };
}
