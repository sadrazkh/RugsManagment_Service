using System.Globalization;
using System.Text;

namespace RugsManagment.Application.Features.Documents.Expressions;

/// <summary>نوع یک نشانهٔ متن عبارت.</summary>
public enum TokenKind
{
    End = 0,
    Number = 1,
    Text = 2,
    Identifier = 3,
    LeftParen = 4,
    RightParen = 5,
    Comma = 6,
    Plus = 7,
    Minus = 8,
    Star = 9,
    Slash = 10,
    Ampersand = 11,
    Equal = 12,
    NotEqual = 13,
    Less = 14,
    LessOrEqual = 15,
    Greater = 16,
    GreaterOrEqual = 17
}

/// <summary>یک نشانه با جایش در متن.</summary>
public readonly record struct Token(TokenKind Kind, int Position, string Text, decimal Number = 0m);

/// <summary>
/// نشانه‌خوان زبان عبارت (spec E.4). عددها invariant‌اند («2.5»، نه «۲٫۵»)، چون همین متن در فرمول اکسل هم می‌رود و
/// اکسل با هر زبان سیستمی باید همان را بخواند. متن بین <c>"…"</c> است و <c>""</c> یعنی یک گیومه، مثل اکسل.
/// شناسه‌ها نقطه‌دارند (<c>col.length</c>، <c>rug.custom.bg_color</c>، <c>terms.bale</c>).
/// </summary>
public static class Lexer
{
    /// <summary>نشانه‌ها، یا اولین خطا (<c>E_EXPR_SYNTAX</c> با جای نویسه).</summary>
    public static (IReadOnlyList<Token> Tokens, ExpressionError? Error) Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            var start = i;
            if (IsAsciiDigit(c))
            {
                while (i < text.Length && IsAsciiDigit(text[i])) i++;
                if (i < text.Length && text[i] == '.')
                {
                    i++;
                    if (i >= text.Length || !IsAsciiDigit(text[i]))
                        return (tokens, new ExpressionError(ExpressionCodes.Syntax, i));
                    while (i < text.Length && IsAsciiDigit(text[i])) i++;
                }

                var raw = text[start..i];
                if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    return (tokens, new ExpressionError(ExpressionCodes.Syntax, start));
                tokens.Add(new Token(TokenKind.Number, start, raw, number));
                continue;
            }

            if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                var closed = false;
                while (i < text.Length)
                {
                    if (text[i] == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                            continue;
                        }

                        i++;
                        closed = true;
                        break;
                    }

                    sb.Append(text[i]);
                    i++;
                }

                if (!closed)
                    return (tokens, new ExpressionError(ExpressionCodes.Syntax, start));
                tokens.Add(new Token(TokenKind.Text, start, sb.ToString()));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                while (i < text.Length && (IsIdentifierPart(text[i]) || text[i] == '.' && i + 1 < text.Length && IsIdentifierStart(text[i + 1])))
                    i++;
                tokens.Add(new Token(TokenKind.Identifier, start, text[start..i]));
                continue;
            }

            TokenKind? kind = c switch
            {
                '(' => TokenKind.LeftParen,
                ')' => TokenKind.RightParen,
                ',' => TokenKind.Comma,
                '+' => TokenKind.Plus,
                '-' => TokenKind.Minus,
                '*' => TokenKind.Star,
                '/' => TokenKind.Slash,
                '&' => TokenKind.Ampersand,
                '=' => TokenKind.Equal,
                _ => null
            };

            if (c == '<')
            {
                if (i + 1 < text.Length && text[i + 1] == '>') { tokens.Add(new Token(TokenKind.NotEqual, start, "<>")); i += 2; continue; }
                if (i + 1 < text.Length && text[i + 1] == '=') { tokens.Add(new Token(TokenKind.LessOrEqual, start, "<=")); i += 2; continue; }
                tokens.Add(new Token(TokenKind.Less, start, "<"));
                i++;
                continue;
            }

            if (c == '>')
            {
                if (i + 1 < text.Length && text[i + 1] == '=') { tokens.Add(new Token(TokenKind.GreaterOrEqual, start, ">=")); i += 2; continue; }
                tokens.Add(new Token(TokenKind.Greater, start, ">"));
                i++;
                continue;
            }

            if (kind is null)
                return (tokens, new ExpressionError(ExpressionCodes.Syntax, start));

            tokens.Add(new Token(kind.Value, start, c.ToString()));
            i++;
        }

        tokens.Add(new Token(TokenKind.End, text.Length, ""));
        return (tokens, null);
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static bool IsIdentifierStart(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';

    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || IsAsciiDigit(c);
}

/// <summary>کدهای خطای عبارت؛ متن فارسی هر کد در <c>Messages</c> است.</summary>
public static class ExpressionCodes
{
    public const string Syntax = "E_EXPR_SYNTAX";
    public const string TooLong = "E_EXPR_TOO_LONG";
    public const string TooDeep = "E_EXPR_TOO_DEEP";
    public const string Empty = "E_EXPR_EMPTY";
    public const string UnknownFunction = "E_EXPR_UNKNOWN_FUNCTION";
    public const string Arguments = "E_EXPR_ARGS";
    public const string NumberExpected = "E_EXPR_NUMBER_EXPECTED";
    public const string BoolExpected = "E_EXPR_BOOL_EXPECTED";
    public const string DateExpected = "E_EXPR_DATE_EXPECTED";
    public const string TextCompare = "E_EXPR_TEXT_COMPARE";
    public const string Mixed = "E_EXPR_MIXED";
    public const string ConcatType = "E_EXPR_CONCAT_TYPE";
    public const string AggregateContext = "E_EXPR_AGG_CONTEXT";
    public const string AggregateArgument = "E_EXPR_AGG_ARG";
    public const string ColumnNotAllowed = "E_EXPR_COLUMN_CONTEXT";
    public const string FieldScope = "E_EXPR_FIELD_SCOPE";
    public const string UnknownField = "E_LAYOUT_UNKNOWN_FIELD";
    public const string UnknownColumn = "E_LAYOUT_UNKNOWN_COLUMN";
}
