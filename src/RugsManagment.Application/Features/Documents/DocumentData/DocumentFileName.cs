using System.Globalization;
using System.Text;
using RugsManagment.Application.Features.Documents.Expressions;

namespace RugsManagment.Application.Features.Documents.Data;

/// <summary>
/// نام فایل دانلود سند (spec E.7 «Download name»):
/// <c>Content-Disposition: attachment; filename="declaration-{number}-{yyyy-MM-dd}.xlsx"; filename*=UTF-8''اظهار-{number}-{jdot}[-پیش‌نویس].xlsx</c>.
///
/// چرا دو نام: مرورگرهای قدیمی و بعضی برنامه‌ها فقط <c>filename</c> اَسکی را می‌فهمند؛ بقیه نام فارسی RFC 5987 را
/// (<c>filename*</c>) ترجیح می‌دهند. پسوند «پیش‌نویس» فقط روی پیش‌نویس می‌آید تا فایل قطعی با پیش‌نویس قاطی نشود.
/// </summary>
public static class DocumentFileName
{
    /// <summary>پسوند نام فایل پیش‌نویس.</summary>
    public const string DraftSuffix = "پیش‌نویس";

    /// <summary>نام اَسکی: <c>declaration-12-2026-09-06.xlsx</c>.</summary>
    public static string Ascii(string prefix, int number, DateOnly date)
        => string.Create(CultureInfo.InvariantCulture, $"{prefix}-{number}-{date:yyyy-MM-dd}.xlsx");

    /// <summary>نام فارسی: <c>اظهار-12-1405.06.15-پیش‌نویس.xlsx</c>.</summary>
    public static string Persian(string prefix, int number, DateOnly date, bool isDraft)
        => string.Create(CultureInfo.InvariantCulture, $"{prefix}-{number}-{Evaluator.JalaliDot(date)}") + (isDraft ? "-" + DraftSuffix : "") + ".xlsx";

    /// <summary>سرآیند کامل <c>Content-Disposition</c> با <c>filename</c> و <c>filename*</c> (RFC 6266 / RFC 5987).</summary>
    public static string ContentDisposition(string asciiName, string unicodeName)
        => "attachment; filename=\"" + asciiName.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
           + "\"; filename*=UTF-8''" + EncodeRfc5987(unicodeName);

    /// <summary>
    /// کدگذاری RFC 5987 (<c>ext-value</c>): هر بایت UTF-8 بیرون از <c>attr-char</c> به‌صورت <c>%XX</c> (حروف بزرگ). نیم‌فاصلهٔ
    /// «پیش‌نویس» (U+200C) هم <c>%E2%80%8C</c> می‌شود و گم نمی‌شود.
    /// </summary>
    public static string EncodeRfc5987(string value)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (b < 0x80 && (char.IsAsciiLetterOrDigit(c) || "!#$&+-.^_`|~".Contains(c, StringComparison.Ordinal)))
                sb.Append(c);
            else
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
