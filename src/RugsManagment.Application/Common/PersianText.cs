namespace RugsManagment.Application.Common;

/// <summary>
/// یکسان‌سازی متن فارسی برای جستجو.
///
/// چرا لازم است: کاربران بسته به کیبورد و منبع کپی، «ی» را گاهی عربی (ي) و «ک» را
/// گاهی عربی (ك) تایپ می‌کنند؛ همچنین ممکن است عدد را فارسی بنویسند. بدون یکسان‌سازی،
/// جستجوی «کاشان» فرشی که با «كاشان» ثبت شده را پیدا نمی‌کند.
///
/// همین تبدیل‌ها در کوئری روی ستون‌های دیتابیس هم اعمال می‌شود (RugRepository)
/// تا دو طرف مقایسه هم‌شکل باشند.
/// </summary>
public static class PersianText
{
    // به‌صورت رشته تعریف شده‌اند نه char: فقط string.Replace(string, string)
    // توسط Npgsql به تابع replace() پستگرس ترجمه می‌شود — نسخهٔ char ترجمه نمی‌شود.

    /// <summary>حرف عربیِ «ی» که باید به فارسی تبدیل شود.</summary>
    public const string ArabicYeh = "ي";
    public const string PersianYeh = "ی";

    /// <summary>حرف عربیِ «ک» که باید به فارسی تبدیل شود.</summary>
    public const string ArabicKaf = "ك";
    public const string PersianKaf = "ک";

    /// <summary>
    /// ارقام فارسی/عربی را به لاتین و حروف عربی را به فارسی تبدیل می‌کند،
    /// فاصله‌های اضافه را جمع می‌کند و «نیم‌فاصله» را به فاصلهٔ ساده می‌برد.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var buffer = new System.Text.StringBuilder(input.Length);
        var lastWasSpace = false;

        foreach (var ch in input.Trim())
        {
            var mapped = ch switch
            {
                'ي' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                'ۀ' => 'ه',
                >= '۰' and <= '۹' => (char)(ch - '۰' + '0'),   // ارقام فارسی
                >= '٠' and <= '٩' => (char)(ch - '٠' + '0'),   // ارقام عربی
                '‌' => ' ',                                // نیم‌فاصله
                _ => ch
            };

            if (char.IsWhiteSpace(mapped))
            {
                if (lastWasSpace) continue;
                lastWasSpace = true;
                buffer.Append(' ');
            }
            else
            {
                lastWasSpace = false;
                buffer.Append(mapped);
            }
        }

        return buffer.ToString().Trim();
    }
}
