using System.Globalization;
using ClosedXML.Excel;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Features.Documents.Layout;

namespace RugsManagment.Infrastructure.Documents;

/// <summary>
/// نویسندهٔ اکسل سند با ClosedXML (spec E.7)، و بعد <see cref="CachedValueInjector"/> برای چیزهایی که ClosedXML درست
/// نمی‌نویسد. آزمایش روز اول (E.6) نشان داد ClosedXML راست‌به‌چپ، ادغام، فرمول، قلم و زمینه و کادر، ارتفاع ردیف،
/// fitToPage با fitToHeight = 0، حاشیه‌ها، عنوان‌های تکرارشونده، شکستن دستی، سرصفحهٔ وسط و fullCalcOnLoad را می‌نویسد؛
/// ولی (۱) مقدار ذخیره‌شدهٔ فرمول‌ها را نمی‌نویسد، (۲) پهنای ستون را با فاصلهٔ قلم جابه‌جا می‌کند (۳٫۸۸۶۷۱۸۷۵ → ۴٫۵۹۷)،
/// (۳) <c>Print_Titles</c> را بی گیومه و بی <c>$</c> می‌نویسد، (۴) <c>scale</c> و <c>fitToPage</c> را جایگزین هم
/// می‌داند، (۵) <c>max</c> شکستن ردیف را ۱۰۴۸۵۷۶ می‌نویسد و (۶) یک <c>numFmt</c> خالی با شمارهٔ ۰ می‌افزاید. این‌ها را
/// تزریق‌گر درست می‌کند. اکسل فایل نهایی را با همان مقدارهای ذخیره‌شده باز می‌کند (پیش از محاسبهٔ دوباره) و در حالت
/// «جا شدن در پهنا» با <c>fitToHeight = 0</c> شکستن‌های دستی بین نگله‌ها را رعایت می‌کند (صفحهٔ ۲ نمونه با نگلهٔ ۱۱).
///
/// قلم Normal کتاب کار Calibri 11 می‌ماند (همان نمونه)؛ B Nazanin فقط از سبک ستون و خانه می‌آید، چون قاعدهٔ پهنای صفحه
/// (<see cref="PageBreakPlanner"/>) به پهنای رقم قلم Normal بسته است.
/// </summary>
public sealed class ClosedXmlDocumentWriter : IXlsxDocumentWriter
{
    public async Task WriteAsync(RenderModel model, Stream output, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            Build(workbook, model);
            workbook.SaveAs(buffer);
        }

        CachedValueInjector.Apply(buffer, model);
        buffer.Position = 0;
        await buffer.CopyToAsync(output, cancellationToken);
    }

    private static void Build(XLWorkbook workbook, RenderModel model)
    {
        workbook.FullCalculationOnLoad = true;
        var page = model.Page;
        var styles = new StyleCache(model);
        foreach (var sheet in model.Sheets)
        {
            var ws = workbook.AddWorksheet(sheet.Name);
            ws.RightToLeft = page.RightToLeft;
            ws.SheetView.ZoomScale = page.Zoom;
            ws.SheetView.ZoomScaleNormal = page.Zoom;
            ws.TabSelected = sheet.TabSelected;
            if (sheet.TabSelected)
                ws.TabActive = true;
            ws.ColumnWidth = (double)page.DefaultColumnWidth;
            ws.RowHeight = (double)page.RowHeight;

            var columnStyle = sheet.ColumnStyle is { } cs && model.Styles.TryGetValue(cs, out var cst) ? cst : null;
            foreach (var column in sheet.Columns)
            {
                var col = ws.Column(column.Index);
                col.Width = (double)column.Width;
                if (columnStyle is not null)
                    ApplyFontAndAlignment(col.Style, columnStyle);
            }

            SetupPage(ws.PageSetup, page, sheet);

            foreach (var row in sheet.Rows)
            {
                ws.Row(row.Number).Height = (double)row.Height;
                foreach (var cell in row.Cells)
                    WriteCell(ws, styles, row, cell);
            }

            var anchors = sheet.Rows.SelectMany(r => r.Cells.Select(c => (r.Number, Cell: c))).ToDictionary(x => (x.Number, x.Cell.Col), x => x.Cell);
            foreach (var range in sheet.Merges)
            {
                var merged = ws.Range(range.FirstRow, range.FirstCol, range.LastRow, range.LastCol);
                merged.Merge();
                if (anchors.TryGetValue((range.FirstRow, range.FirstCol), out var anchor)
                    && model.Styles.TryGetValue(anchor.Style, out var style) && style.Border == BorderStyles.Outer)
                    merged.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }
    }

    private static void SetupPage(IXLPageSetup setup, RenderPage page, RenderSheet sheet)
    {
        setup.PaperSize = page.PaperSize switch
        {
            8 => XLPaperSize.A3Paper,
            11 => XLPaperSize.A5Paper,
            1 => XLPaperSize.LetterPaper,
            _ => XLPaperSize.A4Paper
        };
        setup.PageOrientation = page.Orientation == "landscape" ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        if (page.FitToPage)
            setup.FitToPages(page.FitToWidth, page.FitToHeight);
        else
            setup.Scale = page.Scale;
        setup.Margins.Left = (double)page.Margins.Left;
        setup.Margins.Right = (double)page.Margins.Right;
        setup.Margins.Top = (double)page.Margins.Top;
        setup.Margins.Bottom = (double)page.Margins.Bottom;
        setup.Margins.Header = (double)page.Margins.Header;
        setup.Margins.Footer = (double)page.Margins.Footer;
        if (!string.IsNullOrEmpty(page.CenterHeader))
            setup.Header.Center.AddText(page.CenterHeader);
        if (sheet.PrintTitleRows > 0)
            setup.SetRowsToRepeatAtTop(1, sheet.PrintTitleRows);
        foreach (var after in sheet.RowBreaksAfter)
            setup.AddHorizontalPageBreak(after);
    }

    private static void WriteCell(IXLWorksheet ws, StyleCache styles, RenderRow row, RenderCell cell)
    {
        var target = ws.Cell(row.Number, cell.Col);
        switch (cell.Value.Kind)
        {
            case DocValueKind.Number:
                // عدد دقیق را تزریق‌گر در <v> می‌نویسد؛ این‌جا فقط نوع خانه عدد می‌شود
                target.Value = (double)cell.Value.Number;
                break;
            case DocValueKind.Text:
                target.Value = cell.Value.Text ?? "";
                break;
            case DocValueKind.Bool:
                target.Value = cell.Value.Bool;
                break;
            case DocValueKind.Date:
                target.Value = cell.Value.AsText();
                break;
        }

        if (cell.Formula is not null)
            target.FormulaA1 = cell.Formula;

        if (styles.Get(cell.Style, cell.Format) is not { } style)
            return;

        // خانه‌های زیر ادغام هم همان سبک لنگر را می‌گیرند (کادر یکدست درون ادغام — تفاوت عمدی E.10)
        if (cell.RowSpan > 1 || cell.ColSpan > 1)
        {
            foreach (var covered in ws.Range(row.Number, cell.Col, row.Number + cell.RowSpan - 1, cell.Col + cell.ColSpan - 1).Cells())
                covered.Style = style;
        }
        else
        {
            target.Style = style;
        }

        // فقط همین خانه؛ سبکِ نمونه دست نمی‌خورد
        if (cell.QuotePrefix)
            target.Style.IncludeQuotePrefix = true;
    }

    /// <summary>
    /// یک سبکِ جدا (نه سبکِ خانه‌ای از برگه) برای هر نام سبک و قالب عدد، که به خانه‌ها کپی می‌شود. چرا: ساختن سبک خانه به
    /// خانه در ClosedXML کند است (هر ویژگی یک سبک تازه می‌سازد)؛ کپی یک سبک آماده بیش از ده برابر تندتر است (آزمایش:
    /// ۱۶۲ هزار خانه ۲٫۲ ثانیه در برابر ۰٫۱۲ ثانیه). سبک نمونه از <c>XLWorkbook.DefaultStyle</c> می‌آید که هر بار نمونهٔ تازه
    /// و جدا از کتاب کار است، پس پیشوند گیومهٔ یک خانه به بقیه نشت نمی‌کند.
    /// </summary>
    private sealed class StyleCache(RenderModel model)
    {
        private readonly Dictionary<(string Style, string Format), IXLStyle> _styles = [];

        public IXLStyle? Get(string name, string format)
        {
            if (_styles.TryGetValue((name, format), out var cached))
                return cached;
            if (!model.Styles.TryGetValue(name, out var style))
                return null;
            var prototype = XLWorkbook.DefaultStyle;
            Apply(prototype, style, format);
            _styles[(name, format)] = prototype;
            return prototype;
        }
    }

    private static void Apply(IXLStyle target, RenderStyle style, string format)
    {
        ApplyFontAndAlignment(target, style);
        if (style.FillRgb is { } fill)
        {
            target.Fill.PatternType = XLFillPatternValues.Solid;
            target.Fill.BackgroundColor = Color(fill);
        }

        if (style.Border is BorderStyles.Thin or BorderStyles.Outer)
        {
            target.Border.LeftBorder = XLBorderStyleValues.Thin;
            target.Border.RightBorder = XLBorderStyleValues.Thin;
            target.Border.TopBorder = XLBorderStyleValues.Thin;
            target.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        if (!string.IsNullOrEmpty(format) && !string.Equals(format, LayoutDefaults.Format, StringComparison.OrdinalIgnoreCase))
            target.NumberFormat.Format = format;
    }

    private static void ApplyFontAndAlignment(IXLStyle target, RenderStyle style)
    {
        target.Font.FontName = style.Font;
        target.Font.FontSize = (double)style.Size;
        if (style.ColorRgb is { } color)
            target.Font.FontColor = Color(color);
        target.Alignment.Horizontal = style.HAlign switch
        {
            "left" => XLAlignmentHorizontalValues.Left,
            "right" => XLAlignmentHorizontalValues.Right,
            "general" => XLAlignmentHorizontalValues.General,
            _ => XLAlignmentHorizontalValues.Center
        };
        target.Alignment.Vertical = style.VAlign switch
        {
            "top" => XLAlignmentVerticalValues.Top,
            "bottom" => XLAlignmentVerticalValues.Bottom,
            _ => XLAlignmentVerticalValues.Center
        };
        if (style.ShrinkToFit)
            target.Alignment.ShrinkToFit = true;
    }

    private static XLColor Color(string argb)
        => XLColor.FromArgb(int.Parse(argb, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
