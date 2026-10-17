namespace RugsManagment.Application.Features.Documents.Expressions;

/// <summary>مرزهای زبان عبارت (spec E.4): ۱۰۰۰ نویسه و ژرفای ۳۲.</summary>
public static class ExpressionLimits
{
    public const int MaxLength = 1000;
    public const int MaxDepth = 32;
}

/// <summary>نتیجهٔ خواندن یک عبارت: درخت یا اولین خطا.</summary>
public sealed record ParseResult(Expr? Expression, ExpressionError? Error)
{
    public bool Ok => Expression is not null && Error is null;
}

/// <summary>
/// خوانندهٔ عبارت (نزولی بازگشتی) با همان اولویت اکسل، از پایین به بالا:
/// مقایسه (<c>= &lt;&gt; &lt; &lt;= &gt; &gt;=</c>) ← <c>&amp;</c> ← <c>+ -</c> ← <c>* /</c> ← منفی یک‌تایی ← اتم.
/// چرا همان اولویت اکسل: عبارت عیناً به فرمول اکسل ترجمه می‌شود (<see cref="ExcelCompiler"/>)؛ اگر اولویت‌ها فرق
/// داشتند، مقدار پیش‌نمایش و مقدار اکسل از هم جدا می‌شدند.
/// </summary>
public static class ExpressionParser
{
    public static ParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ParseResult(null, new ExpressionError(ExpressionCodes.Empty, 0));
        if (text.Length > ExpressionLimits.MaxLength)
            return new ParseResult(null, new ExpressionError(ExpressionCodes.TooLong, ExpressionLimits.MaxLength));

        var (tokens, lexError) = Lexer.Tokenize(text);
        if (lexError is not null)
            return new ParseResult(null, lexError);

        var state = new State(tokens);
        try
        {
            var expr = ParseComparison(state, 1);
            if (state.Current.Kind != TokenKind.End)
                return new ParseResult(null, new ExpressionError(ExpressionCodes.Syntax, state.Current.Position));
            return new ParseResult(expr, null);
        }
        catch (ParseException e)
        {
            return new ParseResult(null, e.Error);
        }
    }

    private sealed class State(IReadOnlyList<Token> tokens)
    {
        private int _index;

        public Token Current => tokens[_index];

        public Token Next() => tokens[_index++];

        public Token Peek(int offset = 1) => tokens[Math.Min(_index + offset, tokens.Count - 1)];
    }

    private sealed class ParseException(ExpressionError error) : Exception(error.Code)
    {
        public ExpressionError Error { get; } = error;
    }

    private static void CheckDepth(int depth, int position)
    {
        if (depth > ExpressionLimits.MaxDepth)
            throw new ParseException(new ExpressionError(ExpressionCodes.TooDeep, position));
    }

    private static Expr ParseComparison(State s, int depth)
    {
        CheckDepth(depth, s.Current.Position);
        var left = ParseConcat(s, depth);
        while (true)
        {
            BinaryOp? op = s.Current.Kind switch
            {
                TokenKind.Equal => BinaryOp.Equal,
                TokenKind.NotEqual => BinaryOp.NotEqual,
                TokenKind.Less => BinaryOp.Less,
                TokenKind.LessOrEqual => BinaryOp.LessOrEqual,
                TokenKind.Greater => BinaryOp.Greater,
                TokenKind.GreaterOrEqual => BinaryOp.GreaterOrEqual,
                _ => null
            };
            if (op is null)
                return left;
            var position = s.Next().Position;
            left = new BinaryExpr(op.Value, left, ParseConcat(s, depth), position);
        }
    }

    private static Expr ParseConcat(State s, int depth)
    {
        var left = ParseAdditive(s, depth);
        while (s.Current.Kind == TokenKind.Ampersand)
        {
            var position = s.Next().Position;
            left = new BinaryExpr(BinaryOp.Concat, left, ParseAdditive(s, depth), position);
        }

        return left;
    }

    private static Expr ParseAdditive(State s, int depth)
    {
        var left = ParseTerm(s, depth);
        while (s.Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var token = s.Next();
            var op = token.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Subtract;
            left = new BinaryExpr(op, left, ParseTerm(s, depth), token.Position);
        }

        return left;
    }

    private static Expr ParseTerm(State s, int depth)
    {
        var left = ParseUnary(s, depth);
        while (s.Current.Kind is TokenKind.Star or TokenKind.Slash)
        {
            var token = s.Next();
            var op = token.Kind == TokenKind.Star ? BinaryOp.Multiply : BinaryOp.Divide;
            left = new BinaryExpr(op, left, ParseUnary(s, depth), token.Position);
        }

        return left;
    }

    private static Expr ParseUnary(State s, int depth)
    {
        if (s.Current.Kind == TokenKind.Minus)
        {
            var position = s.Next().Position;
            CheckDepth(depth + 1, position);
            return new NegateExpr(ParseUnary(s, depth + 1), position);
        }

        if (s.Current.Kind == TokenKind.Plus)
        {
            var position = s.Next().Position;
            CheckDepth(depth + 1, position);
            return ParseUnary(s, depth + 1);
        }

        return ParsePrimary(s, depth);
    }

    private static Expr ParsePrimary(State s, int depth)
    {
        var token = s.Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                s.Next();
                return new NumberLiteral(token.Number, token.Position);

            case TokenKind.Text:
                s.Next();
                return new TextLiteral(token.Text, token.Position);

            case TokenKind.LeftParen:
            {
                s.Next();
                var inner = ParseComparison(s, depth + 1);
                Expect(s, TokenKind.RightParen);
                return inner;
            }

            case TokenKind.Identifier:
            {
                s.Next();
                if (s.Current.Kind == TokenKind.LeftParen)
                    return ParseCall(s, token, depth);

                var name = token.Text;
                if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase))
                    return new BoolLiteral(true, token.Position);
                if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase))
                    return new BoolLiteral(false, token.Position);
                if (name.StartsWith("col.", StringComparison.Ordinal) && name.Length > 4)
                    return new ColumnRef(name[4..], token.Position);
                if (!name.Contains('.', StringComparison.Ordinal))
                    throw new ParseException(new ExpressionError(ExpressionCodes.UnknownField, token.Position, name));
                return new FieldRef(name, token.Position);
            }

            default:
                throw new ParseException(new ExpressionError(ExpressionCodes.Syntax, token.Position));
        }
    }

    private static Expr ParseCall(State s, Token nameToken, int depth)
    {
        var name = nameToken.Text.ToLowerInvariant();
        if (!ExpressionFunctions.Arity.TryGetValue(name, out var arity))
            throw new ParseException(new ExpressionError(ExpressionCodes.UnknownFunction, nameToken.Position, nameToken.Text));

        CheckDepth(depth + 1, nameToken.Position);
        Expect(s, TokenKind.LeftParen);
        var args = new List<Expr>();
        if (s.Current.Kind != TokenKind.RightParen)
        {
            while (true)
            {
                args.Add(ParseComparison(s, depth + 1));
                if (s.Current.Kind != TokenKind.Comma)
                    break;
                s.Next();
            }
        }

        Expect(s, TokenKind.RightParen);
        if (args.Count < arity.Min || args.Count > arity.Max)
            throw new ParseException(new ExpressionError(ExpressionCodes.Arguments, nameToken.Position, name));
        return new CallExpr(name, args, nameToken.Position);
    }

    private static void Expect(State s, TokenKind kind)
    {
        if (s.Current.Kind != kind)
            throw new ParseException(new ExpressionError(ExpressionCodes.Syntax, s.Current.Position));
        s.Next();
    }
}
