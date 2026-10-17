using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
namespace RugsManagment.IntegrationTests.P8.Fixtures;

/// <summary>نمونهٔ مصنوعی با همان بی‌نظمی‌های اکسل؛ هیچ فایل شخصی وارد مخزن نمی‌شود.</summary>
public static class SampleSheetBuilder
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static MemoryStream Build()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-shipment.json")));
        var rugs = fixture.RootElement.GetProperty("lines").EnumerateArray().ToArray();
        var cells = new SortedDictionary<int, SortedDictionary<int, object>>();
        var merges = new List<string>();
        void Set(int row, int col, object value) { if (!cells.TryGetValue(row, out var line)) cells[row] = line = new(); line[col] = value; }
        void Title(int row) => Set(row, 1, "صورت نگله بندی و اظهار فرش صادراتی تاریخ 1405.06.15");
        void Header(int row)
        {
            var headers = new[] { "نگله", "شماره", "رنگ متن", "رنگ حاشیه", "نقشه", "محل بافت", "مواد مصرفی", "طول", "عرض", "مساحت", "ارزش متر مربع", "جمع کل دلار" };
            for (var c = 0; c < headers.Length; c++) Set(row, c + 1, headers[c]);
        }
        Title(1); Header(2);
        foreach (var row in new[] { 46, 87, 130, 173, 216 }) { Title(row); Header(row + 1); }
        Header(226);
        foreach (var rug in rugs)
        {
            var r = rug.GetProperty("row").GetInt32();
            Set(r, 2, rug.GetProperty("serial").GetString()!);
            foreach (var (field, col) in new[] { ("bg_color", 3), ("border_color", 4), ("pattern", 5), ("origin", 6), ("material", 7) })
                if (rug.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String) Set(r, col, value.GetString()!);
            Set(r, 8, rug.GetProperty("length").GetDecimal()); Set(r, 9, rug.GetProperty("width").GetDecimal());
            Set(r, 10, rug.GetProperty("cachedArea").GetDecimal()); Set(r, 11, rug.GetProperty("usdPerM2").GetDecimal()); Set(r, 12, rug.GetProperty("cachedUsd").GetDecimal());
            if (rug.GetProperty("qty").GetInt32() > 1)
            {
                cells[r].Remove(3); cells[r].Remove(6); Set(r, 5, "گبه پشم"); Set(r, 7, rug.GetProperty("qty").GetInt32());
                merges.Add($"B{r}:C{r}"); merges.Add($"E{r}:F{r}");
            }
        }
        foreach (var group in rugs.GroupBy(r => r.GetProperty("bale").GetInt32()))
        {
            var first = group.Min(r => r.GetProperty("row").GetInt32()); var last = group.Max(r => r.GetProperty("row").GetInt32());
            var start = first == 192 ? 193 : first;
            Set(start, 1, group.Key);
            if (last > start) merges.Add($"A{start}:A{last}");
            var footer = last + 1;
            Set(footer, 1, "نگله شماره"); Set(footer, 3, footer == 210 ? 26 : group.Key);
            Set(footer, 4, "شامل"); Set(footer, 5, group.Sum(r => r.GetProperty("qty").GetInt32())); Set(footer, 6, "تخته");
            var area = group.Sum(r => r.GetProperty("cachedArea").GetDecimal() * r.GetProperty("qty").GetInt32());
            var usd = group.Sum(r => r.GetProperty("cachedUsd").GetDecimal() * r.GetProperty("qty").GetInt32());
            Set(footer, 9, group.Key == 1 ? area + 0.000000000000002m : area); Set(footer, 11, usd);
            var summary = 261 + group.Key;
            Set(summary, 4, group.Key); Set(summary, 5, group.Sum(r => r.GetProperty("qty").GetInt32()));
            Set(summary, 6, area); Set(summary, 7, usd + 0.000000000000004m);
        }
        Set(261, 4, "نگله"); Set(261, 5, "تعداد فرش"); Set(261, 6, "متراژ"); Set(261, 7, "ارزش گمرکی");
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(text); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/></Types>");
            Entry("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\"/></Relationships>");
            var xml = new XElement(Ns + "worksheet", new XElement(Ns + "sheetData", cells.Select(row =>
                new XElement(Ns + "row", new XAttribute("r", row.Key), row.Value.Select(c => c.Value is string str
                    ? new XElement(Ns + "c", new XAttribute("r", Column(c.Key) + row.Key), new XAttribute("t", "inlineStr"), new XElement(Ns + "is", new XElement(Ns + "t", str)))
                    : new XElement(Ns + "c", new XAttribute("r", Column(c.Key) + row.Key), new XElement(Ns + "v", Convert.ToString(c.Value, CultureInfo.InvariantCulture))))))),
                new XElement(Ns + "mergeCells", merges.Select(m => new XElement(Ns + "mergeCell", new XAttribute("ref", m)))));
            Entry("xl/worksheets/sheet1.xml", xml.ToString(SaveOptions.DisableFormatting));
        }
        output.Position = 0; return output;
    }
    private static string Column(int n) => n <= 26 ? ((char)('A' + n - 1)).ToString() : throw new ArgumentOutOfRangeException(nameof(n));
}
