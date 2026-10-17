// E.6 day-1 spike (throwaway; not in the repo). Part A: what raw ClosedXML 0.105.1 writes for every E.6 #1 item.
// Part B: ClosedXML read-back speed (10,000+ rows) on a file from the real writer (ClosedXmlDocumentWriter + CachedValueInjector).
using System.Diagnostics;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Infrastructure.Documents;
using S = DocumentFormat.OpenXml.Spreadsheet;

var outDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
Directory.CreateDirectory(outDir);
var report = new List<string>();
void R(string line) { report.Add(line); Console.WriteLine(line); }

// ── Part A: raw ClosedXML ────────────────────────────────────────────────
var rawPath = Path.Combine(outDir, "spike-raw-closedxml.xlsx");
using (var wb = new XLWorkbook())
{
    wb.FullCalculationOnLoad = true;
    var ws = wb.AddWorksheet("اظهار");
    ws.RightToLeft = true;
    ws.SheetView.ZoomScale = 70;
    ws.SheetView.ZoomScaleNormal = 70;
    ws.ColumnWidth = 9.109375;
    ws.RowHeight = 16.8;
    double[] widths = [3.88671875, 6.5546875, 7.88671875, 8.6640625, 9.109375, 8, 10.109375, 6.109375, 6.44140625, 7.109375, 14.88671875, 16];
    for (var i = 0; i < widths.Length; i++)
    {
        ws.Column(i + 1).Width = widths[i];
        ws.Column(i + 1).Style.Font.FontName = "B Nazanin";
        ws.Column(i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    ws.Cell("A1").Value = "صورت نگله بندی و اظهار فرش صادراتی    تاریخ  1405.06.15";
    ws.Range("A1:L1").Merge();
    ws.Range("A1:L1").Style.Font.FontName = "B Nazanin";
    ws.Range("A1:L1").Style.Font.FontColor = XLColor.FromArgb(unchecked((int)0xFF767171));
    ws.Range("A1:L1").Style.Fill.PatternType = XLFillPatternValues.Solid;
    ws.Range("A1:L1").Style.Fill.BackgroundColor = XLColor.FromArgb(unchecked((int)0xFFFFFF00));
    ws.Range("A1:L1").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    ws.Cell("B3").Value = 3816;
    ws.Cell("H3").Value = 2.3;
    ws.Cell("I3").Value = 1.65;
    ws.Cell("J3").FormulaA1 = "ROUND(H3*I3, 2)";
    ws.Range("A3:A6").Merge();
    for (var r = 1; r <= 50; r++)
        ws.Row(r).Height = 16.8;
    ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
    ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
    ws.PageSetup.FitToPages(1, 0);
    ws.PageSetup.Scale = 95; // ClosedXML: setting Scale after FitToPages?
    ws.PageSetup.Margins.Left = 0.2; ws.PageSetup.Margins.Right = 0.2; ws.PageSetup.Margins.Top = 0.5;
    ws.PageSetup.Margins.Bottom = 0.5; ws.PageSetup.Margins.Header = 0.3; ws.PageSetup.Margins.Footer = 0.3;
    ws.PageSetup.SetRowsToRepeatAtTop(1, 2);
    ws.PageSetup.AddHorizontalPageBreak(43);
    ws.PageSetup.Header.Center.AddText("پیش‌نویس");
    var summary = wb.AddWorksheet("خلاصه");
    summary.RightToLeft = true;
    summary.Cell("D3").FormulaA1 = "'اظهار'!J3";
    wb.SaveAs(rawPath);
}

using (var doc = SpreadsheetDocument.Open(rawPath, false))
{
    var wbPart = doc.WorkbookPart!;
    var sheet = wbPart.Workbook.Sheets!.Elements<S.Sheet>().ToList();
    var ws = ((WorksheetPart)wbPart.GetPartById(sheet[0].Id!)).Worksheet;
    var view = ws.GetFirstChild<S.SheetViews>()!.Elements<S.SheetView>().First();
    R($"A1 RTL={view.RightToLeft?.Value} zoom={view.ZoomScale?.Value}/{view.ZoomScaleNormal?.Value}");
    R($"A2 sheet names={string.Join(",", sheet.Select(s => s.Name!.Value))}");
    R($"A3 merges={string.Join(",", ws.Descendants<S.MergeCell>().Select(m => m.Reference!.Value))}");
    var j3 = ws.Descendants<S.Cell>().First(c => c.CellReference == "J3");
    R($"A4 formula J3 f='{j3.CellFormula?.Text}' v='{j3.CellValue?.Text ?? "(none)"}' t={j3.DataType?.Value.ToString() ?? "(none)"}");
    var d3 = ((WorksheetPart)wbPart.GetPartById(sheet[1].Id!)).Worksheet.Descendants<S.Cell>().First(c => c.CellReference == "D3");
    R($"A4b cross-sheet D3 f='{d3.CellFormula?.Text}' v='{d3.CellValue?.Text ?? "(none)"}'");
    var styles = wbPart.WorkbookStylesPart!.Stylesheet;
    R($"A5 fonts={string.Join(" | ", styles.Fonts!.Elements<S.Font>().Select(f => $"{f.FontName?.Val} {f.FontSize?.Val} {f.Color?.Rgb}"))}");
    R($"A5b fills={string.Join(" | ", styles.Fills!.Elements<S.Fill>().Select(f => $"{f.PatternFill?.PatternType?.Value} {f.PatternFill?.ForegroundColor?.Rgb}"))}");
    R($"A5c numFmts={(styles.NumberingFormats is null ? "(none)" : string.Join(",", styles.NumberingFormats.Elements<S.NumberingFormat>().Select(n => $"{n.NumberFormatId}:'{n.FormatCode}'")))}");
    R($"A6 cols={string.Join(" ", ws.GetFirstChild<S.Columns>()!.Elements<S.Column>().Select(c => $"{c.Min}-{c.Max}:{c.Width}{(c.CustomWidth?.Value == true ? "c" : "")}"))}");
    var fmt = ws.GetFirstChild<S.SheetFormatProperties>()!;
    R($"A6b sheetFormat defaultColWidth={fmt.DefaultColumnWidth?.Value} defaultRowHeight={fmt.DefaultRowHeight?.Value}");
    var row1 = ws.Descendants<S.Row>().First();
    R($"A7 row1 ht={row1.Height?.Value} customHeight={row1.CustomHeight?.Value}");
    var setup = ws.GetFirstChild<S.PageSetup>()!;
    R($"A8 pageSetup paper={setup.PaperSize?.Value} orient={setup.Orientation?.Value} scale={setup.Scale?.Value} fitW={setup.FitToWidth?.Value} fitH={setup.FitToHeight?.Value} fitToPage={ws.GetFirstChild<S.SheetProperties>()?.PageSetupProperties?.FitToPage?.Value}");
    var m = ws.GetFirstChild<S.PageMargins>()!;
    R($"A9 margins L{m.Left} R{m.Right} T{m.Top} B{m.Bottom} H{m.Header} F{m.Footer}");
    R($"A10 definedNames={string.Join(" | ", wbPart.Workbook.DefinedNames?.Elements<S.DefinedName>().Select(d => $"{d.Name} local={d.LocalSheetId?.Value} text={d.Text}") ?? [])}");
    R($"A11 rowBreaks={string.Join(",", ws.GetFirstChild<S.RowBreaks>()?.Elements<S.Break>().Select(b => $"id={b.Id} max={b.Max} man={b.ManualPageBreak}") ?? [])}");
    R($"A12 header='{ws.GetFirstChild<S.HeaderFooter>()?.OddHeader?.Text}'");
    R($"A13 fullCalcOnLoad={wbPart.Workbook.CalculationProperties?.FullCalculationOnLoad?.Value}");
    R($"A14 parts={string.Join(",", doc.Parts.Select(p => p.OpenXmlPart.Uri.ToString()))}");
}

// ClosedXML read-back of its own raw file: cached J3 without evaluation
using (var wb = new XLWorkbook(rawPath))
{
    var c = wb.Worksheet("اظهار").Cell("J3");
    R($"A15 ClosedXML raw read-back J3 cached={c.CachedValue} needsRecalc={c.NeedsRecalculation}");
}

// ── Part B: real writer, 10,000+ rows, ClosedXML read speed ──────────────
var rug = new DocumentRug(null, "3816", 1, Pattern: "ساده", Origin: "گبه", Material: "پشم", IntakeLength: 2.3m, IntakeWidth: 1.65m,
    Custom: new Dictionary<string, string?> { ["bg_color"] = "کرم", ["border_color"] = "آبی" });
var bales = Enumerable.Range(1, 2500).Select(b => new DocumentBale(b,
    Enumerable.Range(1, 4).Select(i => new DocumentLine(i, rug with { Serial = (10000 + b * 4 + i).ToString(System.Globalization.CultureInfo.InvariantCulture) }, 2.3m, 1.65m, UsdPerM2: 45m)).ToList())).ToList();
var data = new DocumentData(new DocumentShipment(null, 1, null, new DateOnly(2026, 9, 6), null, null, null, null, null), bales, [], BaleTerms.Negleh, new DocumentTenant(), false);
var catalogue = DocumentFields.Catalogue([DocumentFields.CustomText("bg_color", "رنگ متن"), DocumentFields.CustomText("border_color", "رنگ حاشیه")]);
var sw = Stopwatch.StartNew();
var model = DocumentComposer.Compose(LayoutJson.Default("shipment-declaration").Parse(), data, new ComposeOptions(Fields: catalogue));
var composeMs = sw.ElapsedMilliseconds;
sw.Restart();
var bigPath = Path.Combine(outDir, "spike-10000-rows.xlsx");
await using (var file = File.Create(bigPath))
    await new ClosedXmlDocumentWriter().WriteAsync(model, file);
var writeMs = sw.ElapsedMilliseconds;
var dataRows = model.Sheets[0].Rows.Count(r => r.Kind == RenderRowKinds.Data);
var totalRows = model.Sheets[0].Rows.Count;
sw.Restart();
decimal sum = 0;
int formulas = 0, numbers = 0;
using (var wb = new XLWorkbook(bigPath))
{
    foreach (var cell in wb.Worksheet("اظهار").CellsUsed())
    {
        if (cell.HasFormula) formulas++;
        if (cell.CachedValue.IsNumber) { numbers++; sum += (decimal)cell.CachedValue.GetNumber(); }
    }
    var totals = wb.Worksheet("خلاصه").LastRowUsed()!;
    R($"B4 summary totals (cached) E={totals.Cell(5).CachedValue} F={totals.Cell(6).CachedValue} G={totals.Cell(7).CachedValue}");
}
var readMs = sw.ElapsedMilliseconds;
R($"B1 compose {dataRows} data rows / {totalRows} rows: {composeMs} ms; write: {writeMs} ms; file {new FileInfo(bigPath).Length / 1024} KB");
R($"B2 ClosedXML load + read every used cell (cached, no evaluation): {readMs} ms; formulas={formulas}; numeric cells={numbers}");
R($"B3 read under 3 s: {(readMs < 3000 ? "PASS" : "FAIL")}");

File.WriteAllLines(Path.Combine(outDir, "spike-report.txt"), report);
