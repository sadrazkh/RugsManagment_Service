using System.Text.Json;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;

namespace RugsManagment.Tests.Documents;

/// <summary>
/// دادهٔ آزمون‌های موتور سند از رونویسی فایل نمونهٔ بازرگان (<c>tests/fixtures/sample-shipment.json</c>، spec E.10): همان
/// <see cref="DocumentData"/> که P6-wiring از دیتابیس پر می‌کند، این‌جا مستقیم از فایل ساخته می‌شود. جمع‌های دفتر هر نگله
/// (برای <c>checkAgainst</c>) از جدول خلاصهٔ همان فایل می‌آید.
/// </summary>
internal static class SampleDocuments
{
    /// <summary>تاریخ سند نمونه (۱۴۰۵/۰۶/۱۵).</summary>
    public static readonly DateOnly SampleDate = new(2026, 9, 6);

    public static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    /// <summary>کاتالوگ بی دیتابیس با دو فیلد سفارشی رنگ که قالب اظهار لازم دارد.</summary>
    public static IReadOnlyList<FieldDefDto> Catalogue(BaleTermWords? terms = null) => DocumentFields.Catalogue(
        [DocumentFields.CustomText("bg_color", "رنگ متن"), DocumentFields.CustomText("border_color", "رنگ حاشیه")], terms);

    public static DocumentLayout Declaration() => LayoutJson.Default("shipment-declaration").Parse();

    public static DocumentLayout RugList() => LayoutJson.Default("rug-list").Parse();

    /// <summary>سند اظهار با قالب پیش‌فرض.</summary>
    public static RenderModel Compose(DocumentData data, DocumentLayout? layout = null, ComposeOptions? options = null)
        => DocumentComposer.Compose(layout ?? Declaration(), data, options ?? new ComposeOptions(Fields: Catalogue(data.Terms)));

    /// <summary>
    /// محمولهٔ نمونه؛ <paramref name="bales"/> نگله‌ها را صاف می‌کند (۱ تا ۱۰ برای سند بی دسته = ردیف‌های ۱ تا ۴۳ فایل).
    /// </summary>
    public static DocumentData Shipment(Func<int, bool>? bales = null, BaleTermWords? terms = null, bool isDraft = false)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath("sample-shipment.json")));
        var root = document.RootElement;
        var ledger = root.GetProperty("summary").EnumerateArray().ToDictionary(
            e => e.GetProperty("bale").GetInt32(),
            e => (Pieces: e.GetProperty("pieces").GetInt32(), Area: e.GetProperty("areaM2").GetDecimal(), Usd: e.GetProperty("usd").GetDecimal()));

        var lines = root.GetProperty("lines").EnumerateArray().Select(e => (
            Bale: e.GetProperty("bale").GetInt32(),
            Row: e.GetProperty("row").GetInt32(),
            Rug: new DocumentRug(
                Id: null,
                Serial: e.GetProperty("serial").GetString(),
                Qty: e.GetProperty("qty").GetInt32(),
                Pattern: Text(e, "pattern"),
                Origin: Text(e, "origin"),
                Material: Text(e, "material"),
                IntakeLength: e.GetProperty("length").GetDecimal(),
                IntakeWidth: e.GetProperty("width").GetDecimal(),
                Custom: new Dictionary<string, string?> { ["bg_color"] = Text(e, "bg_color"), ["border_color"] = Text(e, "border_color") }),
            Length: e.GetProperty("length").GetDecimal(),
            Width: e.GetProperty("width").GetDecimal(),
            Rate: e.GetProperty("usdPerM2").GetDecimal())).ToList();

        var result = lines
            .GroupBy(l => l.Bale)
            .Where(g => bales is null || bales(g.Key))
            .OrderBy(g => g.Key)
            .Select(g => new DocumentBale(
                g.Key,
                g.OrderBy(l => l.Row).Select((l, i) => new DocumentLine(i + 1, l.Rug, l.Length, l.Width, UsdPerM2: l.Rate)).ToList(),
                ledger[g.Key].Pieces,
                ledger[g.Key].Area,
                ledger[g.Key].Usd))
            .ToList();

        return new DocumentData(
            new DocumentShipment(null, 1, null, SampleDate, null, null, null, null, null),
            result,
            [],
            terms ?? BaleTerms.Negleh,
            new DocumentTenant(),
            isDraft);
    }

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>همهٔ خانه‌های یک برگه با نشانی.</summary>
    public static IReadOnlyDictionary<string, RenderCell> Cells(RenderSheet sheet)
        => sheet.Rows.SelectMany(r => r.Cells).ToDictionary(c => c.Address, StringComparer.Ordinal);

    /// <summary>ردیفِ دادهٔ یک شماره (B).</summary>
    public static RenderRow RowOfSerial(RenderSheet sheet, string serial)
        => sheet.Rows.Single(r => r.Kind == RenderRowKinds.Data && r.Cells.Any(c => c.Col == 2 && c.Value.AsText() == serial));
}
