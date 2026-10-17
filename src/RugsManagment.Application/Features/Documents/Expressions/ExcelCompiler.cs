using System.Text;

namespace RugsManagment.Application.Features.Documents.Expressions;

/// <summary>جای یک ستون در ردیفی که ترجمه می‌شود: نشانی (ستون دیده‌شده) یا مقدارِ همین ردیف (ستون پنهان).</summary>
/// <param name="Letter">حرف ستون اکسل وقتی ستون دیده می‌شود؛ null یعنی پنهان.</param>
/// <param name="Value">مقدار این ردیفِ ستون پنهان (به‌جای نشانی، ثابت گذاشته می‌شود).</param>
public readonly record struct ColumnBinding(string? Letter, DocValue Value)
{
    public bool IsVisible => Letter is not null;

    public static ColumnBinding At(string letter) => new(letter, DocValue.Blank);

    public static ColumnBinding Hidden(DocValue value) => new(null, value);
}

/// <summary>آنچه مترجم برای یک خانه لازم دارد.</summary>
public interface ICompileContext
{
    /// <summary>شمارهٔ ردیف خانه در برگه (نشانی‌های همان ردیف).</summary>
    int Row { get; }

    /// <summary>جای ستون (<c>col.*</c>).</summary>
    ColumnBinding Column(string key);

    /// <summary>مقدار فیلد کاتالوگ برای این خانه؛ فیلدها همیشه ثابت گذاشته می‌شوند (E.4).</summary>
    DocValue Field(string key);

    /// <summary>
    /// متن فرمول یک جمع پانویس (<c>SUM(J3:J6)</c>، <c>SUMPRODUCT(H3:H6,K3:K6)</c>، <c>ROWS(B3:B6)</c>)؛ null یعنی
    /// با فرمول نوشتنی نیست (بیرون از پانویس، یا ستونِ پنهان) و خانه فقط مقدار می‌گیرد.
    /// </summary>
    string? Aggregate(string function, IReadOnlyList<string> columnKeys);
}

/// <summary>نتیجهٔ ترجمه: متن فرمول (بی «=») یا «فقط مقدار».</summary>
/// <param name="Formula">متن فرمول؛ null وقتی خانه فقط مقدار است.</param>
/// <param name="ValueOnly">خانه فرمول ندارد: عبارت برای این ردیف ثابت است، یا متنی بیش از ۲۵۵ نویسه دارد، یا جمعی
/// که با فرمول نوشتنی نیست.</param>
public sealed record CompiledFormula(string? Formula, bool ValueOnly)
{
    public static readonly CompiledFormula Value = new(null, true);
}

/// <summary>
/// مترجم عبارت به فرمول اکسل (spec E.4 «Compilation»):
/// <list type="bullet">
/// <item><c>col.*</c> نشانیِ همان ردیف می‌شود؛ فیلدهای کاتالوگ ثابت گذاشته می‌شوند؛ ستونِ پنهان مقدارِ همان ردیف را
/// می‌گیرد و «× ۱» حذف می‌شود — پس <c>round(col.length*col.width,2)*col.qty</c> بی ستون تعداد همان
/// <c>ROUND(H3*I3, 2)</c> می‌شود.</item>
/// <item>زیرعبارتی که هیچ ستونِ دیده‌شده و هیچ جمعی ندارد پیش از نوشتن حساب و ثابت می‌شود (اکسل تابع <c>jdot</c> ندارد).
/// اگر کل عبارت ثابت شد، خانه «فقط مقدار» است (عنوان سند، شمارهٔ نگله در پانویس).</item>
/// <item>تقسیم بر چیزی که ممکن است صفر شود <c>IF(b=0,"",a/b)</c> می‌شود؛ <c>coalesce(a,b)</c> → <c>IF(a="", b, a)</c>.</item>
/// <item>متن ثابتِ بیش از ۲۵۵ نویسه (مرز اکسل در فرمول) خانه را «فقط مقدار» می‌کند.</item>
/// </list>
/// شکل نوشتار همان نمونهٔ بازرگان است: <c>ROUND(H3*I3, 2)</c> با فاصله بعد از ویرگول.
/// </summary>
public static class ExcelCompiler
{
    /// <summary>بیشترین طول متن ثابت در فرمول اکسل.</summary>
    public const int MaxFormulaTextLiteral = 255;
    public const int MaxFormulaLength = 8000;

    public static CompiledFormula Compile(Expr expr, ICompileContext context)
    {
        var folded = Fold(expr, context);
        if (IsLiteral(folded))
            return CompiledFormula.Value;

        var emitter = new Emitter(context);
        var text = emitter.Emit(folded, textContext: true);
        return emitter.ValueOnly ? CompiledFormula.Value : new CompiledFormula(text, false);
    }

    private static bool IsLiteral(Expr e) => e is NumberLiteral or TextLiteral or BoolLiteral or BlankLiteral;

    /// <summary>بدون ستونِ دیده‌شده و بدون جمع: برای این ردیف ثابت است.</summary>
    private static bool IsRowConstant(Expr expr, ICompileContext context)
        => !ExprWalker.Nodes(expr).Any(n =>
            n is ColumnRef c && context.Column(c.Key).IsVisible
            || n is CallExpr call && ExpressionFunctions.IsAggregate(call.Name));

    private static Expr Fold(Expr expr, ICompileContext context)
    {
        if (IsLiteral(expr))
            return expr;

        if (IsRowConstant(expr, context))
            return ToLiteral(Evaluator.Evaluate(expr, new ConstantContext(context)), expr.Position);

        switch (expr)
        {
            case NegateExpr n:
                return new NegateExpr(Fold(n.Operand, context), n.Position);

            case BinaryExpr b:
            {
                var left = Fold(b.Left, context);
                var right = Fold(b.Right, context);
                if (b.Op == BinaryOp.Multiply)
                {
                    if (right is NumberLiteral { Value: 1m })
                        return left;
                    if (left is NumberLiteral { Value: 1m })
                        return right;
                }

                return new BinaryExpr(b.Op, left, right, b.Position);
            }

            case CallExpr c when ExpressionFunctions.IsAggregate(c.Name):
                return c;

            case CallExpr c:
            {
                var args = c.Args.Select(a => Fold(a, context)).ToList();
                if (c.Name == ExpressionFunctions.Coalesce && IsLiteral(args[0]))
                    return args[0] is BlankLiteral or TextLiteral { Value.Length: 0 } ? args[1] : args[0];
                if (c.Name is ExpressionFunctions.Min or ExpressionFunctions.Max)
                {
                    args = c.Args.Where(a => a is not ColumnRef column || context.Column(column.Key).IsVisible
                        || context.Column(column.Key).Value.Kind == DocValueKind.Number).Select(a => Fold(a, context)).ToList();
                    if (args.Count == 0) return new NumberLiteral(0m, c.Position);
                }
                return new CallExpr(c.Name, args, c.Position);
            }

            default:
                return expr;
        }
    }

    private static Expr ToLiteral(DocValue value, int position) => value.Kind switch
    {
        DocValueKind.Number => new NumberLiteral(value.Number, position),
        DocValueKind.Text => new TextLiteral(value.Text ?? "", position),
        DocValueKind.Bool => new BoolLiteral(value.Bool, position),
        DocValueKind.Date => new TextLiteral(value.AsText(), position),
        _ => new BlankLiteral(position)
    };

    /// <summary>ارزیابی زیرعبارت ثابت: فیلدها و ستون‌های پنهان از بافت ترجمه.</summary>
    private sealed class ConstantContext(ICompileContext context) : IEvaluationContext
    {
        public DocValue Field(string key) => context.Field(key);

        public DocValue Column(string key) => context.Column(key).Value;

        public DocValue Aggregate(string function, IReadOnlyList<string> columnKeys)
            => throw new InvalidOperationException("Aggregates are never folded.");
    }

    private sealed class Emitter(ICompileContext context)
    {
        public bool ValueOnly { get; private set; }
        private int _emittedCharacters;

        // اولویت‌ها مثل اکسل
        private const int CompareLevel = 1;
        private const int ConcatLevel = 2;
        private const int AddLevel = 3;
        private const int MultiplyLevel = 4;
        private const int UnaryLevel = 5;
        private const int AtomLevel = 6;

        public string Emit(Expr expr, bool textContext)
        {
            if (ValueOnly) return "";
            var value = EmitCore(expr, textContext);
            _emittedCharacters += value.Length;
            if (_emittedCharacters > MaxFormulaLength || value.Length > MaxFormulaLength)
            {
                ValueOnly = true;
                return "";
            }
            return value;
        }

        private string EmitCore(Expr expr, bool textContext)
        {
            switch (expr)
            {
                case NumberLiteral n:
                    return DocNumber.General(n.Value);
                case TextLiteral t:
                    return Quote(t.Value);
                case BoolLiteral b:
                    return b.Value ? "TRUE" : "FALSE";
                case BlankLiteral:
                    return textContext ? "\"\"" : "0";
                case ColumnRef c:
                {
                    var binding = context.Column(c.Key);
                    if (!binding.IsVisible)
                        return Emit(ToLiteral(binding.Value, c.Position), textContext);
                    return binding.Letter + context.Row.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                case FieldRef f:
                    return Emit(ToLiteral(context.Field(f.Key), f.Position), textContext);
                case NegateExpr neg:
                    return "-" + Wrap(neg.Operand, UnaryLevel, strict: false, textContext: false);
                case BinaryExpr bin:
                    return EmitBinary(bin, textContext);
                case CallExpr call:
                    return EmitCall(call, textContext);
                default:
                    ValueOnly = true;
                    return "";
            }
        }

        private string EmitBinary(BinaryExpr bin, bool textContext)
        {
            if (bin.Op == BinaryOp.Divide)
            {
                var a = Wrap(bin.Left, MultiplyLevel, strict: false, textContext: false);
                var b = Wrap(bin.Right, MultiplyLevel, strict: true, textContext: false);
                if (bin.Right is NumberLiteral { Value: not 0m })
                    return a + "/" + b;
                // تقسیم بر صفر در اکسل #DIV/0! است؛ سند خانهٔ خالی می‌خواهد (E.4)
                return "IF(" + Emit(bin.Right, false) + "=0," + (textContext ? "\"\"" : "0") + "," + a + "/" + b + ")";
            }

            var (symbol, level, strictRight, text) = bin.Op switch
            {
                BinaryOp.Add => ("+", AddLevel, false, false),
                BinaryOp.Subtract => ("-", AddLevel, true, false),
                BinaryOp.Multiply => ("*", MultiplyLevel, false, false),
                BinaryOp.Concat => ("&", ConcatLevel, false, true),
                BinaryOp.Equal => ("=", CompareLevel, true, true),
                BinaryOp.NotEqual => ("<>", CompareLevel, true, true),
                BinaryOp.Less => ("<", CompareLevel, true, false),
                BinaryOp.LessOrEqual => ("<=", CompareLevel, true, false),
                BinaryOp.Greater => (">", CompareLevel, true, false),
                BinaryOp.GreaterOrEqual => (">=", CompareLevel, true, false),
                _ => throw new InvalidOperationException($"Unknown operator {bin.Op}.")
            };
            return Wrap(bin.Left, level, strict: false, textContext: text) + symbol + Wrap(bin.Right, level, strictRight, textContext: text);
        }

        private string EmitCall(CallExpr call, bool textContext)
        {
            var args = call.Args;
            switch (call.Name)
            {
                case ExpressionFunctions.Round:
                    return "ROUND(" + Emit(args[0], false) + ", " + Emit(args[1], false) + ")";
                case ExpressionFunctions.If:
                    return "IF(" + Emit(args[0], false) + ", " + Emit(args[1], textContext) + ", " + Emit(args[2], textContext) + ")";
                case ExpressionFunctions.Coalesce:
                {
                    var a = Emit(args[0], true);
                    return "IF(" + a + "=\"\", " + Emit(args[1], textContext) + ", " + a + ")";
                }
                case ExpressionFunctions.And:
                case ExpressionFunctions.Or:
                case ExpressionFunctions.Not:
                case ExpressionFunctions.Abs:
                case ExpressionFunctions.Min:
                case ExpressionFunctions.Max:
                    return call.Name.ToUpperInvariant() + "(" + string.Join(", ", args.Select(a => Emit(a, false))) + ")";
                case ExpressionFunctions.Sum:
                case ExpressionFunctions.SumProduct:
                case ExpressionFunctions.Count:
                {
                    var keys = args.OfType<ColumnRef>().Select(c => c.Key).ToList();
                    var text = context.Aggregate(call.Name, keys);
                    if (text is null)
                        ValueOnly = true;
                    return text ?? "";
                }
                default:
                    // jdot روی چیزی که برای ردیف ثابت نیست: اکسل معادلی ندارد
                    ValueOnly = true;
                    return "";
            }
        }

        private string Wrap(Expr child, int parentLevel, bool strict, bool textContext)
        {
            var text = Emit(child, textContext);
            var level = Level(child);
            var needs = level < parentLevel || strict && level == parentLevel;
            if (!needs && child is NumberLiteral { Value: < 0m })
                needs = true;
            return needs ? "(" + text + ")" : text;
        }

        private int Level(Expr e) => e switch
        {
            BinaryExpr b => b.Op switch
            {
                BinaryOp.Add or BinaryOp.Subtract => AddLevel,
                BinaryOp.Multiply => MultiplyLevel,
                BinaryOp.Divide => b.Right is NumberLiteral { Value: not 0m } ? MultiplyLevel : AtomLevel,
                BinaryOp.Concat => ConcatLevel,
                _ => CompareLevel
            },
            NegateExpr => UnaryLevel,
            ColumnRef c when !context.Column(c.Key).IsVisible => LiteralLevel(context.Column(c.Key).Value),
            _ => AtomLevel
        };

        private static int LiteralLevel(DocValue value) => value.Kind == DocValueKind.Number && value.Number < 0m ? UnaryLevel : AtomLevel;

        private string Quote(string text)
        {
            text = DocumentText.XmlSafe(text);
            if (text.Length > MaxFormulaTextLiteral)
                ValueOnly = true;
            var sb = new StringBuilder(text.Length + 2);
            sb.Append('"').Append(text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            return sb.ToString();
        }
    }
}
