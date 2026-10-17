using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;
using RugsManagment.Infrastructure.Documents;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace RugsManagment.IntegrationTests.Documents;

/// <summary>
/// سندهای نمونه برای آزمون فایل xlsx (spec E.10): همان دادهٔ <c>tests/fixtures/sample-shipment.json</c> که آزمون‌های واحد
/// موتور می‌خوانند، این بار تا خود فایل نوشته و دوباره خوانده می‌شود. پایگاه داده لازم نیست.
///
/// اگر متغیر محیطی <c>RUGS_DOC_SAMPLES</c> پوشه‌ای را نشان دهد، فایل‌های ساخته‌شده آن‌جا هم ذخیره می‌شوند تا با اکسل باز و
/// دیده شوند.
/// </summary>
internal static class XlsxSamples
{
    public static readonly DateOnly SampleDate = new(2026, 9, 6);

    public static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    public static IReadOnlyList<FieldDefDto> Catalogue(BaleTermWords? terms = null) => DocumentFields.Catalogue(
        [DocumentFields.CustomText("bg_color", "رنگ متن"), DocumentFields.CustomText("border_color", "رنگ حاشیه")], terms);

    public static RenderModel Compose(DocumentData data, DocumentLayout? layout = null)
        => DocumentComposer.Compose(layout ?? LayoutJson.Default("shipment-declaration").Parse(), data, new ComposeOptions(Fields: Catalogue(data.Terms)));

    public static DocumentData Shipment(Func<int, bool>? bales = null, BaleTermWords? terms = null, bool isDraft = false)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath("sample-shipment.json")));
        var root = document.RootElement;
        var ledger = root.GetProperty("summary").EnumerateArray().ToDictionary(
            e => e.GetProperty("bale").GetInt32(),
            e => (Pieces: e.GetProperty("pieces").GetInt32(), Area: e.GetProperty("areaM2").GetDecimal(), Usd: e.GetProperty("usd").GetDecimal()));

        string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var lines = root.GetProperty("lines").EnumerateArray().Select(e => (
            Bale: e.GetProperty("bale").GetInt32(),
            Row: e.GetProperty("row").GetInt32(),
            Rug: new DocumentRug(null, e.GetProperty("serial").GetString(), e.GetProperty("qty").GetInt32(),
                Pattern: Text(e, "pattern"), Origin: Text(e, "origin"), Material: Text(e, "material"),
                IntakeLength: e.GetProperty("length").GetDecimal(), IntakeWidth: e.GetProperty("width").GetDecimal(),
                Custom: new Dictionary<string, string?> { ["bg_color"] = Text(e, "bg_color"), ["border_color"] = Text(e, "border_color") }),
            Length: e.GetProperty("length").GetDecimal(),
            Width: e.GetProperty("width").GetDecimal(),
            Rate: e.GetProperty("usdPerM2").GetDecimal())).ToList();

        var result = lines.GroupBy(l => l.Bale)
            .Where(g => bales is null || bales(g.Key))
            .OrderBy(g => g.Key)
            .Select(g => new DocumentBale(g.Key,
                g.OrderBy(l => l.Row).Select((l, i) => new DocumentLine(i + 1, l.Rug, l.Length, l.Width, UsdPerM2: l.Rate)).ToList(),
                ledger[g.Key].Pieces, ledger[g.Key].Area, ledger[g.Key].Usd))
            .ToList();

        return new DocumentData(new DocumentShipment(null, 1, null, SampleDate, null, null, null, null, null),
            result, [], terms ?? BaleTerms.Negleh, new DocumentTenant(), isDraft);
    }

    /// <summary>سند را با نویسندهٔ واقعی می‌نویسد و بایت‌های فایل را برمی‌گرداند (و اگر خواسته شده، ذخیره می‌کند).</summary>
    public static async Task<byte[]> WriteAsync(RenderModel model, string sampleName)
    {
        using var stream = new MemoryStream();
        await new ClosedXmlDocumentWriter().WriteAsync(model, stream);
        var bytes = stream.ToArray();
        if (Environment.GetEnvironmentVariable("RUGS_DOC_SAMPLES") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(Path.Combine(dir, sampleName + ".xlsx"), bytes);
        }

        return bytes;
    }

    /// <summary>بستهٔ OpenXml فقط‌خواندنی روی بایت‌ها.</summary>
    public static SpreadsheetDocument Open(byte[] bytes) => SpreadsheetDocument.Open(new MemoryStream(bytes), false);

    /// <summary>بخش برگه با نام.</summary>
    public static S.Worksheet Sheet(SpreadsheetDocument document, string name)
    {
        var workbook = document.WorkbookPart!;
        var sheet = workbook.Workbook.Sheets!.Elements<S.Sheet>().Single(s => s.Name == name);
        return ((WorksheetPart)workbook.GetPartById(sheet.Id!)).Worksheet;
    }

    /// <summary>خانه‌های خام برگه با نشانی.</summary>
    public static Dictionary<string, S.Cell> RawCells(S.Worksheet sheet)
        => sheet.Descendants<S.Cell>().ToDictionary(c => c.CellReference!.Value!, StringComparer.Ordinal);
}
