namespace RugsManagment.Application.Features.Documents.Expressions;

/// <summary>
/// آنچه نوع‌سنج از جای عبارت می‌داند: کدام فیلدها مجازند (و نوعشان)، کدام ستون‌ها (و نوعشان)، و آیا جمع پانویس
/// مجاز است.
/// </summary>
public interface ITypeScope
{
    /// <summary>نوع یک فیلد کاتالوگ؛ null یعنی فیلد ناشناخته است.</summary>
    ExprType? FieldType(string key);

    /// <summary>فیلد در این جا مجاز است؟ (عنوان: فقط محموله/کارگاه/واژه؛ پانویس: بدون فرش و خط.)</summary>
    bool FieldAllowed(string key);

    /// <summary>نوع یک ستون؛ null یعنی ستون ناشناخته است.</summary>
    ExprType? ColumnType(string key);

    /// <summary><c>col.*</c> بیرون از جمع مجاز است (فقط در عبارت ستون).</summary>
    bool ColumnsAllowed { get; }

    /// <summary>جمع‌های پانویس (<c>sum</c>، <c>sumproduct</c>، <c>count</c>) مجازند (فقط در پانویس).</summary>
    bool AggregatesAllowed { get; }
}

/// <summary>نتیجهٔ نوع‌سنجی: نوع نهایی و خطاها.</summary>
public sealed record TypeCheckResult(ExprType Type, IReadOnlyList<ExpressionError> Errors)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// نوع‌سنج زبان عبارت (spec E.4). قاعده‌ها:
/// <list type="bullet">
/// <item>حساب (<c>+ - * /</c> و منفی) و مقایسه فقط روی عدد؛ مقایسهٔ متن رد می‌شود (اکسل و موتور ما متن را یکسان
/// مرتب نمی‌کنند، پس نتیجهٔ پیش‌نمایش با اکسل فرق می‌کرد).</item>
/// <item><c>&amp;</c> عدد و متن می‌پذیرد و متن می‌دهد؛ تاریخ باید اول با <c>jdot</c> متن شود.</item>
/// <item><c>if</c> شرط بله/نه و دو شاخهٔ هم‌نوع؛ <c>coalesce</c> دو ورودی هم‌نوع.</item>
/// <item>جمع‌ها فقط در پانویس و ورودی‌شان فقط ستونِ عددی.</item>
/// </list>
/// </summary>
public static class TypeChecker
{
    public static TypeCheckResult Check(Expr expr, ITypeScope scope)
    {
        var errors = new List<ExpressionError>();
        var type = Infer(expr, scope, errors, insideAggregate: false);
        return new TypeCheckResult(errors.Count == 0 ? type : ExprType.Error, errors);
    }

    private static ExprType Infer(Expr expr, ITypeScope scope, List<ExpressionError> errors, bool insideAggregate)
    {
        switch (expr)
        {
            case NumberLiteral:
                return ExprType.Number;
            case TextLiteral:
                return ExprType.Text;
            case BoolLiteral:
                return ExprType.Bool;
            case BlankLiteral:
                return ExprType.Number;

            case FieldRef f:
            {
                var type = scope.FieldType(f.Key);
                if (type is null)
                {
                    errors.Add(new ExpressionError(ExpressionCodes.UnknownField, f.Position, f.Key));
                    return ExprType.Error;
                }

                if (!scope.FieldAllowed(f.Key))
                {
                    errors.Add(new ExpressionError(ExpressionCodes.FieldScope, f.Position, f.Key));
                    return ExprType.Error;
                }

                return type.Value;
            }

            case ColumnRef c:
            {
                if (!scope.ColumnsAllowed && !insideAggregate)
                {
                    errors.Add(new ExpressionError(ExpressionCodes.ColumnNotAllowed, c.Position, c.Key));
                    return ExprType.Error;
                }

                var type = scope.ColumnType(c.Key);
                if (type is null)
                {
                    errors.Add(new ExpressionError(ExpressionCodes.UnknownColumn, c.Position, c.Key));
                    return ExprType.Error;
                }

                return type.Value;
            }

            case NegateExpr neg:
                RequireNumber(Infer(neg.Operand, scope, errors, insideAggregate), neg.Operand.Position, errors);
                return ExprType.Number;

            case BinaryExpr bin:
                return InferBinary(bin, scope, errors, insideAggregate);

            case CallExpr call:
                return InferCall(call, scope, errors, insideAggregate);

            default:
                errors.Add(new ExpressionError(ExpressionCodes.Syntax, expr.Position));
                return ExprType.Error;
        }
    }

    private static ExprType InferBinary(BinaryExpr bin, ITypeScope scope, List<ExpressionError> errors, bool insideAggregate)
    {
        var left = Infer(bin.Left, scope, errors, insideAggregate);
        var right = Infer(bin.Right, scope, errors, insideAggregate);
        switch (bin.Op)
        {
            case BinaryOp.Concat:
                RequireConcat(left, bin.Left.Position, errors);
                RequireConcat(right, bin.Right.Position, errors);
                return ExprType.Text;

            case BinaryOp.Add:
            case BinaryOp.Subtract:
            case BinaryOp.Multiply:
            case BinaryOp.Divide:
                RequireNumber(left, bin.Left.Position, errors);
                RequireNumber(right, bin.Right.Position, errors);
                return ExprType.Number;

            default:
                if (left == ExprType.Text || right == ExprType.Text)
                {
                    errors.Add(new ExpressionError(ExpressionCodes.TextCompare, bin.Position));
                    return ExprType.Error;
                }

                RequireNumber(left, bin.Left.Position, errors);
                RequireNumber(right, bin.Right.Position, errors);
                return ExprType.Bool;
        }
    }

    private static ExprType InferCall(CallExpr call, ITypeScope scope, List<ExpressionError> errors, bool insideAggregate)
    {
        var args = call.Args;
        if (ExpressionFunctions.IsAggregate(call.Name))
        {
            if (!scope.AggregatesAllowed || insideAggregate)
            {
                errors.Add(new ExpressionError(ExpressionCodes.AggregateContext, call.Position, call.Name));
                return ExprType.Error;
            }

            foreach (var arg in args)
            {
                if (arg is not ColumnRef column)
                {
                    errors.Add(new ExpressionError(ExpressionCodes.AggregateArgument, arg.Position, call.Name));
                    continue;
                }

                RequireNumber(Infer(column, scope, errors, insideAggregate: true), column.Position, errors);
            }

            return ExprType.Number;
        }

        var types = args.Select(a => Infer(a, scope, errors, insideAggregate)).ToList();
        switch (call.Name)
        {
            case ExpressionFunctions.Round:
                RequireNumber(types[0], args[0].Position, errors);
                RequireNumber(types[1], args[1].Position, errors);
                return ExprType.Number;

            case ExpressionFunctions.Abs:
            case ExpressionFunctions.Min:
            case ExpressionFunctions.Max:
                for (var i = 0; i < types.Count; i++)
                    RequireNumber(types[i], args[i].Position, errors);
                return ExprType.Number;

            case ExpressionFunctions.And:
            case ExpressionFunctions.Or:
            case ExpressionFunctions.Not:
                for (var i = 0; i < types.Count; i++)
                    RequireBool(types[i], args[i].Position, errors);
                return ExprType.Bool;

            case ExpressionFunctions.If:
                RequireBool(types[0], args[0].Position, errors);
                return Same(types[1], types[2], call.Position, errors);

            case ExpressionFunctions.Coalesce:
                return Same(types[0], types[1], call.Position, errors);

            case ExpressionFunctions.Jdot:
                if (types[0] != ExprType.Date && types[0] != ExprType.Error)
                    errors.Add(new ExpressionError(ExpressionCodes.DateExpected, args[0].Position));
                return ExprType.Text;

            default:
                errors.Add(new ExpressionError(ExpressionCodes.UnknownFunction, call.Position, call.Name));
                return ExprType.Error;
        }
    }

    private static ExprType Same(ExprType a, ExprType b, int position, List<ExpressionError> errors)
    {
        if (a == ExprType.Error || b == ExprType.Error)
            return ExprType.Error;
        if (a != b)
        {
            errors.Add(new ExpressionError(ExpressionCodes.Mixed, position));
            return ExprType.Error;
        }

        return a;
    }

    private static void RequireNumber(ExprType type, int position, List<ExpressionError> errors)
    {
        if (type != ExprType.Number && type != ExprType.Error)
            errors.Add(new ExpressionError(ExpressionCodes.NumberExpected, position));
    }

    private static void RequireBool(ExprType type, int position, List<ExpressionError> errors)
    {
        if (type != ExprType.Bool && type != ExprType.Error)
            errors.Add(new ExpressionError(ExpressionCodes.BoolExpected, position));
    }

    private static void RequireConcat(ExprType type, int position, List<ExpressionError> errors)
    {
        if (type is ExprType.Date or ExprType.Bool)
            errors.Add(new ExpressionError(ExpressionCodes.ConcatType, position));
    }
}
