using System.Security.Cryptography;
using System.Text;
using RugsManagment.Application.Common;
namespace RugsManagment.Application.Features.Import;

public static class HeaderDictionary
{
    private static readonly Dictionary<string, string> Headers = new()
    {
        ["نگله"] = "bale", ["عدل"] = "bale", ["شماره"] = "serial", ["سریال"] = "serial", ["کد"] = "serial",
        ["رنگ متن"] = "bg_color", ["رنگ زمینه"] = "bg_color", ["رنگ حاشیه"] = "border_color",
        ["نقشه"] = "pattern", ["طرح"] = "pattern", ["محل بافت"] = "origin", ["نوع"] = "origin",
        ["مواد مصرفی"] = "material", ["جنس"] = "material", ["تعداد"] = "qty", ["طول"] = "length",
        ["عرض"] = "width", ["مساحت"] = "area", ["متراژ"] = "area",
        ["ارزش متر مربع"] = "usdPerM2", ["جمع کل دلار"] = "usd"
    };
    public static Dictionary<string, int> Map(SheetGrid sheet, int row)
    {
        var result = new Dictionary<string, int>();
        foreach (var c in sheet.Cells.Where(c => c.Row == row))
            if (Headers.TryGetValue(PersianText.Normalize(c.Text), out var field)) result.TryAdd(field, c.Column);
        return result;
    }
    public static int? Detect(SheetGrid sheet)
        => Enumerable.Range(1, Math.Min(30, sheet.LastRow)).Cast<int?>().FirstOrDefault(r => Map(sheet, r!.Value).Count >= 5);
    public static string Signature(SheetGrid sheet, int row)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",
            sheet.Cells.Where(c => c.Row == row).OrderBy(c => c.Column).Select(c => PersianText.Normalize(c.Text))))));
}
