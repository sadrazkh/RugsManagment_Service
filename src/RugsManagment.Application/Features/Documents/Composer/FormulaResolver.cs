using System.Globalization;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;

namespace RugsManagment.Application.Features.Documents.Composer;

/// <summary>بازهٔ ردیف‌های دادهٔ یک گروه (اول تا آخر، هر دو شامل).</summary>
public readonly record struct RowSpan(int First, int Last);

/// <summary>
/// ستون‌های برگهٔ اصلی پس از چیدن: کدام کلید کدام حرف را گرفت، کدام پنهان ماند، ستون تعداد کجاست و جمع پانویس
/// وزن‌دار است یا نه. پانویس‌ها و پیوندهای خلاصه از همین نقشه حرف می‌گیرند، نه از حرف ثابت قالب.
/// </summary>
public sealed class SheetColumnMap
{
    private readonly Dictionary<string, int> _visibleIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ColumnLayout> _all = new(StringComparer.Ordinal);

    public SheetColumnMap(SheetLayout sheet, bool hasLots, string lotRow)
    {
        Visible = LayoutColumns.Visible(sheet, hasLots);
        for (var i = 0; i < Visible.Count; i++)
        {
            if (Visible[i].Key is { } key)
                _visibleIndex.TryAdd(key, i + 1);
        }

        foreach (var c in sheet.Columns)
        {
            if (c.Key is { } key)
                _all.TryAdd(key, c);
        }

        QtyKey = (Visible.FirstOrDefault(c => c.Field == RugFieldKeys.Quantity)
            ?? sheet.Columns.FirstOrDefault(c => c.Field == RugFieldKeys.Quantity))?.Key;
        QtyVisible = QtyKey is not null && _visibleIndex.ContainsKey(QtyKey);
        Weighted = lotRow == LotRowModes.PerPiece && QtyVisible;
        var keyColumn = sheet.Group?.KeyColumn;
        var countColumn = Visible.FirstOrDefault(c => c.Key != keyColumn) ?? Visible.FirstOrDefault();
        CountKey = countColumn?.Key;
    }

    /// <summary>ستون‌های دیده‌شده به ترتیب.</summary>
    public IReadOnlyList<ColumnLayout> Visible { get; }

    /// <summary>کلید ستون تعداد (<c>rug.qty</c>)، دیده‌شده یا نه.</summary>
    public string? QtyKey { get; }

    /// <summary>ستون تعداد دیده می‌شود (سند دسته دارد).</summary>
    public bool QtyVisible { get; }

    /// <summary>
    /// جمع پانویس با تعداد وزن می‌خورد: <c>lotRow = perPiece</c> و ستون تعداد دیده‌شده (E.2: «sum(of) is weighted by qty
    /// (SUMPRODUCT) when lotRow = perPiece and the qty column is visible»).
    /// </summary>
    public bool Weighted { get; }

    /// <summary>ستونی که <c>ROWS()</c> روی آن شمرده می‌شود وقتی ستون تعداد پنهان است (اولین ستون دیده‌شدهٔ غیر کلید: B).</summary>
    public string? CountKey { get; }

    /// <summary>شمارهٔ ۱-پایهٔ ستون دیده‌شده؛ ۰ اگر پنهان یا ناشناخته.</summary>
    public int IndexOf(string? key) => key is not null && _visibleIndex.TryGetValue(key, out var i) ? i : 0;

    /// <summary>حرف ستون دیده‌شده؛ null اگر پنهان.</summary>
    public string? LetterOf(string? key) => IndexOf(key) is > 0 and var i ? LayoutColumns.Letter(i) : null;

    /// <summary>تعریف ستون با کلید (دیده‌شده یا پنهان).</summary>
    public ColumnLayout? Column(string key) => _all.GetValueOrDefault(key);
}

/// <summary>
/// پیوند نشانی‌های منطقی به A1 پس از چیدن ردیف‌ها (spec E.5 گام ۳): بازهٔ گروه برای جمع‌های پانویس، نشانی خانه‌های پانویس
/// با شناسه (نه حرف) برای پیوندهای خلاصه، و پیوند میان برگه‌ها. شکستن صفحه ردیفی اضافه نمی‌کند، پس نشانی‌ها پایدارند.
/// </summary>
public static class FormulaResolver
{
    /// <summary>طول بیشینهٔ فرمولی که می‌نویسیم (اکسل ۸۱۹۲ را می‌پذیرد)؛ بیشتر از آن خانه «فقط مقدار» می‌شود.</summary>
    public const int MaxFormulaLength = 8000;

    /// <summary>نشانی A1.</summary>
    public static string Address(int col, int row) => LayoutColumns.Letter(col) + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>بازهٔ یک ستون: <c>J3:J6</c>.</summary>
    public static string Range(string letter, RowSpan span)
        => letter + span.First.ToString(CultureInfo.InvariantCulture) + ":" + letter + span.Last.ToString(CultureInfo.InvariantCulture);

    /// <summary>ارجاع به خانهٔ برگهٔ دیگر: <c>'اظهار'!C7</c> (نام همیشه در گیومهٔ تکی، گیومهٔ درون نام دوتایی).</summary>
    public static string SheetReference(string sheetName, string address)
        => "'" + sheetName.Replace("'", "''", StringComparison.Ordinal) + "'!" + address;

    /// <summary>
    /// فرمول یک جمع پانویس روی یک یا چند بازه (E.9 «Footer formulas»):
    /// <list type="bullet">
    /// <item><c>count</c>: <c>SUM(H3:H6)</c> با ستون تعداد دیده‌شده، وگرنه <c>ROWS(B3:B6)</c>؛</item>
    /// <item><c>sum(x)</c>: وزن‌دار <c>SUMPRODUCT(H3:H6,K3:K6)</c>، وگرنه <c>SUM(J3:J6)</c> (جمعِ خودِ ستون تعداد هرگز وزن
    /// نمی‌خورد)؛</item>
    /// <item><c>sumproduct(a, b)</c>: <c>SUMPRODUCT(a,b)</c>.</item>
    /// </list>
    /// null یعنی با فرمول نوشتنی نیست (ستونِ پنهان، بی بازه، یا فرمول بیش از اندازه) و خانه فقط مقدار می‌گیرد.
    /// </summary>
    public static string? Aggregate(string function, IReadOnlyList<string> keys, IReadOnlyList<RowSpan> spans, SheetColumnMap map)
    {
        if (spans.Count == 0 || spans.Count > 255)
            return null;

        string? text;
        switch (function)
        {
            case ExpressionFunctions.Count:
                if (map.QtyVisible)
                    text = "SUM(" + string.Join(",", spans.Select(s => Range(map.LetterOf(map.QtyKey)!, s))) + ")";
                else if (map.LetterOf(map.CountKey) is { } countLetter)
                    text = string.Join("+", spans.Select(s => "ROWS(" + Range(countLetter, s) + ")"));
                else
                    text = null;
                break;

            case ExpressionFunctions.Sum:
            {
                var letter = keys.Count == 1 ? map.LetterOf(keys[0]) : null;
                if (letter is null)
                {
                    text = null;
                }
                else if (map.Weighted && keys[0] != map.QtyKey)
                {
                    var qty = map.LetterOf(map.QtyKey)!;
                    text = string.Join("+", spans.Select(s => "SUMPRODUCT(" + Range(qty, s) + "," + Range(letter, s) + ")"));
                }
                else
                {
                    text = "SUM(" + string.Join(",", spans.Select(s => Range(letter, s))) + ")";
                }

                break;
            }

            case ExpressionFunctions.SumProduct:
            {
                var a = keys.Count == 2 ? map.LetterOf(keys[0]) : null;
                var b = keys.Count == 2 ? map.LetterOf(keys[1]) : null;
                text = a is null || b is null
                    ? null
                    : string.Join("+", spans.Select(s => "SUMPRODUCT(" + Range(a, s) + "," + Range(b, s) + ")"));
                break;
            }

            default:
                text = null;
                break;
        }

        return text is { Length: > MaxFormulaLength } ? null : text;
    }

    /// <summary>
    /// مقدار یک جمع پانویس از مقدارهای ردیف‌ها، دقیقاً هم‌معنای <see cref="Aggregate"/> تا مقدار ذخیره‌شده با محاسبهٔ دوبارهٔ
    /// اکسل یکی باشد.
    /// </summary>
    public static DocValue AggregateValue(string function, IReadOnlyList<string> keys, IReadOnlyList<IRowValues> rows, SheetColumnMap map)
    {
        decimal Qty(IRowValues r) => map.QtyKey is null ? 1m : r.Value(map.QtyKey).AsNumber();

        switch (function)
        {
            case ExpressionFunctions.Count:
                return DocValue.FromNumber(map.QtyVisible ? rows.Sum(Qty) : rows.Count);

            case ExpressionFunctions.Sum:
            {
                if (keys.Count != 1)
                    return DocValue.Blank;
                var key = keys[0];
                var weighted = map.Weighted && key != map.QtyKey;
                return DocValue.FromNumber(rows.Sum(r => (weighted ? Qty(r) : 1m) * r.Value(key).AsNumber()));
            }

            case ExpressionFunctions.SumProduct:
                return keys.Count != 2
                    ? DocValue.Blank
                    : DocValue.FromNumber(rows.Sum(r => r.Value(keys[0]).AsNumber() * r.Value(keys[1]).AsNumber()));

            default:
                return DocValue.Blank;
        }
    }

    /// <summary>فرمول جمع یک ستون از برگهٔ خلاصه: <c>SUM(E3:E38)</c>.</summary>
    public static string ColumnSum(string letter, RowSpan span) => "SUM(" + Range(letter, span) + ")";
}

/// <summary>مقدارهای یک ردیف داده با کلید ستون (برای جمع‌های پانویس).</summary>
public interface IRowValues
{
    DocValue Value(string columnKey);
}
