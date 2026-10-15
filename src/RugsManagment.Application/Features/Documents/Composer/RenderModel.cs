using RugsManagment.Application.Features.Documents.Expressions;

namespace RugsManagment.Application.Features.Documents.Composer;

/// <summary>
/// سندِ چیده‌شده (spec E.5): یک مدل برای هر سه خروجی — صفحهٔ چاپ (Razor)، نویسندهٔ اکسل و پیش‌نمایش JSON. هر خانه نشانی
/// اکسل، مقدار دقیق (decimal)، متن فرمول و نام سبکش را دارد؛ پس چاپ و اکسل از یک منبع می‌آیند و جمع‌ها جدا نمی‌شوند.
/// </summary>
/// <param name="Kind">نوع سند (<c>ShipmentDeclaration</c>، <c>RugList</c>، …).</param>
/// <param name="Page">تنظیم صفحه و نما.</param>
/// <param name="Sheets">برگه‌ها به ترتیب (اصلی، بعد خلاصه).</param>
/// <param name="Styles">سبک‌ها با نام (همان نام‌های قالب، به‌اضافهٔ سبک‌های مشتق مثل <c>summaryCell-noborder</c>).</param>
/// <param name="Warnings">هشدارهای ساختن (<c>W_EMPTY_BALE</c>، <c>W_BALE_TOO_LONG</c>، <c>W_CHECK_MISMATCH</c>).</param>
/// <param name="IsDraft">پیش‌نویس: سرصفحهٔ «پیش‌نویس».</param>
/// <param name="HasLots">سند دسته دارد و ستون «تعداد» دیده می‌شود.</param>
/// <param name="Truncated">پیش‌نمایش با <c>maxGroups</c> کوتاه شده (فایل و چاپ هرگز کوتاه نمی‌شوند).</param>
public sealed record RenderModel(
    string Kind,
    RenderPage Page,
    IReadOnlyList<RenderSheet> Sheets,
    IReadOnlyDictionary<string, RenderStyle> Styles,
    IReadOnlyList<DocumentIssue> Warnings,
    bool IsDraft,
    bool HasLots,
    bool Truncated);

/// <summary>
/// تنظیم صفحه (E.9 «Page setup»): A4 عمودی، <c>fitToPage</c> با <c>fitToWidth = 1</c>، <c>fitToHeight = 0</c> و
/// <c>scale = 95</c> هر دو نوشته می‌شوند، همان نمونه. در حالت جایگزین (E.6 #5) <c>FitToPage = false</c> و مقیاس صحیح.
/// </summary>
public sealed record RenderPage(
    int PaperSize,
    string Paper,
    string Orientation,
    bool FitToPage,
    int FitToWidth,
    int FitToHeight,
    int Scale,
    RenderMargins Margins,
    int Zoom,
    bool RightToLeft,
    decimal RowHeight,
    decimal DefaultColumnWidth,
    string NormalFont,
    decimal NormalFontSize,
    string? CenterHeader,
    bool KeepGroupTogether);

/// <summary>حاشیه‌ها به اینچ.</summary>
public sealed record RenderMargins(decimal Left, decimal Right, decimal Top, decimal Bottom, decimal Header, decimal Footer);

/// <summary>یک برگه: ستون‌ها، ردیف‌ها، ادغام‌ها، عنوان‌های تکرارشونده و شکستن‌های دستی صفحه.</summary>
/// <param name="Name">نام برگه («اظهار»، «خلاصه»).</param>
/// <param name="Role"><c>main</c> یا <c>summary</c>.</param>
/// <param name="TabSelected">برگهٔ باز هنگام گشودن فایل.</param>
/// <param name="Columns">ستون‌های برگه با پهنای دقیق (نویسهٔ اکسل).</param>
/// <param name="ColumnStyle">سبکِ خودِ ستون‌ها (<c>&lt;col style&gt;</c>).</param>
/// <param name="Rows">ردیف‌ها به ترتیب، شمارهٔ ۱-پایه.</param>
/// <param name="Merges">بازه‌های ادغام (فقط وقتی rowspan یا colspan &gt; ۱).</param>
/// <param name="PrintTitleRows">ردیف‌های ۱..n که در هر صفحه تکرار می‌شوند (<c>$1:$2</c>)؛ ۰ برای خلاصه.</param>
/// <param name="RowBreaksAfter">شکستن دستی صفحه بعد از این ردیف‌ها (هرگز وسط یک نگله).</param>
/// <param name="Pages">محاسبهٔ صفحه‌بندی (برای آزمون و پیش‌نمایش)؛ null برای برگهٔ بی‌شکستن.</param>
public sealed record RenderSheet(
    string Name,
    string Role,
    bool TabSelected,
    IReadOnlyList<RenderColumn> Columns,
    string? ColumnStyle,
    IReadOnlyList<RenderRow> Rows,
    IReadOnlyList<CellRange> Merges,
    int PrintTitleRows,
    IReadOnlyList<int> RowBreaksAfter,
    PageBreakPlan? Pages)
{
    /// <summary>خانه با نشانی (<c>J57</c>)؛ null اگر نیست یا زیر ادغام است.</summary>
    public RenderCell? Cell(string address)
        => Rows.SelectMany(r => r.Cells).FirstOrDefault(c => string.Equals(c.Address, address, StringComparison.Ordinal));
}

/// <summary>یک ستون برگه.</summary>
/// <param name="Index">شمارهٔ ۱-پایه.</param>
/// <param name="Letter">حرف اکسل.</param>
/// <param name="Key">کلید ستون قالب؛ null برای ستون‌های برگهٔ خلاصه.</param>
/// <param name="Label">عنوان ستون (با واژهٔ نگله/عدل).</param>
/// <param name="Width">پهنا به نویسهٔ اکسل، عیناً همان قالب (۳٫۸۸۶۷۱۸۷۵).</param>
/// <param name="Format">قالب عددی («general»).</param>
public sealed record RenderColumn(int Index, string Letter, string? Key, string Label, decimal Width, string Format);

/// <summary>نوع ردیف.</summary>
public static class RenderRowKinds
{
    public const string Title = "title";
    public const string Header = "header";
    public const string Data = "data";
    public const string Footer = "footer";
    public const string GrandTotal = "grandTotal";
    public const string Summary = "summary";
    public const string EqualsRow = "equals";
    public const string Totals = "totals";
}

/// <summary>یک ردیف.</summary>
/// <param name="Number">شمارهٔ ردیف اکسل (۱-پایه).</param>
/// <param name="RowId">شناسهٔ پایدار («title»، «bale:3:line:2»، «bale:3:footer»، «summary:3»).</param>
/// <param name="Kind"><see cref="RenderRowKinds"/>.</param>
/// <param name="Group">شمارهٔ نگلهٔ ردیف (داده و پانویس و خلاصه)؛ صفحهٔ چاپ هر نگله را یک <c>tbody</c> می‌کند.</param>
/// <param name="Height">ارتفاع ردیف (۱۶٫۸، همیشه <c>customHeight</c>).</param>
/// <param name="Cells">خانه‌ها به ترتیب ستون؛ خانه‌های زیر ادغام نمی‌آیند.</param>
public sealed record RenderRow(int Number, string RowId, string Kind, int? Group, decimal Height, IReadOnlyList<RenderCell> Cells);

/// <summary>
/// یک خانه. <see cref="Value"/> مقدار دقیق است (همان که در <c>&lt;v&gt;</c> نوشته می‌شود)؛ <see cref="Formula"/> متن فرمول
/// بی «=» یا null برای «فقط مقدار».
/// </summary>
/// <param name="Col">شمارهٔ ستون ۱-پایه.</param>
/// <param name="Address">نشانی اکسل (<c>J57</c>).</param>
/// <param name="ColSpan">تعداد ستونِ ادغام‌شده.</param>
/// <param name="RowSpan">تعداد ردیفِ ادغام‌شده.</param>
/// <param name="Value">مقدار دقیق.</param>
/// <param name="Formula">فرمول اکسل بی «=».</param>
/// <param name="Style">نام سبک.</param>
/// <param name="Format">قالب عددی ستون («general»).</param>
/// <param name="QuotePrefix">متنی از داده که با <c>= + - @</c> شروع می‌شود (اکسل آن را فرمول نخواند).</param>
/// <param name="Display">متن نمایش با قالب General و ارقام لاتین (چاپ ارقام را فارسی می‌کند).</param>
public sealed record RenderCell(
    int Col,
    string Address,
    int ColSpan,
    int RowSpan,
    DocValue Value,
    string? Formula,
    string Style,
    string Format,
    bool QuotePrefix,
    string Display)
{
    public bool IsNumber => Value.Kind == DocValueKind.Number;

    public bool ValueOnly => Formula is null;
}

/// <summary>بازهٔ ادغام با شمارهٔ ۱-پایه.</summary>
public sealed record CellRange(int FirstRow, int FirstCol, int LastRow, int LastCol)
{
    /// <summary>نشانی A1 («A3:A6»).</summary>
    public string A1 => Layout.LayoutColumns.Letter(FirstCol) + FirstRow + ":" + Layout.LayoutColumns.Letter(LastCol) + LastRow;

    public override string ToString() => A1;
}

/// <summary>یک سبک حل‌شده (E.2 <c>styles</c>).</summary>
/// <param name="Font">نام قلم.</param>
/// <param name="Size">اندازه.</param>
/// <param name="ColorRgb">رنگ قلم AARRGGBB؛ null یعنی خودکار (سیاه).</param>
/// <param name="FillRgb">زمینهٔ یکدست AARRGGBB؛ null یعنی بی زمینه.</param>
/// <param name="Border">none | thin | outer.</param>
/// <param name="HAlign">left | center | right | general.</param>
/// <param name="VAlign">top | center | bottom.</param>
/// <param name="ShrinkToFit">متن بلند کوچک شود تا در خانه جا شود.</param>
public sealed record RenderStyle(
    string Font,
    decimal Size,
    string? ColorRgb,
    string? FillRgb,
    string Border,
    string HAlign,
    string VAlign,
    bool ShrinkToFit);

/// <summary>یک هشدار یا خطای سند با متن فارسی.</summary>
/// <param name="Code">کد (<c>W_EMPTY_BALE</c>، …).</param>
/// <param name="Message">متن فارسی پرشده.</param>
/// <param name="Group">شمارهٔ نگله، اگر مربوط به یک نگله است.</param>
/// <param name="Path">مسیر JSON قالب، اگر مربوط به قالب است.</param>
public sealed record DocumentIssue(string Code, string Message, int? Group = null, string? Path = null);
