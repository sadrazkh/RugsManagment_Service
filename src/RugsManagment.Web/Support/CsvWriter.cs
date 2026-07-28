using System.Text;

namespace RugsManagment.Web.Support;

/// <summary>
/// تولید فایل CSV سازگار با اکسل فارسی.
///
/// دو نکته که بدون آن‌ها اکسل فایل را خراب نشان می‌دهد:
///   • BOM یونیکد در ابتدای فایل — وگرنه اکسل ویندوز متن فارسی را به‌صورت «Ø§Ø¨» می‌خواند.
///   • جداکنندهٔ اعلام‌شده با «sep=,» در خط اول — وگرنه اکسل در برخی locale‌ها
///     سمی‌کالن انتظار دارد و همهٔ ستون‌ها در یک ستون می‌افتند.
///
/// اعداد عمداً با ارقام لاتین نوشته می‌شوند تا اکسل بتواند رویشان محاسبه کند؛
/// ارقام فارسی برای نمایش است، نه برای فایل داده.
/// </summary>
public sealed class CsvWriter
{
    private readonly StringBuilder _buffer = new();

    public CsvWriter()
    {
        // اعلام جداکننده برای اکسل
        _buffer.Append("sep=,\r\n");
    }

    public CsvWriter AddRow(params object?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0) _buffer.Append(',');
            _buffer.Append(Escape(values[i]));
        }

        _buffer.Append("\r\n");
        return this;
    }

    /// <summary>بایت‌های نهایی فایل، با BOM.</summary>
    public byte[] ToBytes()
    {
        var content = Encoding.UTF8.GetBytes(_buffer.ToString());
        var bom = Encoding.UTF8.GetPreamble();

        var result = new byte[bom.Length + content.Length];
        bom.CopyTo(result, 0);
        content.CopyTo(result, bom.Length);
        return result;
    }

    /// <summary>
    /// مقدار را برای CSV امن می‌کند.
    /// مقادیری که با = یا + یا - یا @ شروع شوند با یک آپاستروف خنثی می‌شوند تا
    /// اکسل آن‌ها را فرمول اجرا نکند (تزریق فرمول در CSV).
    /// </summary>
    private static string Escape(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            decimal d => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dt => PersianFormat.Date(dt),
            bool b => b ? "بله" : "خیر",
            _ => value.ToString() ?? string.Empty
        };

        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
            text = "'" + text;

        var needsQuotes = text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r');
        return needsQuotes ? '"' + text.Replace("\"", "\"\"") + '"' : text;
    }
}
