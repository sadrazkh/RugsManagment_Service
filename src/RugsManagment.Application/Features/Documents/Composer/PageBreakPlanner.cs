namespace RugsManagment.Application.Features.Documents.Composer;

/// <summary>یک بلوک صفحه‌بندی: ردیف‌های داده و پانویس یک نگله.</summary>
/// <param name="FirstRow">ردیف اول بلوک.</param>
/// <param name="RowCount">تعداد ردیف (داده + پانویس).</param>
/// <param name="Group">شمارهٔ نگله.</param>
public sealed record PageBlock(int FirstRow, int RowCount, int Group);

/// <summary>ورودی صفحه‌بندی.</summary>
/// <param name="ColumnWidths">پهنای ستون‌های دیده‌شده به نویسهٔ اکسل.</param>
/// <param name="PaperWidthInches">پهنای کاغذ (A4 عمودی ۸٫۲۷).</param>
/// <param name="PaperHeightInches">بلندی کاغذ (A4 عمودی ۱۱٫۶۹).</param>
/// <param name="Margins">حاشیه‌ها.</param>
/// <param name="RowHeight">ارتفاع هر ردیف (۱۶٫۸ نقطه).</param>
/// <param name="PrintTitleRows">ردیف‌هایی که در هر صفحه تکرار می‌شوند.</param>
/// <param name="FirstBodyRow">ردیف اول بدنه (بعد از عنوان و سرستون: ۳).</param>
/// <param name="Blocks">بلوک‌ها به ترتیب.</param>
/// <param name="FallbackScale">حالت جایگزین E.6 #5: مقیاس صحیحِ نوشته‌شده (⌊s × 100⌋ / 100) به‌جای «جا شدن در پهنا».</param>
/// <param name="MaxDigitWidthPx">پهنای بزرگ‌ترین رقمِ قلم Normal به پیکسل (Calibri 11 = ۷).</param>
public sealed record PageBreakInput(
    IReadOnlyList<decimal> ColumnWidths,
    decimal PaperWidthInches,
    decimal PaperHeightInches,
    RenderMargins Margins,
    decimal RowHeight,
    int PrintTitleRows,
    int FirstBodyRow,
    IReadOnlyList<PageBlock> Blocks,
    bool FallbackScale = false,
    int MaxDigitWidthPx = PageBreakPlanner.CalibriMaxDigitWidthPx,
    bool FitToWidth = true,
    int ScalePercent = 100);

/// <summary>نتیجهٔ صفحه‌بندی.</summary>
/// <param name="SheetWidthPx">پهنای برگه به پیکسل (قاعدهٔ اکسل).</param>
/// <param name="SheetWidthPt">پهنای برگه به نقطه.</param>
/// <param name="Scale">مقیاس s (در حالت عادی گرد نشده؛ در حالت جایگزین همان صحیحِ نوشته‌شده / ۱۰۰).</param>
/// <param name="WrittenScale">مقیاس صحیحی که در حالت جایگزین نوشته می‌شود (⌊s × 100⌋).</param>
/// <param name="Capacity">C: ردیف‌های صفحهٔ اول (با عنوان و سرستون)؛ صفحه‌های بعد C − ردیف‌های تکرارشونده ردیف بدنه.</param>
/// <param name="RowBreaksAfter">شکستن دستی بعد از این ردیف‌ها.</param>
/// <param name="PageFirstGroups">نگلهٔ اول هر صفحه.</param>
/// <param name="TooLongGroups">نگله‌هایی که از یک صفحه بلندترند و درونشان شکسته شد (<c>W_BALE_TOO_LONG</c>).</param>
public sealed record PageBreakPlan(
    int SheetWidthPx,
    decimal SheetWidthPt,
    decimal Scale,
    int WrittenScale,
    int Capacity,
    IReadOnlyList<int> RowBreaksAfter,
    IReadOnlyList<int> PageFirstGroups,
    IReadOnlyList<int> TooLongGroups);

/// <summary>
/// شکستن صفحه با رعایت نگله (spec E.7 «Bale-aware page breaks»، <c>keepGroupTogether</c>): هیچ نگله‌ای میان دو صفحه
/// نمی‌شکند، مگر خودش از یک صفحه بلندتر باشد.
///
/// چرا حساب خودمان و نه صفحه‌بندی اکسل: اکسل با «جا شدن در پهنا» هر جا که جا تمام شد می‌بُرد، حتی وسط یک نگله؛ بازرگان
/// هر صفحه را با دست به نگله‌های کامل می‌چید (۱–۱۰، ۱۱–۱۹، ۲۰–۲۲، ۲۳–۲۷). پس ظرفیت صفحه را با همان قاعدهٔ اکسل
/// حساب می‌کنیم و پیش از هر نگله‌ای که جا نمی‌شود شکستن دستی می‌گذاریم. آزمایش روز اول (E.6 #5) نشان داد اکسل این
/// شکستن‌ها را در حالت «جا شدن در پهنا» با <c>fitToHeight = 0</c> رعایت می‌کند.
/// <list type="number">
/// <item>بلندی چاپی H = (بلندی کاغذ − بالا − پایین) × ۷۲ = ۷۶۹٫۶۸ نقطه.</item>
/// <item>پیکسل هر ستون = ⌊(256w + ⌊128/7⌋)/256 × 7⌋ با پهنای رقم قلم Normal (Calibri 11 = ۷)؛ پهنا به نقطه = Σpx × 72/96.</item>
/// <item>s = min(1, پهنای چاپی ÷ پهنای برگه)، گرد نشده (مقیاس خود اکسل گرد شدهٔ رو به پایین است، پس s محتاطانه است).</item>
/// <item>C = ⌊H ÷ (16.8 × s)⌋ − 2 ردیف ذخیره.</item>
/// <item>صفحهٔ اول C ردیف؛ صفحه‌های بعد C − 2 ردیف بدنه، چون عنوان‌ها تکرار می‌شوند.</item>
/// </list>
/// بی دسته (۱۲ ستون): ۷۳۴ px = ۵۵۰٫۵ pt، s = 1، C = 43؛ با دسته (۱۳ ستون): ۷۷۲ px = ۵۷۹ pt، s = 0.9787، C = 44.
/// </summary>
public static class PageBreakPlanner
{
    /// <summary>پهنای بزرگ‌ترین رقم Calibri 11 به پیکسل در ۹۶ dpi.</summary>
    public const int CalibriMaxDigitWidthPx = 7;

    /// <summary>ردیف‌های ذخیره در هر صفحه.</summary>
    public const int SlackRows = 2;

    private const decimal PointsPerInch = 72m;

    /// <summary>پیکسل یک ستون با قاعدهٔ اکسل.</summary>
    public static int ColumnPixels(decimal width, int maxDigitWidthPx = CalibriMaxDigitWidthPx)
    {
        var padding = Math.Floor(128m / maxDigitWidthPx);
        return (int)Math.Floor((256m * width + padding) / 256m * maxDigitWidthPx);
    }

    /// <summary>ظرفیت صفحه C برای یک مقیاس.</summary>
    public static int Capacity(decimal printableHeightPt, decimal rowHeight, decimal scale)
        => (int)Math.Floor(printableHeightPt / (rowHeight * scale)) - SlackRows;

    public static PageBreakPlan Plan(PageBreakInput input)
    {
        var px = input.ColumnWidths.Sum(w => ColumnPixels(w, input.MaxDigitWidthPx));
        var sheetPt = px * PointsPerInch / 96m;
        var printableWidth = (input.PaperWidthInches - input.Margins.Left - input.Margins.Right) * PointsPerInch;
        var printableHeight = (input.PaperHeightInches - input.Margins.Top - input.Margins.Bottom) * PointsPerInch;
        var scale = sheetPt <= 0m ? 1m : Math.Min(1m, printableWidth / sheetPt);
        var written = (int)Math.Floor(scale * 100m);
        if (input.FallbackScale)
            scale = written / 100m;
        if (!input.FitToWidth)
        {
            written = Math.Clamp(input.ScalePercent, 10, 400);
            scale = written / 100m;
        }

        var capacity = Math.Max(input.PrintTitleRows + 1, Capacity(printableHeight, input.RowHeight, scale));

        var breaks = new List<int>();
        var firstGroups = new List<int>();
        var tooLong = new List<int>();
        var used = input.FirstBodyRow - 1; // عنوان و سرستون صفحهٔ اول
        var pageCapacity = capacity;
        var pageHasBody = false;

        foreach (var block in input.Blocks)
        {
            if (firstGroups.Count == 0)
                firstGroups.Add(block.Group);

            if (used + block.RowCount > pageCapacity && pageHasBody)
            {
                breaks.Add(block.FirstRow - 1);
                firstGroups.Add(block.Group);
                used = input.PrintTitleRows;
                pageHasBody = false;
            }

            if (used + block.RowCount <= pageCapacity)
            {
                used += block.RowCount;
                pageHasBody = true;
                continue;
            }

            // بلوکی بلندتر از یک صفحهٔ کامل: درونش شکسته می‌شود (W_BALE_TOO_LONG)
            tooLong.Add(block.Group);
            var row = block.FirstRow;
            var remaining = block.RowCount;
            while (used + remaining > pageCapacity)
            {
                var fits = Math.Max(1, pageCapacity - used);
                row += fits;
                remaining -= fits;
                breaks.Add(row - 1);
                used = input.PrintTitleRows;
            }

            used += remaining;
            pageHasBody = true;
        }

        return new PageBreakPlan(px, sheetPt, scale, written, capacity, breaks, firstGroups, tooLong);
    }
}
