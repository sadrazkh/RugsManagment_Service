namespace RugsManagment.Application.Features.Documents.Expressions;

/// <summary>
/// نوع مقدار یک عبارت (spec E.4). نتیجهٔ یک ستون فقط عدد یا متن است؛ بله/نه و تاریخ فقط درون عبارت می‌آیند
/// (<c>if</c> و <c>jdot</c>). <see cref="Error"/> برای جلوگیری از خطاهای پشت‌سرهم بعد از اولین خطاست.
/// </summary>
public enum ExprType
{
    Error = 0,
    Number = 1,
    Text = 2,
    Bool = 3,
    Date = 4
}

/// <summary>عملگرهای دوتایی، به همان معنای اکسل.</summary>
public enum BinaryOp
{
    Add = 0,
    Subtract = 1,
    Multiply = 2,
    Divide = 3,
    Concat = 4,
    Equal = 5,
    NotEqual = 6,
    Less = 7,
    LessOrEqual = 8,
    Greater = 9,
    GreaterOrEqual = 10
}

/// <summary>
/// گرهٔ درخت عبارت. <paramref name="Position"/> جای نویسهٔ شروع (از ۰) است تا پیام خطا بگوید «نزدیک نویسهٔ ۱۲».
/// </summary>
public abstract record Expr(int Position);

/// <summary>عدد ثابت (نوشتار invariant: «2.5»).</summary>
public sealed record NumberLiteral(decimal Value, int Position) : Expr(Position);

/// <summary>متن ثابت (<c>"…"</c>، با <c>""</c> برای گیومهٔ درون متن).</summary>
public sealed record TextLiteral(string Value, int Position) : Expr(Position);

/// <summary><c>true</c> / <c>false</c>.</summary>
public sealed record BoolLiteral(bool Value, int Position) : Expr(Position);

/// <summary>
/// خانهٔ خالی — در متن عبارت نوشته نمی‌شود؛ فقط وقتی پیدا می‌شود که مترجم اکسل یک فیلد بی‌مقدار یا ستون پنهانِ خالی را
/// جایگزین ثابت می‌کند (در حساب ۰، در چسباندن «»).
/// </summary>
public sealed record BlankLiteral(int Position) : Expr(Position);

/// <summary><c>col.&lt;key&gt;</c>: خانهٔ ستون دیگری در همین ردیف.</summary>
public sealed record ColumnRef(string Key, int Position) : Expr(Position);

/// <summary>فیلد کاتالوگ (<c>rug.origin</c>، <c>shipment.documentDate</c>، <c>terms.bale</c>، …).</summary>
public sealed record FieldRef(string Key, int Position) : Expr(Position);

/// <summary>منفی یک‌تایی (<c>-x</c>).</summary>
public sealed record NegateExpr(Expr Operand, int Position) : Expr(Position);

/// <summary>عملگر دوتایی.</summary>
public sealed record BinaryExpr(BinaryOp Op, Expr Left, Expr Right, int Position) : Expr(Position);

/// <summary>فراخوانی تابع؛ نام همیشه حروف کوچک ذخیره می‌شود.</summary>
public sealed record CallExpr(string Name, IReadOnlyList<Expr> Args, int Position) : Expr(Position);

/// <summary>یک خطای عبارت: کد پیام (<c>E_EXPR_*</c> یا <c>E_LAYOUT_UNKNOWN_*</c>) و جای آن در متن.</summary>
/// <param name="Code">کد ثابت؛ متن فارسی از <c>Messages</c> می‌آید.</param>
/// <param name="Position">جای نویسه (از ۰).</param>
/// <param name="Key">نام تابع، کلید ستون یا فیلدی که خطا دارد (نشانهٔ <c>{key}</c>).</param>
public sealed record ExpressionError(string Code, int Position, string? Key = null);

/// <summary>تابع‌های زبان عبارت و تعداد ورودی‌شان (spec E.4).</summary>
public static class ExpressionFunctions
{
    public const string Round = "round";
    public const string If = "if";
    public const string And = "and";
    public const string Or = "or";
    public const string Not = "not";
    public const string Coalesce = "coalesce";
    public const string Abs = "abs";
    public const string Min = "min";
    public const string Max = "max";
    public const string Jdot = "jdot";

    // جمع‌های پانویس — فقط در پانویس و فقط روی ستون
    public const string Sum = "sum";
    public const string SumProduct = "sumproduct";
    public const string Count = "count";

    /// <summary>کمینه و بیشینهٔ تعداد ورودی هر تابع (<c>int.MaxValue</c> یعنی بی‌حد).</summary>
    public static readonly IReadOnlyDictionary<string, (int Min, int Max)> Arity = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
    {
        [Round] = (2, 2),
        [If] = (3, 3),
        [And] = (1, int.MaxValue),
        [Or] = (1, int.MaxValue),
        [Not] = (1, 1),
        [Coalesce] = (2, 2),
        [Abs] = (1, 1),
        [Min] = (1, int.MaxValue),
        [Max] = (1, int.MaxValue),
        [Jdot] = (1, 1),
        [Sum] = (1, 1),
        [SumProduct] = (2, 2),
        [Count] = (0, 0)
    };

    /// <summary>تابع جمعِ پانویس است.</summary>
    public static bool IsAggregate(string name) => name is Sum or SumProduct or Count;
}

/// <summary>پیمایش‌های کمکی روی درخت عبارت.</summary>
public static class ExprWalker
{
    /// <summary>همهٔ گره‌ها (پیش‌ترتیب).</summary>
    public static IEnumerable<Expr> Nodes(Expr expr)
    {
        var stack = new Stack<Expr>();
        stack.Push(expr);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            switch (node)
            {
                case NegateExpr n:
                    stack.Push(n.Operand);
                    break;
                case BinaryExpr b:
                    stack.Push(b.Right);
                    stack.Push(b.Left);
                    break;
                case CallExpr c:
                    for (var i = c.Args.Count - 1; i >= 0; i--)
                        stack.Push(c.Args[i]);
                    break;
            }
        }
    }

    /// <summary>کلید ستون‌هایی که عبارت به آن‌ها اشاره می‌کند (بی تکرار، به ترتیب ظهور).</summary>
    public static IReadOnlyList<string> ColumnKeys(Expr expr)
        => Nodes(expr).OfType<ColumnRef>().Select(c => c.Key).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>کلید فیلدهای کاتالوگ عبارت (بی تکرار).</summary>
    public static IReadOnlyList<string> FieldKeys(Expr expr)
        => Nodes(expr).OfType<FieldRef>().Select(c => c.Key).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>عبارت جمع پانویس دارد.</summary>
    public static bool HasAggregate(Expr expr)
        => Nodes(expr).OfType<CallExpr>().Any(c => ExpressionFunctions.IsAggregate(c.Name));

    /// <summary>عبارت هیچ ستون و هیچ جمعی ندارد، پس برای هر ردیف یک ثابت است (خانهٔ «فقط مقدار»).</summary>
    public static bool IsConstant(Expr expr)
        => !Nodes(expr).Any(n => n is ColumnRef || n is CallExpr c && ExpressionFunctions.IsAggregate(c.Name));
}
