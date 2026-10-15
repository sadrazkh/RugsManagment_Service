using System.Globalization;
using RugsManagment.Domain.Common;

namespace RugsManagment.Application.Features.Documents.Expressions;

/// <summary>نوع یک مقدار سند.</summary>
public enum DocValueKind
{
    Blank = 0,
    Number = 1,
    Text = 2,
    Bool = 3,
    Date = 4
}

/// <summary>
/// مقدار یک خانه یا یک فیلد در موتور سند، با معنای اکسل: خانهٔ خالی در حساب ۰ است و در چسباندن «».
/// عدد همیشه <c>decimal</c> است (نه double) تا مقدار ذخیره‌شده در اکسل (<c>&lt;v&gt;</c>) دقیقاً همان ۳٫۸ باشد که
/// بازرگان می‌بیند، نه ۳٫۷۹۹۹۹۹۹.
/// </summary>
public readonly record struct DocValue(DocValueKind Kind, decimal Number, string? Text, bool Bool, DateOnly Date)
{
    public static readonly DocValue Blank = default;

    public static DocValue FromNumber(decimal value) => new(DocValueKind.Number, value, null, false, default);

    public static DocValue FromText(string value) => new(DocValueKind.Text, 0m, value, false, default);

    public static DocValue FromBool(bool value) => new(DocValueKind.Bool, 0m, null, value, default);

    public static DocValue FromDate(DateOnly value) => new(DocValueKind.Date, 0m, null, false, value);

    /// <summary>عدد اختیاری؛ null خانهٔ خالی است.</summary>
    public static DocValue Of(decimal? value) => value is { } v ? FromNumber(v) : Blank;

    /// <summary>متن اختیاری؛ null یا رشتهٔ خالی خانهٔ خالی است.</summary>
    public static DocValue Of(string? value) => string.IsNullOrEmpty(value) ? Blank : FromText(value);

    /// <summary>تاریخ اختیاری.</summary>
    public static DocValue Of(DateOnly? value) => value is { } v ? FromDate(v) : Blank;

    public bool IsBlank => Kind == DocValueKind.Blank;

    /// <summary>مقدار عددی به روش اکسل: خالی ۰، بله ۱، متنِ عددی خودش، بقیه ۰.</summary>
    public decimal AsNumber() => Kind switch
    {
        DocValueKind.Number => Number,
        DocValueKind.Bool => Bool ? 1m : 0m,
        DocValueKind.Text when decimal.TryParse(Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) => n,
        _ => 0m
    };

    /// <summary>متن به روش اکسل (قالب General برای عدد).</summary>
    public string AsText() => Kind switch
    {
        DocValueKind.Number => DocNumber.General(Number),
        DocValueKind.Text => Text ?? "",
        DocValueKind.Bool => Bool ? "TRUE" : "FALSE",
        DocValueKind.Date => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => ""
    };

    /// <summary>بله/نه: خالی و صفر «نه».</summary>
    public bool AsBool() => Kind switch
    {
        DocValueKind.Bool => Bool,
        DocValueKind.Number => Number != 0m,
        _ => false
    };

    public override string ToString() => AsText();
}

/// <summary>نوشتن عدد به شکل اکسل.</summary>
public static class DocNumber
{
    /// <summary>
    /// قالب General اکسل برای عددهای سند: invariant و بی صفرهای زائد («4.37»، «2.9»، «45»، «20322.5»). نمونهٔ بازرگان
    /// <c>numFmts</c> ندارد، پس اکسل همین را نشان می‌دهد (E.7).
    /// </summary>
    public static string General(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Contains('.', StringComparison.Ordinal))
            text = text.TrimEnd('0').TrimEnd('.');
        return text == "-0" ? "0" : text;
    }

    /// <summary>
    /// قالب عددی ستون (<c>format</c> قالب): «general» → <see cref="General"/>؛ بقیه رشتهٔ قالب .NET/اکسل ساده («0.00»، «#,0»).
    /// </summary>
    public static string Format(decimal value, string? format)
        => string.IsNullOrEmpty(format) || string.Equals(format, "general", StringComparison.OrdinalIgnoreCase)
            ? General(value)
            : value.ToString(format, CultureInfo.InvariantCulture);
}

/// <summary>
/// آنچه ارزیاب از بیرون می‌خواهد: مقدار فیلد کاتالوگ، مقدار ستون دیگرِ همین ردیف، و جمع‌های پانویس.
/// </summary>
public interface IEvaluationContext
{
    /// <summary>مقدار یک فیلد کاتالوگ (ناشناخته: خالی).</summary>
    DocValue Field(string key);

    /// <summary>مقدار ستون دیگری در همین ردیف (فقط در عبارت ستون).</summary>
    DocValue Column(string key);

    /// <summary>جمع پانویس (<c>sum</c>، <c>sumproduct</c>، <c>count</c>) روی ستون‌های داده؛ فقط در پانویس.</summary>
    DocValue Aggregate(string function, IReadOnlyList<string> columnKeys);
}

/// <summary>
/// ارزیاب عبارت با قاعده‌های <c>Num</c> (G.4): گرد کردن فقط با <see cref="Num.Round(decimal, int)"/> (نصفه دور از صفر)،
/// همان که ROUND اکسل می‌کند؛ پس مقدار ذخیره‌شده در فایل با مقدار بعد از محاسبهٔ دوبارهٔ اکسل یکی است.
/// تقسیم بر صفر خالی می‌دهد (اکسل: <c>IF(b=0,"",a/b)</c>).
/// </summary>
public static class Evaluator
{
    public static DocValue Evaluate(Expr expr, IEvaluationContext context)
    {
        switch (expr)
        {
            case NumberLiteral n:
                return DocValue.FromNumber(n.Value);
            case TextLiteral t:
                return DocValue.FromText(t.Value);
            case BoolLiteral b:
                return DocValue.FromBool(b.Value);
            case BlankLiteral:
                return DocValue.Blank;
            case FieldRef f:
                return context.Field(f.Key);
            case ColumnRef c:
                return context.Column(c.Key);
            case NegateExpr neg:
                return DocValue.FromNumber(-Evaluate(neg.Operand, context).AsNumber());
            case BinaryExpr bin:
                return EvaluateBinary(bin, context);
            case CallExpr call:
                return EvaluateCall(call, context);
            default:
                throw new InvalidOperationException($"Unknown expression node {expr.GetType().Name}.");
        }
    }

    private static DocValue EvaluateBinary(BinaryExpr bin, IEvaluationContext context)
    {
        var left = Evaluate(bin.Left, context);
        var right = Evaluate(bin.Right, context);
        switch (bin.Op)
        {
            case BinaryOp.Concat:
                return DocValue.FromText(left.AsText() + right.AsText());
            case BinaryOp.Add:
                return DocValue.FromNumber(left.AsNumber() + right.AsNumber());
            case BinaryOp.Subtract:
                return DocValue.FromNumber(left.AsNumber() - right.AsNumber());
            case BinaryOp.Multiply:
                return DocValue.FromNumber(left.AsNumber() * right.AsNumber());
            case BinaryOp.Divide:
            {
                var divisor = right.AsNumber();
                return divisor == 0m ? DocValue.Blank : DocValue.FromNumber(left.AsNumber() / divisor);
            }
        }

        var a = left.AsNumber();
        var b = right.AsNumber();
        var result = bin.Op switch
        {
            BinaryOp.Equal => a == b,
            BinaryOp.NotEqual => a != b,
            BinaryOp.Less => a < b,
            BinaryOp.LessOrEqual => a <= b,
            BinaryOp.Greater => a > b,
            BinaryOp.GreaterOrEqual => a >= b,
            _ => throw new InvalidOperationException($"Unknown operator {bin.Op}.")
        };
        return DocValue.FromBool(result);
    }

    private static DocValue EvaluateCall(CallExpr call, IEvaluationContext context)
    {
        var args = call.Args;
        switch (call.Name)
        {
            case ExpressionFunctions.Round:
                return DocValue.FromNumber(Round(Evaluate(args[0], context).AsNumber(), Evaluate(args[1], context).AsNumber()));
            case ExpressionFunctions.If:
                return Evaluate(args[0], context).AsBool() ? Evaluate(args[1], context) : Evaluate(args[2], context);
            case ExpressionFunctions.And:
                return DocValue.FromBool(args.All(a => Evaluate(a, context).AsBool()));
            case ExpressionFunctions.Or:
                return DocValue.FromBool(args.Any(a => Evaluate(a, context).AsBool()));
            case ExpressionFunctions.Not:
                return DocValue.FromBool(!Evaluate(args[0], context).AsBool());
            case ExpressionFunctions.Coalesce:
            {
                // اکسل: IF(a="", b, a) — خالی یا متن خالی
                var first = Evaluate(args[0], context);
                return first.IsBlank || first.Kind == DocValueKind.Text && first.Text!.Length == 0 ? Evaluate(args[1], context) : first;
            }
            case ExpressionFunctions.Abs:
                return DocValue.FromNumber(Math.Abs(Evaluate(args[0], context).AsNumber()));
            case ExpressionFunctions.Min:
                return DocValue.FromNumber(args.Select(a => Evaluate(a, context).AsNumber()).Min());
            case ExpressionFunctions.Max:
                return DocValue.FromNumber(args.Select(a => Evaluate(a, context).AsNumber()).Max());
            case ExpressionFunctions.Jdot:
            {
                var value = Evaluate(args[0], context);
                return value.Kind == DocValueKind.Date ? DocValue.FromText(JalaliDot(value.Date)) : DocValue.Blank;
            }
            case ExpressionFunctions.Sum:
            case ExpressionFunctions.SumProduct:
            case ExpressionFunctions.Count:
                return context.Aggregate(call.Name, args.OfType<ColumnRef>().Select(c => c.Key).ToList());
            default:
                throw new InvalidOperationException($"Unknown function {call.Name}.");
        }
    }

    /// <summary>ROUND اکسل با ارقام منفی (گرد به ده و صد) هم؛ نصفه دور از صفر.</summary>
    public static decimal Round(decimal value, decimal digits)
    {
        var n = (int)Math.Truncate(digits);
        if (n >= 0)
            return Num.Round(value, Math.Min(n, 28));

        var factor = 1m;
        for (var i = 0; i < Math.Min(-n, 28); i++)
            factor *= 10m;
        return Num.Round(value / factor, 0) * factor;
    }

    /// <summary>
    /// تاریخ شمسی نقطه‌دار با ارقام لاتین («1405.06.15»)، همان که بازرگان در عنوان اظهار می‌نویسد (L.1: 2026-09-06 →
    /// «1405.06.15»). ارقام لاتین عمدی است: همین متن در خانهٔ اکسل و نام فایل می‌رود.
    /// </summary>
    public static string JalaliDot(DateOnly date)
    {
        var calendar = new PersianCalendar();
        var day = date.ToDateTime(TimeOnly.MinValue);
        return string.Create(CultureInfo.InvariantCulture,
            $"{calendar.GetYear(day):0000}.{calendar.GetMonth(day):00}.{calendar.GetDayOfMonth(day):00}");
    }
}
