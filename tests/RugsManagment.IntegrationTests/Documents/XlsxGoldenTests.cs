using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Features.Documents.Layout;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace RugsManagment.IntegrationTests.Documents;

/// <summary>
/// آزمون طلایی فایل xlsx (spec E.10 #1 و #2، E.6 #2 و #4، K.2/P6): نگله‌های ۱ تا ۱۰ نمونه با نویسندهٔ واقعی (ClosedXML +
/// تزریق‌گر) نوشته و با ClosedXML و OpenXml دوباره خوانده می‌شوند و خانه به خانه با رونویسی ردیف‌های ۱ تا ۴۳ فایل
/// بازرگان (<c>sample-rows-1-43.expected.json</c>) مقایسه می‌شوند: مقدار ذخیره‌شده (دقیق، بی نویز ممیز شناور)، فرمول،
/// ادغام، قلم، رنگ، زمینه، کادر، تراز، قالب عدد، پهنای ستون، ارتفاع ردیف، تنظیم صفحه و نما.
///
/// قاعده‌های مقایسه و تفاوت‌های عمدی همان E.10: فرمول‌های مشترک نمونه باز شده‌اند؛ کادر خانه‌های ادغام‌شده با لبهٔ بیرونی؛
/// پانویس E <c>=ROWS(…)</c>؛ ارتفاع صریح ردیف‌ها؛ نام برگه «اظهار»؛ نام تعریف‌شدهٔ عنوان‌های تکرارشونده؛ <c>shrinkToFit</c>
/// روی خانه‌های داده.
/// </summary>
public partial class XlsxGoldenTests
{
    private static readonly JsonDocument Expected = JsonDocument.Parse(File.ReadAllText(XlsxSamples.FixturePath("sample-rows-1-43.expected.json")));

    private static Task<byte[]> LotFree() => XlsxSamples.WriteAsync(XlsxSamples.Compose(XlsxSamples.Shipment(b => b <= 10)), "golden-bales-1-10");

    private static Task<byte[]> Full(bool draft = false)
        => XlsxSamples.WriteAsync(XlsxSamples.Compose(XlsxSamples.Shipment(isDraft: draft)), draft ? "full-sample-draft" : "full-sample");

    [Fact]
    public async Task LotFree_EveryCell_MatchesTheSample_AfterReadBack()
    {
        var bytes = await LotFree();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("اظهار");
        using var document = XlsxSamples.Open(bytes);
        var raw = XlsxSamples.RawCells(XlsxSamples.Sheet(document, "اظهار"));

        var mergedRanges = ws.MergedRanges.ToList();
        var footerRows = new HashSet<int> { 7, 11, 14, 19, 22, 25, 30, 34, 39, 43 };
        var mismatches = new List<string>();

        foreach (var e in Expected.RootElement.GetProperty("cells").EnumerateArray())
        {
            var reference = e.GetProperty("ref").GetString()!;
            var type = e.GetProperty("type").GetString()!;
            var cell = ws.Cell(reference);
            var row = cell.Address.RowNumber;
            var merged = mergedRanges.Any(r => r.Contains(cell));

            // مقدار
            switch (type)
            {
                case "n":
                {
                    var expected = e.GetProperty("value").GetDecimal();
                    if (!cell.CachedValue.IsNumber || (decimal)cell.CachedValue.GetNumber() != expected)
                        mismatches.Add($"{reference}: cached {cell.CachedValue} ≠ {expected}");
                    // دقیق: <v> همان متن General، بی نویز
                    var v = raw.TryGetValue(reference, out var rc) ? rc.CellValue?.Text : null;
                    if (v != DocNumber.General(expected))
                        mismatches.Add($"{reference}: <v>{v}</v> ≠ {DocNumber.General(expected)}");
                    break;
                }
                case "s":
                    if (!cell.CachedValue.IsText || cell.CachedValue.GetText() != e.GetProperty("value").GetString())
                        mismatches.Add($"{reference}: text «{cell.CachedValue}» ≠ «{e.GetProperty("value").GetString()}»");
                    break;
                default:
                    if (!cell.CachedValue.IsBlank)
                        mismatches.Add($"{reference}: expected blank, got {cell.CachedValue}");
                    break;
            }

            // فرمول
            var formula = e.TryGetProperty("formula", out var f) ? f.GetString() : null;
            var actual = cell.HasFormula ? cell.FormulaA1 : null;
            if (footerRows.Contains(row) && reference.StartsWith('E') && formula is null)
            {
                if (actual is null || !RowsPattern().IsMatch(actual))
                    mismatches.Add($"{reference}: formula {actual} is not ROWS(B…)");
            }
            else if (!string.Equals(formula, actual, StringComparison.Ordinal))
            {
                mismatches.Add($"{reference}: formula «{actual}» ≠ «{formula}»");
            }

            if (cell.Style.IncludeQuotePrefix)
                mismatches.Add($"{reference}: unexpected quote prefix");

            // سبک
            var style = e.GetProperty("style");
            var dataRow = row >= 3 && !footerRows.Contains(row);
            CompareStyle(reference, style, cell.Style, shrinkExpected: dataRow, compareBorders: !merged, mismatches);
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(60)));
    }

    [Fact]
    public async Task LotFree_Merges_AndTheirOuterBorders_MatchTheSample()
    {
        var bytes = await LotFree();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("اظهار");
        var expected = Expected.RootElement.GetProperty("merges").EnumerateArray().Select(m => m.GetString()!).Order().ToList();
        Assert.Equal(expected, ws.MergedRanges.Select(r => r.RangeAddress.ToString()!).Order().ToList());

        foreach (var merge in Expected.RootElement.GetProperty("mergeOuterBorders").EnumerateObject())
        {
            var range = ws.Range(merge.Name);
            var edges = new Dictionary<string, IEnumerable<XLBorderStyleValues>>
            {
                ["left"] = range.Cells().Where(c => c.Address.ColumnNumber == range.RangeAddress.FirstAddress.ColumnNumber).Select(c => c.Style.Border.LeftBorder),
                ["right"] = range.Cells().Where(c => c.Address.ColumnNumber == range.RangeAddress.LastAddress.ColumnNumber).Select(c => c.Style.Border.RightBorder),
                ["top"] = range.Cells().Where(c => c.Address.RowNumber == range.RangeAddress.FirstAddress.RowNumber).Select(c => c.Style.Border.TopBorder),
                ["bottom"] = range.Cells().Where(c => c.Address.RowNumber == range.RangeAddress.LastAddress.RowNumber).Select(c => c.Style.Border.BottomBorder)
            };
            foreach (var side in merge.Value.EnumerateObject())
            {
                var want = side.Value.GetString() == "thin" ? XLBorderStyleValues.Thin : XLBorderStyleValues.None;
                Assert.All(edges[side.Name], b => Assert.Equal(want, b));
            }
        }
    }

    [Fact]
    public async Task LotFree_Columns_Rows_PageSetup_AndView_MatchTheSample()
    {
        var bytes = await LotFree();
        using var document = XlsxSamples.Open(bytes);
        var sheet = XlsxSamples.Sheet(document, "اظهار");
        var root = Expected.RootElement;

        // پهنای دقیق و سبک ستون‌ها، و ستون‌های بعدی با پهنای پیش‌فرض
        var cols = sheet.GetFirstChild<S.Columns>()!.Elements<S.Column>().ToList();
        foreach (var expected in root.GetProperty("columns").EnumerateArray())
        {
            var min = (uint)expected.GetProperty("min").GetInt32();
            var max = (uint)expected.GetProperty("max").GetInt32();
            var col = cols.SingleOrDefault(c => c.Min!.Value == min && c.Max!.Value == max);
            Assert.True(col is not null, $"<col min={min} max={max}> is missing");
            Assert.Equal(expected.GetProperty("width").GetDouble(), col!.Width!.Value);
            Assert.Equal(expected.GetProperty("customWidth").GetBoolean(), col.CustomWidth?.Value ?? false);
            Assert.NotNull(col.Style); // سبک ستون: B Nazanin
        }

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("اظهار");
        foreach (var index in new[] { 1, 7, 12, 13, 40 })
        {
            Assert.Equal("B Nazanin", ws.Column(index).Style.Font.FontName);
            Assert.Equal(XLAlignmentHorizontalValues.Center, ws.Column(index).Style.Alignment.Horizontal);
        }

        var format = sheet.GetFirstChild<S.SheetFormatProperties>()!;
        Assert.Equal(root.GetProperty("sheetFormat").GetProperty("defaultColWidth").GetDouble(), format.DefaultColumnWidth!.Value);
        Assert.Equal(root.GetProperty("sheetFormat").GetProperty("defaultRowHeight").GetDouble(), format.DefaultRowHeight!.Value);
        var rows = sheet.Descendants<S.Row>().ToList();
        Assert.Equal(43, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(16.8, r.Height!.Value);
            Assert.True(r.CustomHeight!.Value);
        });

        var view = sheet.GetFirstChild<S.SheetViews>()!.Elements<S.SheetView>().Single();
        var expectedView = root.GetProperty("sheetView");
        Assert.Equal(expectedView.GetProperty("rightToLeft").GetBoolean(), view.RightToLeft?.Value ?? false);
        Assert.Equal(expectedView.GetProperty("tabSelected").GetBoolean(), view.TabSelected?.Value ?? false);
        Assert.Equal((uint)expectedView.GetProperty("zoomScale").GetInt32(), view.ZoomScale!.Value);
        Assert.Equal((uint)expectedView.GetProperty("zoomScaleNormal").GetInt32(), view.ZoomScaleNormal!.Value);

        var setup = sheet.GetFirstChild<S.PageSetup>()!;
        var expectedSetup = root.GetProperty("pageSetup");
        Assert.Equal((uint)expectedSetup.GetProperty("paperSize").GetInt32(), setup.PaperSize!.Value);
        Assert.Equal(S.OrientationValues.Portrait, setup.Orientation!.Value);
        Assert.Equal((uint)expectedSetup.GetProperty("scale").GetInt32(), setup.Scale!.Value);
        Assert.Equal((uint)expectedSetup.GetProperty("fitToWidth").GetInt32(), setup.FitToWidth?.Value ?? 1u);
        Assert.Equal((uint)expectedSetup.GetProperty("fitToHeight").GetInt32(), setup.FitToHeight!.Value);
        Assert.True(sheet.GetFirstChild<S.SheetProperties>()!.PageSetupProperties!.FitToPage!.Value);

        var margins = sheet.GetFirstChild<S.PageMargins>()!;
        var expectedMargins = root.GetProperty("pageMargins");
        Assert.Equal(expectedMargins.GetProperty("left").GetDouble(), margins.Left!.Value);
        Assert.Equal(expectedMargins.GetProperty("right").GetDouble(), margins.Right!.Value);
        Assert.Equal(expectedMargins.GetProperty("top").GetDouble(), margins.Top!.Value);
        Assert.Equal(expectedMargins.GetProperty("bottom").GetDouble(), margins.Bottom!.Value);
        Assert.Equal(expectedMargins.GetProperty("header").GetDouble(), margins.Header!.Value);
        Assert.Equal(expectedMargins.GetProperty("footer").GetDouble(), margins.Footer!.Value);

        // قلم Normal کتاب کار Calibri 11، مثل نمونه (قاعدهٔ پهنای صفحه به آن بسته است)
        var styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet;
        var normalFont = styles.Fonts!.Elements<S.Font>().First();
        Assert.Equal("Calibri", normalFont.FontName!.Val!.Value);
        Assert.Equal(11, normalFont.FontSize!.Val!.Value);
        // قالب عدد General: هیچ قالب سفارشی
        Assert.True(styles.NumberingFormats is null || !styles.NumberingFormats.Elements<S.NumberingFormat>().Any());
        Assert.All(styles.CellFormats!.Elements<S.CellFormat>(), xf => Assert.Equal(0u, xf.NumberFormatId?.Value ?? 0u));

        // سرصفحه ندارد (قطعی)، پانویس صفحه هم ندارد
        var headerFooter = sheet.GetFirstChild<S.HeaderFooter>();
        Assert.True(headerFooter?.OddHeader is null || string.IsNullOrEmpty(headerFooter.OddHeader.Text));
        Assert.True(headerFooter?.OddFooter is null || string.IsNullOrEmpty(headerFooter.OddFooter.Text));
    }

    [Fact]
    public async Task Workbook_HasPrintTitlesOnTheMainSheetOnly_FullCalcOnLoad_AndNoExternalParts()
    {
        var bytes = await Full();
        using var document = XlsxSamples.Open(bytes);
        var workbook = document.WorkbookPart!.Workbook;
        var names = workbook.DefinedNames!.Elements<S.DefinedName>().ToList();
        var titles = Assert.Single(names, n => n.Name == "_xlnm.Print_Titles");
        Assert.Equal(0u, titles.LocalSheetId!.Value);
        Assert.Equal("'اظهار'!$1:$2", titles.Text);
        Assert.True(workbook.CalculationProperties!.FullCalculationOnLoad!.Value);
        Assert.Equal(["اظهار", "خلاصه"], workbook.Sheets!.Elements<S.Sheet>().Select(s => s.Name!.Value!));
        Assert.Empty(document.WorkbookPart.ExternalWorkbookParts);
        Assert.Null(document.WorkbookPart.VbaProjectPart);

        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(1, book.Worksheet("اظهار").PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(2, book.Worksheet("اظهار").PageSetup.LastRowToRepeatAtTop);
        Assert.Equal(0, book.Worksheet("خلاصه").PageSetup.FirstRowToRepeatAtTop);
    }

    [Fact]
    public async Task Full_EveryFormulaCell_HasAnExactCachedValue()
    {
        var model = XlsxSamples.Compose(XlsxSamples.Shipment());
        var bytes = await XlsxSamples.WriteAsync(model, "full-sample");
        using var document = XlsxSamples.Open(bytes);
        foreach (var renderSheet in model.Sheets)
        {
            var raw = XlsxSamples.RawCells(XlsxSamples.Sheet(document, renderSheet.Name));
            foreach (var cell in renderSheet.Rows.SelectMany(r => r.Cells))
            {
                var xml = raw[cell.Address];
                if (cell.Formula is not null)
                {
                    Assert.True(xml.CellFormula is not null, $"{renderSheet.Name}!{cell.Address} has no formula");
                    Assert.Equal(cell.Formula, xml.CellFormula!.Text);
                    Assert.True(xml.CellValue is not null, $"{renderSheet.Name}!{cell.Address} has no cached <v>");
                }

                if (cell.Value.Kind == DocValueKind.Number)
                {
                    Assert.Equal(DocNumber.General(cell.Value.Number), xml.CellValue!.Text);
                    Assert.True(xml.DataType is null || xml.DataType.Value == S.CellValues.Number);
                }
            }
        }

        // J57 نمونه: 2.30 × 1.65 → 3.8، نه 3.7949999
        var row3816 = model.Sheets[0].Rows.Single(r => r.Kind == RenderRowKinds.Data && r.Cells.Any(c => c.Col == 2 && c.Value.AsText() == "3816"));
        var sheet = XlsxSamples.RawCells(XlsxSamples.Sheet(document, "اظهار"));
        Assert.Equal("3.8", sheet[$"K{row3816.Number}"].CellValue!.Text);
        Assert.Equal($"ROUND(I{row3816.Number}*J{row3816.Number}, 2)", sheet[$"K{row3816.Number}"].CellFormula!.Text);
    }

    [Fact]
    public async Task Full_ReadBack_GivesTheTotals_WithoutEvaluating()
    {
        var bytes = await Full();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var summary = workbook.Worksheet("خلاصه");
        var last = summary.LastRowUsed()!.RowNumber();
        Assert.Equal(40, last); // عنوان، سرستون، ۳۶ ردیف، «=»، جمع
        Assert.Equal(242, summary.Cell(last, 5).CachedValue.GetNumber());
        Assert.Equal(464.74, summary.Cell(last, 6).CachedValue.GetNumber());
        Assert.Equal(20322.5, summary.Cell(last, 7).CachedValue.GetNumber());
        Assert.Equal("SUM(E3:E38)", summary.Cell(last, 5).FormulaA1);
        Assert.Equal("'اظهار'!J7", summary.Cell(3, 6).FormulaA1);
        Assert.Equal("'اظهار'!L7", summary.Cell(3, 7).FormulaA1);
        Assert.False(summary.Cell(3, 6).NeedsRecalculation);

        // «=»: متن ساده، بی کادر و بی پیشوند گیومه
        foreach (var col in Enumerable.Range(4, 4))
        {
            var equals = summary.Cell(last - 1, col);
            Assert.Equal("=", equals.CachedValue.GetText());
            Assert.False(equals.HasFormula);
            Assert.False(equals.Style.IncludeQuotePrefix);
            Assert.Equal(XLBorderStyleValues.None, equals.Style.Border.TopBorder);
            Assert.Equal(XLBorderStyleValues.None, equals.Style.Border.LeftBorder);
        }

        // D خالی ولی کادردار
        var blank = summary.Cell(last, 4);
        Assert.True(blank.CachedValue.IsBlank);
        Assert.Equal(XLBorderStyleValues.Thin, blank.Style.Border.TopBorder);
        Assert.Equal(XLBorderStyleValues.Thin, blank.Style.Border.LeftBorder);

        // عنوان خلاصه C1:H1 زرد و بی کادر
        Assert.Contains(summary.MergedRanges, r => r.RangeAddress.ToString() == "C1:H1");
        Assert.Equal("صورت نگله بندی و اظهار فرش صادراتی  تاریخ1405.06.15", summary.Cell("C1").CachedValue.GetText());
        Assert.Equal(XLColor.FromArgb(unchecked((int)0xFFFFFF00)).Color.ToArgb(), summary.Cell("C1").Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.Equal(XLBorderStyleValues.None, summary.Cell("C1").Style.Border.TopBorder);
        Assert.True(summary.RightToLeft);

        var main = workbook.Worksheet("اظهار");
        Assert.True(main.RightToLeft);
        Assert.Equal(70, main.SheetView.ZoomScale);
        Assert.Contains(main.MergedRanges, r => r.RangeAddress.ToString() == "A1:M1");
        Assert.Equal(XLDataType.Number, main.Cell("B3").DataType);
        Assert.Equal(3778, main.Cell("B3").CachedValue.GetNumber());
        Assert.Equal(1, main.Cell("H3").CachedValue.GetNumber());
        // عنوان: رنگ #767171 و زمینهٔ زرد
        Assert.Equal(unchecked((int)0xFF767171), main.Cell("A1").Style.Font.FontColor.Color.ToArgb());
        Assert.Equal(unchecked((int)0xFFFFFF00), main.Cell("A1").Style.Fill.BackgroundColor.Color.ToArgb());
    }

    [Fact]
    public async Task Full_RowBreaks_AreManual_AndFallOnlyBetweenBales()
    {
        var model = XlsxSamples.Compose(XlsxSamples.Shipment());
        var bytes = await XlsxSamples.WriteAsync(model, "full-sample");
        using var document = XlsxSamples.Open(bytes);
        var breaks = XlsxSamples.Sheet(document, "اظهار").GetFirstChild<S.RowBreaks>()!.Elements<S.Break>().ToList();
        var footers = model.Sheets[0].Rows.Where(r => r.Kind == RenderRowKinds.Footer).Select(r => r.Number).ToHashSet();
        Assert.Equal(model.Sheets[0].RowBreaksAfter, breaks.Select(b => (int)b.Id!.Value));
        Assert.Equal(43u, breaks[0].Id!.Value); // صفحهٔ ۲ با نگلهٔ ۱۱
        Assert.All(breaks, b =>
        {
            Assert.Contains((int)b.Id!.Value, footers);
            Assert.True(b.ManualPageBreak!.Value);
            Assert.Equal(16383u, b.Max!.Value);
        });
        Assert.Null(XlsxSamples.Sheet(document, "خلاصه").GetFirstChild<S.RowBreaks>());
    }

    [Fact]
    public async Task Draft_HasTheCentreHeader()
    {
        var bytes = await Full(draft: true);
        using var document = XlsxSamples.Open(bytes);
        var header = XlsxSamples.Sheet(document, "اظهار").GetFirstChild<S.HeaderFooter>()!.OddHeader!.Text;
        Assert.Contains("پیش‌نویس", header, StringComparison.Ordinal);
        Assert.StartsWith("&C", header, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adl_File_SaysAdlEverywhere()
    {
        var model = XlsxSamples.Compose(XlsxSamples.Shipment(b => b <= 3, BaleTerms.Adl));
        var bytes = await XlsxSamples.WriteAsync(model, "adl-bales-1-3");
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var main = workbook.Worksheet("اظهار");
        Assert.StartsWith("صورت عدل بندی", main.Cell("A1").CachedValue.GetText());
        Assert.Equal("عدل", main.Cell("A2").CachedValue.GetText());
        Assert.Equal("عدل شماره", main.Cell("A7").CachedValue.GetText());
        Assert.Equal("عدل", workbook.Worksheet("خلاصه").Cell("D2").CachedValue.GetText());
        Assert.DoesNotContain(workbook.Worksheets.SelectMany(w => w.CellsUsed()), c => c.CachedValue.IsText && c.CachedValue.GetText().Contains("نگله", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DataText_StartingWithAFormulaCharacter_IsAQuotePrefixedString()
    {
        var data = XlsxSamples.Shipment(b => b == 1);
        var bale = data.Bales[0];
        var first = bale.Lines[0] with { Rug = bale.Lines[0].Rug with { Pattern = "=SUM(1)", Serial = "0123" } };
        data = data with { Bales = [bale with { Lines = [first, .. bale.Lines.Skip(1)], Pieces = null, AreaM2 = null, Usd = null }] };
        var bytes = await XlsxSamples.WriteAsync(XlsxSamples.Compose(data), "quote-prefix");
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet("اظهار");
        Assert.False(ws.Cell("E3").HasFormula);
        Assert.Equal("=SUM(1)", ws.Cell("E3").CachedValue.GetText());
        Assert.True(ws.Cell("E3").Style.IncludeQuotePrefix);
        Assert.Equal("0123", ws.Cell("B3").CachedValue.GetText());
        Assert.Equal(XLDataType.Text, ws.Cell("B3").DataType);
    }

    // ── سبک ─────────────────────────────────────────────────────────────

    private static void CompareStyle(string reference, JsonElement e, IXLStyle style, bool shrinkExpected, bool compareBorders, List<string> mismatches)
    {
        var font = e.GetProperty("font");
        if (font.GetProperty("name").GetString() != style.Font.FontName)
            mismatches.Add($"{reference}: font {style.Font.FontName}");
        if ((double)font.GetProperty("size").GetDecimal() != style.Font.FontSize)
            mismatches.Add($"{reference}: size {style.Font.FontSize}");
        var color = font.GetProperty("color").GetString()!;
        if (!SameColor(style.Font.FontColor, color))
            mismatches.Add($"{reference}: colour {style.Font.FontColor} ≠ {color}");

        var fill = e.GetProperty("fill").ValueKind == JsonValueKind.Null ? null : e.GetProperty("fill").GetString();
        if (fill is null)
        {
            if (style.Fill.PatternType != XLFillPatternValues.None)
                mismatches.Add($"{reference}: unexpected fill {style.Fill.BackgroundColor}");
        }
        else if (style.Fill.PatternType != XLFillPatternValues.Solid || !SameColor(style.Fill.BackgroundColor, fill))
        {
            mismatches.Add($"{reference}: fill {style.Fill.BackgroundColor} ≠ {fill}");
        }

        if (style.NumberFormat.NumberFormatId != e.GetProperty("numFmtId").GetInt32() || !string.IsNullOrEmpty(style.NumberFormat.Format))
            mismatches.Add($"{reference}: number format {style.NumberFormat.NumberFormatId} «{style.NumberFormat.Format}»");

        var align = e.GetProperty("align");
        if (!string.Equals(align.GetProperty("h").GetString(), style.Alignment.Horizontal.ToString(), StringComparison.OrdinalIgnoreCase))
            mismatches.Add($"{reference}: horizontal {style.Alignment.Horizontal}");
        if (!string.Equals(align.GetProperty("v").GetString(), style.Alignment.Vertical.ToString(), StringComparison.OrdinalIgnoreCase))
            mismatches.Add($"{reference}: vertical {style.Alignment.Vertical}");
        if ((shrinkExpected || align.GetProperty("shrinkToFit").GetBoolean()) != style.Alignment.ShrinkToFit)
            mismatches.Add($"{reference}: shrinkToFit {style.Alignment.ShrinkToFit}");

        if (!compareBorders)
            return;
        var border = e.GetProperty("border");
        void Side(string name, XLBorderStyleValues actual)
        {
            var want = border.GetProperty(name).GetString() == "thin" ? XLBorderStyleValues.Thin : XLBorderStyleValues.None;
            if (want != actual)
                mismatches.Add($"{reference}: border {name} {actual} ≠ {want}");
        }

        Side("left", style.Border.LeftBorder);
        Side("right", style.Border.RightBorder);
        Side("top", style.Border.TopBorder);
        Side("bottom", style.Border.BottomBorder);
    }

    /// <summary>رنگ خانه با AARRGGBB نمونه؛ «خودکار» و رنگ متن پوسته (Text1) همان سیاه نمونه‌اند.</summary>
    private static bool SameColor(XLColor color, string argb)
    {
        var expected = int.Parse(argb, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return color.ColorType switch
        {
            XLColorType.Color => color.Color.ToArgb() == expected,
            XLColorType.Theme => color.ThemeColor == XLThemeColor.Text1 && expected == unchecked((int)0xFF000000),
            XLColorType.Indexed => color.Indexed == 64 && expected == unchecked((int)0xFF000000),
            _ => false
        };
    }

    [GeneratedRegex(@"^ROWS\(B\d+:B\d+\)$")]
    private static partial Regex RowsPattern();
}
