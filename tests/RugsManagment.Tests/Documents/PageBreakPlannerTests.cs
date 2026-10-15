using RugsManagment.Application.Features.Documents.Composer;

namespace RugsManagment.Tests.Documents;

/// <summary>
/// شکستن صفحه با رعایت نگله (spec E.7، L.1 «PageBreakPlanner»): ظرفیت با عددهای کارشده (۷۳۴ px → s = 1، C = 43؛ ۷۷۲ px →
/// ۵۷۹ pt، s = 0.9787، C = 44؛ جایگزین s = 0.97، C = 45)، شکستن پیش از بلوکی که جا نمی‌شود، نگلهٔ بلندتر از یک صفحه با
/// <c>W_BALE_TOO_LONG</c>، و روی دادهٔ نمونه همان صفحه‌بندی دستی بازرگان (۱–۱۰، ۱۱–۱۹، ۲۰–۲۲، ۲۳–۲۷).
/// </summary>
public class PageBreakPlannerTests
{
    private static readonly decimal[] NoLotWidths = [3.88671875m, 6.5546875m, 7.88671875m, 8.6640625m, 9.109375m, 8m, 10.109375m, 6.109375m, 6.44140625m, 7.109375m, 14.88671875m, 16m];

    private static readonly decimal[] LotWidths = [3.88671875m, 6.5546875m, 7.88671875m, 8.6640625m, 9.109375m, 8m, 10.109375m, 5.5m, 6.109375m, 6.44140625m, 7.109375m, 14.88671875m, 16m];

    private static readonly RenderMargins A4Margins = new(0.2m, 0.2m, 0.5m, 0.5m, 0.3m, 0.3m);

    private static PageBreakPlan Plan(decimal[] widths, IReadOnlyList<PageBlock> blocks, bool fallback = false)
        => PageBreakPlanner.Plan(new PageBreakInput(widths, 8.27m, 11.69m, A4Margins, 16.8m, 2, 3, blocks, fallback));

    /// <summary>بلوک‌های نمونه (ردیف‌های داده + پانویس هر نگله) پشت سر هم از ردیف ۳.</summary>
    private static List<PageBlock> SampleBlocks()
    {
        var data = SampleDocuments.Shipment();
        var blocks = new List<PageBlock>();
        var row = 3;
        foreach (var bale in data.Bales)
        {
            var count = bale.Lines.Count + 1;
            blocks.Add(new PageBlock(row, count, bale.Number));
            row += count;
        }

        return blocks;
    }

    [Fact]
    public void ColumnPixels_FollowExcelsRule()
    {
        Assert.Equal(734, NoLotWidths.Sum(w => PageBreakPlanner.ColumnPixels(w)));
        Assert.Equal(772, LotWidths.Sum(w => PageBreakPlanner.ColumnPixels(w)));
        Assert.Equal(27, PageBreakPlanner.ColumnPixels(3.88671875m)); // ⌊(995 + 18) / 256 × 7⌋
        Assert.Equal(112, PageBreakPlanner.ColumnPixels(16m));
    }

    [Fact]
    public void Capacity_NoLots_Is43()
    {
        var plan = Plan(NoLotWidths, []);
        Assert.Equal(734, plan.SheetWidthPx);
        Assert.Equal(550.5m, plan.SheetWidthPt);
        Assert.Equal(1m, plan.Scale);
        Assert.Equal(43, plan.Capacity);
    }

    [Fact]
    public void Capacity_Lots_Is44()
    {
        var plan = Plan(LotWidths, []);
        Assert.Equal(772, plan.SheetWidthPx);
        Assert.Equal(579m, plan.SheetWidthPt);
        Assert.Equal(0.9787m, Math.Round(plan.Scale, 4));
        Assert.Equal(97, plan.WrittenScale);
        Assert.Equal(44, plan.Capacity);
    }

    [Fact]
    public void Capacity_Fallback_Is45()
    {
        var plan = Plan(LotWidths, [], fallback: true);
        Assert.Equal(0.97m, plan.Scale);
        Assert.Equal(45, plan.Capacity);
        Assert.Equal(100, Plan(NoLotWidths, [], fallback: true).WrittenScale);
    }

    [Fact]
    public void SampleData_ReproducesTheMerchantsPages()
    {
        var plan = Plan(LotWidths, SampleBlocks());
        Assert.Equal([1, 11, 20, 23, 28], plan.PageFirstGroups.Take(5));
        Assert.Equal(43, plan.RowBreaksAfter[0]);
        Assert.Empty(plan.TooLongGroups);
    }

    [Fact]
    public void SampleData_Fallback_Page2Holds11To20()
    {
        var plan = Plan(LotWidths, SampleBlocks(), fallback: true);
        Assert.Equal([1, 11, 21], plan.PageFirstGroups.Take(3));
    }

    [Fact]
    public void Breaks_FallOnlyBetweenBlocks()
    {
        var blocks = SampleBlocks();
        var ends = blocks.Select(b => b.FirstRow + b.RowCount - 1).ToHashSet();
        var plan = Plan(LotWidths, blocks);
        Assert.All(plan.RowBreaksAfter, after => Assert.Contains(after, ends));
    }

    [Fact]
    public void ABlockThatDoesNotFit_BreaksBeforeIt()
    {
        // C = 43: صفحهٔ اول ۲ ردیف عنوان + ۴۰ ردیف؛ بلوک ۵ ردیفی بعدی جا نمی‌شود
        var plan = Plan(NoLotWidths, [new PageBlock(3, 40, 1), new PageBlock(43, 5, 2), new PageBlock(48, 3, 3)]);
        Assert.Equal([42], plan.RowBreaksAfter);
        Assert.Equal([1, 2], plan.PageFirstGroups);

        // دقیقاً پر: شکستنی لازم نیست
        Assert.Empty(Plan(NoLotWidths, [new PageBlock(3, 41, 1)]).RowBreaksAfter);
    }

    [Fact]
    public void LaterPages_HoldCapacityMinusTheTitleRows()
    {
        // صفحهٔ ۲ و بعد: C − 2 = 41 ردیف بدنه
        var plan = Plan(NoLotWidths, [new PageBlock(3, 41, 1), new PageBlock(44, 41, 2), new PageBlock(85, 1, 3)]);
        Assert.Equal([43, 84], plan.RowBreaksAfter);
    }

    [Fact]
    public void ABlockTallerThanAPage_IsBrokenInside_AndReported()
    {
        var plan = Plan(NoLotWidths, [new PageBlock(3, 5, 1), new PageBlock(8, 100, 2), new PageBlock(108, 3, 3)]);
        Assert.Equal([2], plan.TooLongGroups);
        // شکستن پیش از نگلهٔ ۲، بعد درونش هر ۴۱ ردیف
        Assert.Equal(7, plan.RowBreaksAfter[0]);
        Assert.Equal([7, 48, 89], plan.RowBreaksAfter.Take(3));
    }

    [Fact]
    public void Composer_ReportsTooLongBales()
    {
        var data = SampleDocuments.Shipment(b => b == 1);
        var bale = data.Bales[0];
        var lines = Enumerable.Range(1, 60).Select(i => bale.Lines[0] with { Seq = i }).ToList();
        data = data with { Bales = [bale with { Lines = lines, Pieces = null, AreaM2 = null, Usd = null }] };
        var model = SampleDocuments.Compose(data);
        var warning = Assert.Single(model.Warnings);
        Assert.Equal("W_BALE_TOO_LONG", warning.Code);
        Assert.Equal("نگلهٔ ۱ از یک صفحه بلندتر است و میان دو صفحه شکست.", warning.Message);
    }
}
