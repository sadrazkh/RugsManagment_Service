using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Expressions;

namespace RugsManagment.Infrastructure.Documents;

/// <summary>
/// پس از ClosedXML، بستهٔ xlsx را با OpenXml باز می‌کند و آنچه موتور سند دقیق می‌خواهد می‌نویسد (spec E.6 #2 و #6):
/// <list type="bullet">
/// <item><c>&lt;v&gt;</c> هر خانهٔ فرمول از مقدار decimal سازنده، invariant و بی نویز ممیز شناور («3.8»، نه
/// «3.7999999999999998»)؛ پس اکسل و LibreOffice پیش از محاسبهٔ دوباره همان عدد را نشان می‌دهند و خوانندهٔ ClosedXML بی
/// ارزیابی همان را می‌خواند. عددهای ثابت هم با همین متن دقیق بازنویسی می‌شوند.</item>
/// <item>پهنای دقیق ستون‌ها (۳٫۸۸۶۷۱۸۷۵، نه با فاصلهٔ قلمی که ClosedXML می‌افزاید) و پهنای پیش‌فرض.</item>
/// <item><c>_xlnm.Print_Titles</c> به شکل <c>'اظهار'!$1:$2</c>.</item>
/// <item><c>fitToPage = 1</c> و <c>scale = 95</c> هر دو، همان نمونهٔ بازرگان (ClosedXML یکی را جای دیگری می‌گذارد)؛ و
/// <c>max</c> شکستن ردیف‌ها مثل خود اکسل ۱۶۳۸۳.</item>
/// <item>بی <c>numFmts</c> زائد: قالب General درونی است، مثل نمونه که <c>numFmts</c> ندارد.</item>
/// </list>
/// </summary>
public static class CachedValueInjector
{
    /// <summary>بیشینهٔ ستون برای <c>brk max</c> شکستن ردیف (همان که اکسل می‌نویسد).</summary>
    private const uint RowBreakMax = 16383;

    /// <summary>آخرین ستون برگهٔ اکسل (XFD).</summary>
    private const uint MaxColumn = 16384;

    /// <summary>شمارهٔ اولین قالب عددی سفارشی؛ کمتر از آن قالب‌های درونی اکسل‌اند.</summary>
    private const uint FirstCustomNumberFormat = 164;

    public static void Apply(Stream package, RenderModel model)
    {
        package.Position = 0;
        using (var document = SpreadsheetDocument.Open(package, true))
        {
            var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook part is missing.");
            var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>().ToList() ?? [];
            for (var index = 0; index < model.Sheets.Count; index++)
            {
                var renderSheet = model.Sheets[index];
                var sheet = sheets.FirstOrDefault(s => s.Name?.Value == renderSheet.Name)
                            ?? throw new InvalidOperationException($"Sheet {renderSheet.Name} was not written.");
                var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
                ApplySheet(part.Worksheet, renderSheet, model.Page);
                part.Worksheet.Save();
            }

            FixPrintTitles(workbookPart.Workbook, model, sheets);
            workbookPart.Workbook.Save();
            DropBuiltInNumberFormats(workbookPart.WorkbookStylesPart);
        }

        package.Position = 0;
    }

    /// <summary>
    /// ClosedXML یک <c>&lt;numFmt numFmtId="0" formatCode=""/&gt;</c> بی‌معنا در <c>numFmts</c> می‌نویسد، هرچند قالب General
    /// (شمارهٔ ۰) از قالب‌های درونی اکسل است و جایش آن‌جا نیست. نمونهٔ بازرگان اصلاً <c>numFmts</c> ندارد (E.7)؛ پس هر قالب
    /// درونی (شمارهٔ کمتر از ۱۶۴) از این فهرست برداشته می‌شود و اگر چیزی نماند خود فهرست هم. قالب سفارشیِ قالب سند
    /// (۱۶۴ به بعد) دست نمی‌خورد.
    /// </summary>
    private static void DropBuiltInNumberFormats(WorkbookStylesPart? stylesPart)
    {
        var formats = stylesPart?.Stylesheet?.NumberingFormats;
        if (formats is null)
            return;
        foreach (var format in formats.Elements<NumberingFormat>().Where(f => f.NumberFormatId?.Value is null or < FirstCustomNumberFormat).ToList())
            format.Remove();
        var remaining = formats.Elements<NumberingFormat>().Count();
        if (remaining == 0)
            formats.Remove();
        else
            formats.Count = (uint)remaining;
        stylesPart!.Stylesheet!.Save();
    }

    private static void ApplySheet(Worksheet worksheet, RenderSheet sheet, RenderPage page)
    {
        var cells = sheet.Rows.SelectMany(r => r.Cells).ToDictionary(c => c.Address, StringComparer.Ordinal);
        foreach (var cell in worksheet.Descendants<Cell>())
        {
            var reference = cell.CellReference?.Value;
            if (reference is null || !cells.TryGetValue(reference, out var model))
            {
                if (cell.CellFormula is not null)
                    throw new InvalidOperationException($"Formula cell {sheet.Name}!{reference} has no value in the render model.");
                continue;
            }

            if (model.Formula is not null)
            {
                if (cell.CellFormula is null)
                    throw new InvalidOperationException($"Cell {sheet.Name}!{reference} lost its formula.");
                SetCached(cell, model.Value);
            }
            else if (model.IsNumber && cell.CellFormula is null)
            {
                cell.DataType = null;
                cell.CellValue = new CellValue(DocNumber.General(model.Value.Number));
            }
        }

        // پهنای دقیق ستون‌ها (ClosedXML ستون‌های هم‌پهنای کنار هم را یک <col min..max> می‌کند)
        var columns = worksheet.GetFirstChild<Columns>();
        if (columns is not null)
        {
            var widths = sheet.Columns.ToDictionary(c => (uint)c.Index, c => c.Width);
            uint last = 0;
            foreach (var column in columns.Elements<Column>().ToList())
            {
                if (column.Min?.Value is not { } min || column.Max?.Value is not { } max)
                    continue;
                last = Math.Max(last, max);
                if (widths.TryGetValue(min, out var width)
                    && Enumerable.Range((int)min, (int)(max - min + 1)).All(i => widths.TryGetValue((uint)i, out var w) && w == width))
                {
                    column.Width = (double)width;
                    column.CustomWidth = true;
                }
            }

            // ستون‌های بعدی تا آخر برگه همان سبک ستون‌ها را می‌گیرند (نمونه: <col min="13" max="16384" style=…>)، تا خانهٔ
            // خالیِ بیرون از جدول هم B Nazanin وسط‌چین باشد؛ پهنا همان پیش‌فرض و customWidth ندارد
            var first = columns.Elements<Column>().FirstOrDefault(c => c.Min?.Value == 1);
            if (sheet.ColumnStyle is not null && first?.Style is { } styleIndex && last is > 0 and < MaxColumn)
            {
                columns.Append(new Column
                {
                    Min = last + 1,
                    Max = MaxColumn,
                    Width = (double)page.DefaultColumnWidth,
                    Style = styleIndex.Value
                });
            }
        }

        var format = worksheet.GetFirstChild<SheetFormatProperties>();
        if (format is not null)
        {
            format.DefaultColumnWidth = (double)page.DefaultColumnWidth;
            format.DefaultRowHeight = (double)page.RowHeight;
        }

        // fitToPage و scale هر دو، مثل نمونه
        var setup = worksheet.GetFirstChild<PageSetup>();
        if (setup is not null)
        {
            setup.Scale = (uint)page.Scale;
            if (page.FitToPage)
            {
                setup.FitToWidth = (uint)page.FitToWidth;
                setup.FitToHeight = (uint)page.FitToHeight;
            }
        }

        var properties = worksheet.GetFirstChild<SheetProperties>();
        if (page.FitToPage)
        {
            if (properties is null)
            {
                properties = new SheetProperties();
                worksheet.InsertAt(properties, 0);
            }

            properties.PageSetupProperties ??= new PageSetupProperties();
            properties.PageSetupProperties.FitToPage = true;
        }
        else if (properties?.PageSetupProperties is { } pageSetupProperties)
        {
            pageSetupProperties.FitToPage = false;
        }

        var breaks = worksheet.GetFirstChild<RowBreaks>();
        if (breaks is not null)
        {
            foreach (var brk in breaks.Elements<Break>())
                brk.Max = RowBreakMax;
        }
    }

    private static void SetCached(Cell cell, DocValue value)
    {
        switch (value.Kind)
        {
            case DocValueKind.Number:
                cell.DataType = null;
                cell.CellValue = new CellValue(DocNumber.General(value.Number));
                break;
            case DocValueKind.Bool:
                cell.DataType = CellValues.Boolean;
                cell.CellValue = new CellValue(value.Bool ? "1" : "0");
                break;
            default:
                // متن، یا خالی (تقسیم بر صفر → "")
                cell.DataType = CellValues.String;
                cell.CellValue = new CellValue(DocumentText.XmlSafe(value.AsText()));
                break;
        }
    }

    private static void FixPrintTitles(Workbook workbook, RenderModel model, IReadOnlyList<Sheet> sheets)
    {
        var names = workbook.DefinedNames;
        if (names is null)
            return;
        foreach (var name in names.Elements<DefinedName>())
        {
            if (name.Name?.Value != "_xlnm.Print_Titles" || name.LocalSheetId?.Value is not { } local || local >= sheets.Count)
                continue;
            var sheetName = sheets[(int)local].Name?.Value;
            var renderSheet = model.Sheets.FirstOrDefault(s => s.Name == sheetName);
            if (renderSheet is null || renderSheet.PrintTitleRows <= 0)
                continue;
            name.Text = FormulaResolver.SheetReference(renderSheet.Name, $"$1:${renderSheet.PrintTitleRows}");
        }
    }
}
