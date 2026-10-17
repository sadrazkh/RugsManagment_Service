using System.Globalization;
using System.Text;
namespace RugsManagment.Web.Support;
/// <summary>اعداد منفی عدد می‌مانند؛ فقط متنِ شبیه فرمول از اجرای اکسل محافظت می‌شود.</summary>
public sealed class CsvWriter
{
    private readonly StringBuilder _buffer = new("sep=,\r\n");
    public CsvWriter AddRow(params object?[] values) { _buffer.Append(Row(values)).Append("\r\n"); return this; }
    public byte[] ToBytes() => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(_buffer.ToString())];
    public static string Cell(object? value)
    {
        var numeric = value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal;
        var text = value switch {
            DateTimeOffset date => PersianFormat.Date(date), bool flag => flag ? "بله" : "خیر",
            decimal amount => amount.ToString("0.##", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null,CultureInfo.InvariantCulture), _ => value?.ToString() ?? ""
        };
        if (!numeric && text.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') text = "'" + text;
        return text.IndexOfAny([',','"','\r','\n']) >= 0 ? "\"" + text.Replace("\"","\"\"",StringComparison.Ordinal) + "\"" : text;
    }
    public static string Row(IEnumerable<object?> values) => string.Join(",",values.Select(Cell));
}
