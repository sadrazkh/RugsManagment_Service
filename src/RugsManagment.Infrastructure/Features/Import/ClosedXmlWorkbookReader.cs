using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Import;
using RugsManagment.Domain.Common;
namespace RugsManagment.Infrastructure.Features.Import;

/// <summary>خواندن مستقیم cacheهای OpenXML برای حفظ اعشار و جلوگیری از محاسبهٔ فرمول‌های فایل واردشده.</summary>
public sealed class ClosedXmlWorkbookReader : IWorkbookReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public Task<IReadOnlyList<SheetGrid>> ReadAsync(Stream input, CancellationToken ct = default)
    {
        try
        {
            if (input.Length > XlsxSignature.MaxFileBytes) throw new DomainValidationException(ImportMessages.Text("E_FILE_TOO_LARGE"), "file");
            Span<byte> magic = stackalloc byte[4];
            if (input.Read(magic) != 4 || !XlsxSignature.IsZip(magic)) XlsxPackageInspector.Invalid();
            input.Position = 0;
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            XlsxPackageInspector.Validate(zip);
            var stringsEntry = zip.GetEntry("xl/sharedStrings.xml");
            var strings = stringsEntry is null ? [] : XlsxPackageInspector.Xml(stringsEntry).Descendants(Main + "si")
                .Select(s => string.Concat(s.Descendants(Main + "t").Select(t => t.Value))).ToArray();
            var workbook = XlsxPackageInspector.Xml(zip.GetEntry("xl/workbook.xml")!);
            var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (relsEntry is null) XlsxPackageInspector.Invalid();
            var rels = XlsxPackageInspector.Xml(relsEntry!).Root!.Elements().ToDictionary(e => (string)e.Attribute("Id")!, e => e);
            var sheets = new List<SheetGrid>();
            foreach (var sheet in workbook.Descendants(Main + "sheet"))
            {
                ct.ThrowIfCancellationRequested();
                var id = (string?)sheet.Attribute(Rel + "id");
                if (id is null || !rels.TryGetValue(id, out var relation) || (string?)relation.Attribute("TargetMode") == "External") XlsxPackageInspector.Invalid();
                var target = (string?)rels[id!].Attribute("Target") ?? "";
                var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                if (path.Contains("..", StringComparison.Ordinal)) XlsxPackageInspector.Invalid();
                var entry = zip.GetEntry(path);
                if (entry is null) XlsxPackageInspector.Invalid();
                var xml = XlsxPackageInspector.Xml(entry!);
                var cells = new List<SheetCell>();
                foreach (var cell in xml.Descendants(Main + "c"))
                {
                    var address = Address((string?)cell.Attribute("r") ?? "");
                    if (address.Row > 10000) throw new DomainValidationException(ImportMessages.Text("E_TOO_MANY_ROWS"), "file");
                    if (address.Column > 512 || address.Row < 1 || address.Column < 1) XlsxPackageInspector.Invalid();
                    var raw = (string?)cell.Element(Main + "v");
                    var type = (string?)cell.Attribute("t");
                    string? text = raw; decimal? number = null; var snapped = false;
                    if (type == "s")
                    {
                        if (!int.TryParse(raw, out var index) || index < 0 || index >= strings.Length) XlsxPackageInspector.Invalid();
                        text = strings[index];
                    }
                    else if (type == "inlineStr") text = string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
                    else if (type is null or "n" && decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    {
                        var rounded = Num.Round(n, 2);
                        snapped = n != rounded && decimal.Abs(n - rounded) <= 0.000000001m;
                        number = snapped ? rounded : n;
                        text = number.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    if (!string.IsNullOrEmpty(text)) cells.Add(new(address.Row, address.Column, text, number, snapped));
                }
                var merges = xml.Descendants(Main + "mergeCell").Select(e =>
                {
                    var range = ((string?)e.Attribute("ref") ?? "").Split(':');
                    var first = Address(range[0]); var last = Address(range[^1]);
                    if (last.Row > 10000 || last.Column > 512) throw new DomainValidationException(ImportMessages.Text("E_TOO_MANY_ROWS"), "file");
                    return new SheetMerge(first.Row, last.Row, first.Column, last.Column);
                }).ToArray();
                // برخی فایل‌ها مقدار پنهان هم در خانهٔ پوشیده ذخیره کرده‌اند؛ نباید رنگ یا شمارهٔ تکراری بسازد.
                cells.RemoveAll(c => merges.Any(m => c.Row >= m.FirstRow && c.Row <= m.LastRow && c.Column >= m.FirstColumn && c.Column <= m.LastColumn
                    && (c.Row != m.FirstRow || c.Column != m.FirstColumn)));
                sheets.Add(new((string?)sheet.Attribute("name") ?? "Sheet", cells.Select(c => c.Row).DefaultIfEmpty(0).Max(),
                    cells.Select(c => c.Column).DefaultIfEmpty(0).Max(), cells, merges));
            }
            if (sheets.Count == 0) XlsxPackageInspector.Invalid();
            return Task.FromResult<IReadOnlyList<SheetGrid>>(sheets);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or FormatException or OverflowException or ArgumentException)
        { throw new DomainValidationException(ImportMessages.Text("E_NOT_XLSX"), "file"); }
    }
    private static (int Row, int Column) Address(string address)
    {
        var column = 0; var index = 0;
        while (index < address.Length && address[index] is >= 'A' and <= 'Z') { column = checked(column * 26 + address[index] - 'A' + 1); index++; }
        return (int.TryParse(address.AsSpan(index), out var row) ? row : 0, column);
    }
}
