using System.Globalization;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.RugDetails;

/// <summary>
/// قالب‌های کوچک صفحه‌های فرش (جزئیات، فهرست، سطل زباله): اندازه با «٫»، متراژ دسته به شکل «۰٫۱۶ × ۵۰ = ۸٫۰۰»، و برچسب
/// جای فرش. همه با فرهنگ ثابت و بعد ارقام فارسی (هرگز فرهنگ fa که علامت منفی‌اش ASCII نیست).
/// </summary>
public static class RugDetailsFormat
{
    /// <summary>ضلع یا متراژ با دو رقم اعشار: «۲٫۰۸».</summary>
    public static string Decimal2(decimal value)
        => PersianFormat.ToPersianDigits(value.ToString("0.00", CultureInfo.InvariantCulture)).Replace('.', '٫');

    /// <summary>اندازه به شکل «طول × عرض»: «۲٫۰۸ × ۲٫۱۰».</summary>
    public static string Size(decimal length, decimal width) => $"{Decimal2(length)} × {Decimal2(width)}";

    /// <summary>
    /// متراژ ردیف: برای فرش تک «۴٫۳۷»، برای دسته حساب کامل «۰٫۱۶ × ۵۰ = ۸٫۰۰» (همان شکلی که جدول ورود نشان می‌دهد، G.2) تا
    /// بازرگان ببیند متراژ دسته از کجا آمد.
    /// </summary>
    public static string Area(decimal pieceArea, int quantity, decimal totalArea)
        => quantity <= 1
            ? Decimal2(pieceArea)
            : $"{Decimal2(pieceArea)} × {PersianFormat.Count(quantity)} = {Decimal2(totalArea)}";

    /// <summary>
    /// برچسب جای فرش (C.10). «در نگله» واژهٔ کارگاه را می‌گیرد (نگله یا عدل).
    /// </summary>
    public static string LocationLabel(RugLocation location, string bale = "نگله") => location switch
    {
        RugLocation.InStock => "در انبار",
        RugLocation.InBale => $"در {bale}",
        RugLocation.Exported => "صادر شده",
        RugLocation.Sold => "فروخته‌شده",
        RugLocation.Trashed => "در سطل زباله",
        _ => "—"
    };

    /// <summary>نماد جای فرش در icons.svg.</summary>
    public static string LocationIcon(RugLocation location) => location switch
    {
        RugLocation.InStock => "warehouse",
        RugLocation.InBale => "bale",
        RugLocation.Exported => "truck",
        RugLocation.Sold => "handshake",
        RugLocation.Trashed => "trash",
        _ => "info"
    };
}
