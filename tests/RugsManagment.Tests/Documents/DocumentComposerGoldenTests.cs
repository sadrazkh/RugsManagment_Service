using System.Text.Json;
using System.Text.RegularExpressions;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Features.Documents.Layout;

namespace RugsManagment.Tests.Documents;

/// <summary>
/// آزمون طلایی سند اظهار در سطح مدل (spec E.10 #1): نگله‌های ۱ تا ۱۰ نمونه، بی دسته، باید خانه به خانه با رونویسی
/// ردیف‌های ۱ تا ۴۳ فایل بازرگان (<c>sample-rows-1-43.expected.json</c>) بخواند — مقدار، متن فرمول، ادغام‌ها، قلم، زمینه،
/// کادر، تراز، پهنای ستون‌ها، تنظیم صفحه و نما. قاعده‌های مقایسه همان E.10 است: فرمول‌های مشترک نمونه باز شده‌اند؛ کادرِ
/// خانه‌های ادغام‌شده فقط با لبهٔ بیرونی سنجیده می‌شود؛ تفاوت‌های عمدی (<c>=ROWS(…)</c> در پانویس E، <c>shrinkToFit</c> روی
/// خانه‌های داده، رنگ «خودکار» به‌جای سیاهِ پوسته) صریح نوشته شده‌اند. همین مقایسه روی فایل xlsx واقعی در
/// <c>RugsManagment.IntegrationTests/Documents</c> تکرار می‌شود.
/// </summary>
public partial class DocumentComposerGoldenTests
{
    private static readonly JsonDocument Expected = JsonDocument.Parse(File.ReadAllText(SampleDocuments.FixturePath("sample-rows-1-43.expected.json")));

    private static RenderModel LotFree() => SampleDocuments.Compose(SampleDocuments.Shipment(b => b <= 10));

    [Fact]
    public void LotFree_HasTheSamplesRowMap()
    {
        var model = LotFree();
        Assert.False(model.HasLots);
        var main = model.Sheets[0];
        Assert.Equal("اظهار", main.Name);
        Assert.Equal(43, main.Rows.Count);
        Assert.Equal(RenderRowKinds.Title, main.Rows[0].Kind);
        Assert.Equal(RenderRowKinds.Header, main.Rows[1].Kind);
        Assert.Equal(Enumerable.Range(3, 4), main.Rows.Where(r => r.Group == 1 && r.Kind == RenderRowKinds.Data).Select(r => r.Number));
        Assert.Equal(7, main.Rows.Single(r => r.Group == 1 && r.Kind == RenderRowKinds.Footer).Number);
        Assert.Equal(43, main.Rows.Single(r => r.Group == 10 && r.Kind == RenderRowKinds.Footer).Number);
        Assert.Empty(model.Warnings);
    }

    [Fact]
    public void LotFree_Merges_MatchTheSample()
    {
        var expected = Expected.RootElement.GetProperty("merges").EnumerateArray().Select(m => m.GetString()!).ToHashSet();
        var actual = LotFree().Sheets[0].Merges.Select(m => m.A1).ToHashSet();
        Assert.Equal(expected.Order(), actual.Order());
    }

    [Fact]
    public void LotFree_EveryCell_MatchesTheSample()
    {
        var model = LotFree();
        var main = model.Sheets[0];
        var cells = SampleDocuments.Cells(main);
        var footerRows = main.Rows.Where(r => r.Kind == RenderRowKinds.Footer).Select(r => r.Number).ToHashSet();
        var dataRows = main.Rows.Where(r => r.Kind == RenderRowKinds.Data).Select(r => r.Number).ToHashSet();
        var mismatches = new List<string>();

        foreach (var e in Expected.RootElement.GetProperty("cells").EnumerateArray())
        {
            var reference = e.GetProperty("ref").GetString()!;
            var type = e.GetProperty("type").GetString()!;
            var (col, row) = Parse(reference);
            var merge = main.Merges.FirstOrDefault(m => m.FirstRow <= row && row <= m.LastRow && m.FirstCol <= col && col <= m.LastCol);
            var covered = merge is not null && (merge.FirstRow != row || merge.FirstCol != col);

            RenderCell? cell;
            if (covered)
            {
                // زیر ادغام: خانه در مدل نیست؛ نمونه هم خالی است و سبکش همان سبک لنگر است
                if (cells.ContainsKey(reference))
                    mismatches.Add($"{reference}: covered by {merge!.A1} but present in the model");
                if (type != "blank")
                    mismatches.Add($"{reference}: covered by {merge!.A1} but the sample has a value");
                cell = cells[Address(merge!.FirstCol, merge.FirstRow)];
            }
            else if (!cells.TryGetValue(reference, out cell))
            {
                mismatches.Add($"{reference}: missing");
                continue;
            }
            else
            {
                CompareValue(reference, e, cell, footerRows.Contains(row), mismatches);
            }

            var style = model.Styles[cell.Style];
            CompareStyle(reference, e.GetProperty("style"), style, cell.Format, shrinkExpected: dataRows.Contains(row),
                compareBorders: merge is null, mismatches);
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    [Fact]
    public void LotFree_MergedRanges_HaveTheSamplesOuterBorders()
    {
        var model = LotFree();
        var main = model.Sheets[0];
        var cells = SampleDocuments.Cells(main);
        foreach (var edge in Expected.RootElement.GetProperty("mergeOuterBorders").EnumerateObject())
        {
            var merge = main.Merges.Single(m => m.A1 == edge.Name);
            var style = model.Styles[cells[Address(merge.FirstCol, merge.FirstRow)].Style];
            var drawn = style.Border is BorderStyles.Thin or BorderStyles.Outer ? "thin" : "none";
            foreach (var side in edge.Value.EnumerateObject())
                Assert.True(side.Value.GetString() == drawn, $"{edge.Name} {side.Name}: {side.Value.GetString()} ≠ {drawn}");
        }
    }

    [Fact]
    public void LotFree_Columns_PageSetup_AndView_MatchTheSample()
    {
        var model = LotFree();
        var main = model.Sheets[0];
        var root = Expected.RootElement;

        var columns = root.GetProperty("columns").EnumerateArray().ToList();
        foreach (var c in columns.Where(c => c.GetProperty("customWidth").GetBoolean()))
        {
            var index = c.GetProperty("min").GetInt32();
            Assert.Equal(c.GetProperty("width").GetDecimal(), main.Columns[index - 1].Width);
            var mismatches = new List<string>();
            CompareStyle($"col {index}", c.GetProperty("style"), model.Styles[main.ColumnStyle!], LayoutDefaults.Format, shrinkExpected: false, compareBorders: true, mismatches);
            Assert.Empty(mismatches);
        }

        Assert.Equal(12, main.Columns.Count);
        Assert.Equal(root.GetProperty("sheetFormat").GetProperty("defaultColWidth").GetDecimal(), model.Page.DefaultColumnWidth);
        Assert.Equal(root.GetProperty("sheetFormat").GetProperty("defaultRowHeight").GetDecimal(), model.Page.RowHeight);
        Assert.All(main.Rows, r => Assert.Equal(16.8m, r.Height));

        var normal = root.GetProperty("sheet").GetProperty("normalFont");
        Assert.Equal(normal.GetProperty("name").GetString(), model.Page.NormalFont);
        Assert.Equal(normal.GetProperty("size").GetDecimal(), model.Page.NormalFontSize);

        var view = root.GetProperty("sheetView");
        Assert.Equal(view.GetProperty("rightToLeft").GetBoolean(), model.Page.RightToLeft);
        Assert.Equal(view.GetProperty("tabSelected").GetBoolean(), main.TabSelected);
        Assert.Equal(view.GetProperty("zoomScale").GetInt32(), model.Page.Zoom);

        var setup = root.GetProperty("pageSetup");
        Assert.Equal(setup.GetProperty("paperSize").GetInt32(), model.Page.PaperSize);
        Assert.Equal(setup.GetProperty("orientation").GetString(), model.Page.Orientation);
        Assert.Equal(setup.GetProperty("fitToPage").GetBoolean(), model.Page.FitToPage);
        Assert.Equal(setup.GetProperty("fitToWidth").GetInt32(), model.Page.FitToWidth);
        Assert.Equal(setup.GetProperty("fitToHeight").GetInt32(), model.Page.FitToHeight);
        Assert.Equal(setup.GetProperty("scale").GetInt32(), model.Page.Scale);

        var margins = root.GetProperty("pageMargins");
        Assert.Equal(margins.GetProperty("left").GetDecimal(), model.Page.Margins.Left);
        Assert.Equal(margins.GetProperty("right").GetDecimal(), model.Page.Margins.Right);
        Assert.Equal(margins.GetProperty("top").GetDecimal(), model.Page.Margins.Top);
        Assert.Equal(margins.GetProperty("bottom").GetDecimal(), model.Page.Margins.Bottom);
        Assert.Equal(margins.GetProperty("header").GetDecimal(), model.Page.Margins.Header);
        Assert.Equal(margins.GetProperty("footer").GetDecimal(), model.Page.Margins.Footer);

        // عنوان‌های تکرارشونده فقط برگهٔ اصلی؛ نه سرصفحه برای قطعی
        Assert.Equal(2, main.PrintTitleRows);
        Assert.Equal(0, model.Sheets[1].PrintTitleRows);
        Assert.Null(model.Page.CenterHeader);
    }

    // ── مقایسه‌ها ───────────────────────────────────────────────────────

    private static void CompareValue(string reference, JsonElement e, RenderCell cell, bool footerRow, List<string> mismatches)
    {
        var type = e.GetProperty("type").GetString();
        var formula = e.TryGetProperty("formula", out var f) ? f.GetString() : null;
        switch (type)
        {
            case "n":
                if (cell.Value.Kind != DocValueKind.Number || cell.Value.Number != e.GetProperty("value").GetDecimal())
                    mismatches.Add($"{reference}: value {cell.Value} ≠ {e.GetProperty("value")}");
                break;
            case "s":
                if (cell.Value.Kind != DocValueKind.Text || cell.Value.Text != e.GetProperty("value").GetString())
                    mismatches.Add($"{reference}: text «{cell.Value}» ≠ «{e.GetProperty("value").GetString()}»");
                break;
            default:
                if (!cell.Value.IsBlank)
                    mismatches.Add($"{reference}: expected blank, got {cell.Value}");
                break;
        }

        if (cell.QuotePrefix)
            mismatches.Add($"{reference}: unexpected quote prefix");

        // تفاوت عمدی E.10: پانویس E =ROWS(B…:B…) است و نمونه عدد ثابت دارد (هم‌مقدار)
        if (footerRow && reference.StartsWith('E') && formula is null)
        {
            if (cell.Formula is null || !RowsPattern().IsMatch(cell.Formula))
                mismatches.Add($"{reference}: formula {cell.Formula} is not ROWS(B…)");
            return;
        }

        if (!string.Equals(formula, cell.Formula, StringComparison.Ordinal))
            mismatches.Add($"{reference}: formula «{cell.Formula}» ≠ «{formula}»");
    }

    private static void CompareStyle(string reference, JsonElement e, RenderStyle style, string format, bool shrinkExpected, bool compareBorders, List<string> mismatches)
    {
        var font = e.GetProperty("font");
        if (font.GetProperty("name").GetString() != style.Font)
            mismatches.Add($"{reference}: font {style.Font}");
        if (font.GetProperty("size").GetDecimal() != style.Size)
            mismatches.Add($"{reference}: size {style.Size}");
        // رنگ «خودکار» (null) همان سیاهِ پوستهٔ نمونه است
        var color = font.GetProperty("color").GetString();
        if ((style.ColorRgb ?? "FF000000") != color)
            mismatches.Add($"{reference}: colour {style.ColorRgb} ≠ {color}");
        var fill = e.GetProperty("fill").ValueKind == JsonValueKind.Null ? null : e.GetProperty("fill").GetString();
        if (fill != style.FillRgb)
            mismatches.Add($"{reference}: fill {style.FillRgb} ≠ {fill}");
        if (e.GetProperty("numFmtId").GetInt32() != 0 || !string.Equals(format, LayoutDefaults.Format, StringComparison.OrdinalIgnoreCase))
            mismatches.Add($"{reference}: number format {format}");
        var align = e.GetProperty("align");
        if (align.GetProperty("h").GetString() != style.HAlign || align.GetProperty("v").GetString() != style.VAlign)
            mismatches.Add($"{reference}: alignment {style.HAlign}/{style.VAlign}");
        // تفاوت عمدی E.10: shrinkToFit روی خانه‌های داده
        var shrink = shrinkExpected || align.GetProperty("shrinkToFit").GetBoolean();
        if (shrink != style.ShrinkToFit)
            mismatches.Add($"{reference}: shrinkToFit {style.ShrinkToFit}");

        if (!compareBorders)
            return;
        var drawn = style.Border == BorderStyles.Thin ? "thin" : "none";
        foreach (var side in e.GetProperty("border").EnumerateObject())
        {
            if (side.Value.GetString() != drawn)
                mismatches.Add($"{reference}: border {side.Name} {drawn} ≠ {side.Value.GetString()}");
        }
    }

    internal static (int Col, int Row) Parse(string reference)
    {
        var letters = new string(reference.TakeWhile(char.IsAsciiLetterUpper).ToArray());
        return (LayoutColumns.Index(letters), int.Parse(reference[letters.Length..], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string Address(int col, int row) => FormulaResolver.Address(col, row);

    [GeneratedRegex(@"^ROWS\(B\d+:B\d+\)$")]
    private static partial Regex RowsPattern();
}
