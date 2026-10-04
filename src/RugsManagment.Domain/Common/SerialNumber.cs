using System.Globalization;
using System.Text;

namespace RugsManagment.Domain.Common;

/// <summary>
/// «شماره»ی خودِ بازرگان روی فرش: یک عدد («3778»)، یک دسته («3911 - 3960») یا متن آزاد («A-12»).
///
/// چرا نوع جدا: یک شماره در سه ستون می‌نشیند (<c>SerialNo</c> برای نمایش و ایندکس یکتا، <c>SerialFrom</c>/<c>SerialTo</c>
/// برای همپوشانی، جست‌وجوی «شماره داخل دسته» و «شمارهٔ بعدی») و این سه باید همیشه با هم بخوانند. پس هر شماره‌ای
/// فقط از این‌جا ساخته می‌شود؛ خودِ شکل‌های تایپ‌شده (spec G.4):
/// «3778»، «۳۷۷۸»، «3911 - 3960»، «3911–3960»، «3911_3960»، «3911 تا 3960». شکل نمایشیِ دسته همیشه «3911 - 3960»
/// است و ارقام همیشه لاتین ذخیره می‌شوند (B.1) تا «۳۷۷۸» و «3778» یکی باشند.
///
/// قاعده‌ها:
/// <list type="bullet">
/// <item>«3960-3911» (برعکس) خطاست؛ دسته‌ای با بیش از <see cref="MaxRangeLength"/> شماره خطاست.</item>
/// <item>«3911 - 3911» همان شمارهٔ تکِ 3911 است.</item>
/// <item>هر چیزی که فقط رقم و خط تیره است ولی شکل درست ندارد («3911-»، «3911 3960») خطای قالب است، نه متن آزاد —
/// تقریباً همیشه اشتباه تایپ است.</item>
/// <item>متنی که حرف یا نشانهٔ دیگری دارد («A-12»، «12/5») شمارهٔ متنی است: یکتا می‌ماند ولی بازه و «شمارهٔ بعدی» ندارد.</item>
/// <item>ارقام تایپ‌شده همان‌طور می‌مانند («0123» با صفرش نمایش داده می‌شود) ولی مقدار عددی‌اش ۱۲۳ است؛ عددی بزرگ‌تر از
/// int (بیش از ۱۰ رقم) فقط به‌صورت شمارهٔ متنی پذیرفته می‌شود.</item>
/// <item>بازه می‌تواند از تعداد بلندتر باشد، هرگز کوتاه‌تر (<see cref="EnsureCovers"/>؛ CHECK دیتابیس هم همین است).</item>
/// </list>
/// </summary>
public sealed record SerialNumber
{
    /// <summary>طول ستون <c>SerialNo</c> (varchar(40)).</summary>
    public const int MaxLength = 40;

    /// <summary>بیشترین تعداد شماره در یک دسته.</summary>
    public const int MaxRangeLength = 10_000;

    /// <summary>کلید فیلد در خطای ۴۰۰/۴۰۹ (همان که جزیره زیر خانهٔ شماره نشان می‌دهد).</summary>
    public const string Field = "rug.serial";

    // کدهای پیام — همین کدها با همین متن‌ها در Messages.fa.Rugs و lib/messages/rugs.ts می‌آیند
    public const string FormatErrorCode = "E_SERIAL_FORMAT";
    public const string ReversedErrorCode = "E_SERIAL_REVERSED";
    public const string SpanErrorCode = "E_SERIAL_SPAN";
    public const string TooLongErrorCode = "E_SERIAL_TOO_LONG";
    public const string QuantityErrorCode = "E_SERIAL_QTY";

    /// <summary>متن خطای قالب.</summary>
    public const string FormatErrorMessage = "شماره را مثل ۳۷۷۸ و دسته را مثل ۳۹۱۱ - ۳۹۶۰ بنویسید.";

    /// <summary>متن خطای دستهٔ برعکس.</summary>
    public const string ReversedErrorMessage = "در دسته، شمارهٔ اول باید کوچک‌تر باشد؛ مثل ۳۹۱۱ - ۳۹۶۰ بنویسید.";

    /// <summary>متن خطای دستهٔ بیش از اندازه بلند.</summary>
    public const string SpanErrorMessage = "یک دسته حداکثر ۱۰٬۰۰۰ شماره دارد.";

    /// <summary>متن خطای شمارهٔ بیش از ۴۰ حرف.</summary>
    public const string TooLongErrorMessage = "شماره حداکثر ۴۰ حرف است.";

    /// <summary>
    /// الگوی خطای «بازه کوتاه‌تر از تعداد»؛ نشانه‌ها: <c>{serial}</c>، <c>{count}</c>، <c>{qty}</c>
    /// (همان الگو در Messages.fa.Rugs؛ این‌جا با <see cref="QuantityError"/> پر می‌شود).
    /// </summary>
    public const string QuantityErrorTemplate =
        "شمارهٔ «{serial}» فقط {count} شماره دارد ولی تعداد {qty} تخته است؛ بازه نباید از تعداد کوتاه‌تر باشد (مثل ۳۹۱۱ - ۳۹۶۰).";

    private const string RangeSeparator = " - ";

    private SerialNumber(string display, int? from, int? to)
    {
        Display = display;
        From = from;
        To = to;
    }

    /// <summary>شکل نمایشی و ذخیره‌شده (<c>SerialNo</c>): «3778»، «3911 - 3960» یا «A-12».</summary>
    public string Display { get; }

    /// <summary>اولین شماره؛ null برای شمارهٔ متنی.</summary>
    public int? From { get; }

    /// <summary>آخرین شماره (برای شمارهٔ تک برابر <see cref="From"/>)؛ null برای شمارهٔ متنی.</summary>
    public int? To { get; }

    /// <summary>عددی است (تک یا دسته)، پس بازه، همپوشانی و «شمارهٔ بعدی» دارد.</summary>
    public bool IsNumeric => From is not null;

    /// <summary>دسته است (بیش از یک شماره).</summary>
    public bool IsRange => From is { } from && To is { } to && to > from;

    /// <summary>تعداد شماره‌هایی که می‌پوشاند (شمارهٔ متنی: ۱).</summary>
    public int Count => From is { } from && To is { } to ? to - from + 1 : 1;

    /// <summary>کلید مرتب‌سازی (<c>SerialSort</c>) = اولین شماره؛ null برای شمارهٔ متنی.</summary>
    public long? SortKey => From;

    /// <summary>شمارهٔ بعد از آخرین شمارهٔ این یکی (قاعدهٔ «شمارهٔ بعدی»، G.4)؛ null برای شمارهٔ متنی.</summary>
    public int? NextAfter => To is { } to && to < int.MaxValue ? to + 1 : null;

    // ═════════════════════════════════════════════════════════════════════
    // ساختن
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>شمارهٔ تک عددی.</summary>
    public static SerialNumber Single(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        return new SerialNumber(number.ToString(CultureInfo.InvariantCulture), number, number);
    }

    /// <summary>
    /// دستهٔ <paramref name="from"/> تا <paramref name="to"/> (هر دو شامل). برابر بودن دو سر یعنی شمارهٔ تک.
    /// برعکس یا بیش از <see cref="MaxRangeLength"/> شماره → <see cref="DomainValidationException"/>.
    /// </summary>
    public static SerialNumber Range(int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        var error = CheckRange(from, to);
        if (error is not null)
            throw error.ToException();

        return from == to
            ? Single(from)
            : new SerialNumber(
                from.ToString(CultureInfo.InvariantCulture) + RangeSeparator + to.ToString(CultureInfo.InvariantCulture),
                from, to);
    }

    /// <summary>
    /// شماره‌های یک ردیف دسته وقتی فقط اولین شماره و تعداد معلوم است (G.4: «ردیفی که فقط تعداد بیش از یک گرفته،
    /// بازهٔ From..From + qty − 1 می‌گیرد»).
    /// </summary>
    public static SerialNumber ForLot(int from, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        return Range(from, checked(from + quantity - 1));
    }

    /// <summary>
    /// بازسازی از سه ستون ذخیره‌شده، بی اعتبارسنجی دوباره (دادهٔ دیتابیس را CHECKها نگه داشته‌اند).
    /// <c>SerialNo</c> خالی یعنی «بی‌شماره».
    /// </summary>
    public static SerialNumber? FromStored(string? serialNo, int? serialFrom, int? serialTo)
        => string.IsNullOrEmpty(serialNo)
            ? null
            : new SerialNumber(serialNo, serialFrom, serialFrom is null ? null : serialTo ?? serialFrom);

    // ═════════════════════════════════════════════════════════════════════
    // خواندن متن تایپ‌شده
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// متن تایپ‌شده را می‌خواند. متن خالی یعنی «بی‌شماره» (null). شکل نادرست →
    /// <see cref="DomainValidationException"/> روی فیلد <see cref="Field"/>.
    /// </summary>
    public static SerialNumber? Parse(string? text)
        => TryParse(text, out var serial, out var error)
            ? serial
            : throw error!.ToException();

    /// <summary>
    /// همان <see cref="Parse"/> بدون استثنا. متن خالی: <c>true</c> با <paramref name="serial"/> = null.
    /// </summary>
    public static bool TryParse(string? text, out SerialNumber? serial, out SerialError? error)
    {
        serial = null;
        error = null;

        var normalized = Normalize(text);
        if (normalized.Length == 0)
            return true;

        if (TryReadNumeric(normalized, out var fromDigits, out var toDigits))
        {
            // تک: «3778» (یا عددی بزرگ‌تر از int که فقط متنی پذیرفته می‌شود)
            if (toDigits is null)
            {
                if (!TryParseInt(fromDigits, out var single))
                    return TryText(normalized, out serial, out error);

                serial = new SerialNumber(fromDigits, single, single);
                return true;
            }

            if (!TryParseInt(fromDigits, out var from) || !TryParseInt(toDigits, out var to))
            {
                error = SerialError.Format;
                return false;
            }

            error = CheckRange(from, to);
            if (error is not null)
                return false;

            serial = from == to
                ? new SerialNumber(fromDigits, from, from)
                : new SerialNumber(fromDigits + RangeSeparator + toDigits, from, to);
            return true;
        }

        if (LooksNumeric(normalized))
        {
            // فقط رقم و جداکننده ولی بی‌شکل: «3911-»، «-12»، «3911 3960»، «1-2-3»
            error = SerialError.Format;
            return false;
        }

        return TryText(normalized, out serial, out error);
    }

    // ═════════════════════════════════════════════════════════════════════
    // قاعده‌ها
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>آیا این شماره برای این تعداد تخته کافی است (بازه کوتاه‌تر از تعداد نباشد).</summary>
    public bool Covers(int quantity) => !IsNumeric || Count >= quantity;

    /// <summary>
    /// اگر بازه از تعداد کوتاه‌تر است → <see cref="DomainValidationException"/> (۴۰۰) روی فیلد <see cref="Field"/>.
    /// شمارهٔ متنی هر تعدادی را می‌پذیرد (بازه ندارد).
    /// </summary>
    public void EnsureCovers(int quantity)
    {
        if (!Covers(quantity))
            throw QuantityError(quantity).ToException();
    }

    /// <summary>خطای «بازه کوتاه‌تر از تعداد» با نشانه‌های پرشده.</summary>
    public SerialError QuantityError(int quantity)
        => new(QuantityErrorCode, QuantityErrorTemplate
            .Replace("{serial}", DomainText.PersianDigits(Display), StringComparison.Ordinal)
            .Replace("{count}", DomainText.Number(Count), StringComparison.Ordinal)
            .Replace("{qty}", DomainText.Number(quantity), StringComparison.Ordinal));

    /// <summary>آیا این عدد داخل این شماره (تک یا دسته) است.</summary>
    public bool Contains(int number) => From is { } from && To is { } to && number >= from && number <= to;

    /// <summary>
    /// همپوشانی دو شماره: دو شمارهٔ عددی وقتی بازه‌هایشان اشتراک دارند؛ شمارهٔ متنی فقط با همان متن.
    /// </summary>
    public bool Overlaps(SerialNumber other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (From is { } a1 && To is { } a2 && other.From is { } b1 && other.To is { } b2)
            return a1 <= b2 && b1 <= a2;

        return string.Equals(Display, other.Display, StringComparison.Ordinal);
    }

    /// <summary>
    /// برش دسته (D.9، وقتی طول بازه = تعداد): <paramref name="count"/> شمارهٔ آخر به فرش تازه می‌رسد.
    /// «3911 - 3960» با ۲۰ → «3911 - 3940» و «3941 - 3960». شکل نمایشی از عددها ساخته می‌شود (صفرهای پیشوند نمی‌مانند).
    /// </summary>
    public (SerialNumber Kept, SerialNumber Taken) SplitLast(int count)
    {
        if (From is not { } from || To is not { } to || to == from)
            throw new InvalidOperationException("فقط دستهٔ عددی را می‌شود برش داد.");
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(count, Count);

        return (Range(from, to - count), Range(to - count + 1, to));
    }

    public override string ToString() => Display;

    // ═════════════════════════════════════════════════════════════════════
    // جزئیات
    // ═════════════════════════════════════════════════════════════════════

    private static SerialError? CheckRange(int from, int to)
    {
        if (from > to)
            return SerialError.Reversed;
        if ((long)to - from + 1 > MaxRangeLength)
            return SerialError.Span;
        return null;
    }

    private static bool TryParseInt(string digits, out int value)
        => int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool TryText(string normalized, out SerialNumber? serial, out SerialError? error)
    {
        if (normalized.Length > MaxLength)
        {
            serial = null;
            error = SerialError.TooLong;
            return false;
        }

        serial = new SerialNumber(normalized, null, null);
        error = null;
        return true;
    }

    /// <summary>
    /// «3778» یا «3911 - 3960» با هر جداکنندهٔ مجاز (خط تیره‌ها، «_»، «تا»)، با یا بی‌فاصله.
    /// </summary>
    private static bool TryReadNumeric(string text, out string fromDigits, out string? toDigits)
    {
        fromDigits = string.Empty;
        toDigits = null;

        var i = 0;
        var first = ReadDigits(text, ref i);
        if (first.Length == 0)
            return false;
        if (i == text.Length)
        {
            fromDigits = first;
            return true;
        }

        SkipSpaces(text, ref i);
        if (!TryReadSeparator(text, ref i))
            return false;
        SkipSpaces(text, ref i);

        var second = ReadDigits(text, ref i);
        if (second.Length == 0 || i != text.Length)
            return false;

        fromDigits = first;
        toDigits = second;
        return true;
    }

    private static string ReadDigits(string text, ref int i)
    {
        var start = i;
        while (i < text.Length && text[i] is >= '0' and <= '9') i++;
        return text[start..i];
    }

    private static void SkipSpaces(string text, ref int i)
    {
        while (i < text.Length && text[i] == ' ') i++;
    }

    private static bool TryReadSeparator(string text, ref int i)
    {
        if (i < text.Length && IsDash(text[i]))
        {
            i++;
            return true;
        }

        if (string.CompareOrdinal(text, i, Ta, 0, Ta.Length) == 0)
        {
            i += Ta.Length;
            return true;
        }

        return false;
    }

    /// <summary>«تا» در «3911 تا 3960».</summary>
    private const string Ta = "تا";

    /// <summary>خط تیرهٔ معمولی، en/em dash، منهای ریاضی، hyphenها و «_».</summary>
    private static bool IsDash(char c) => c is '-' or '–' or '—' or '−' or '‐' or '‑' or '‒' or '_';

    /// <summary>
    /// فقط رقم، جداکننده و فاصله دارد (بی هیچ حرف دیگری) — پس کاربر عدد یا دسته می‌خواست، نه متن آزاد.
    /// </summary>
    private static bool LooksNumeric(string text)
    {
        var rest = text.Replace(Ta, string.Empty, StringComparison.Ordinal);
        var hasDigit = false;
        foreach (var c in rest)
        {
            if (c is >= '0' and <= '9') hasDigit = true;
            else if (c != ' ' && !IsDash(c)) return false;
        }

        return hasDigit;
    }

    /// <summary>
    /// ارقام فارسی/عربی ← لاتین، «ي/ك/ة/ۀ» ← فارسی (مثل PersianText.Normalize)، نشانه‌های جهت متن و BOM حذف،
    /// هر فاصله‌ای (نیم‌فاصله، فاصلهٔ نشکن) ← یک فاصلهٔ ساده، و trim.
    /// </summary>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true; // فاصلهٔ ابتدایی حذف می‌شود
        foreach (var raw in text)
        {
            if (raw is '‎' or '‏' or '؜' or '﻿' or '‍' or (>= '‪' and <= '‮')
                or (>= '⁦' and <= '⁩'))
                continue;

            var c = DomainText.LatinDigit(raw) switch
            {
                'ي' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                'ۀ' => 'ه',
                '‌' => ' ', // نیم‌فاصله
                var other => other
            };

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
                continue;
            }

            if (char.IsControl(c))
                continue;

            builder.Append(c);
            lastWasSpace = false;
        }

        if (builder.Length > 0 && builder[^1] == ' ')
            builder.Length--;

        return builder.ToString();
    }
}

/// <summary>
/// خطای خواندن یا سنجیدن یک شماره: کد ثابت (همان کد Messages.fa.Rugs) و متن فارسیِ آمادهٔ نمایش.
/// </summary>
public sealed record SerialError(string Code, string Message)
{
    public static readonly SerialError Format = new(SerialNumber.FormatErrorCode, SerialNumber.FormatErrorMessage);
    public static readonly SerialError Reversed = new(SerialNumber.ReversedErrorCode, SerialNumber.ReversedErrorMessage);
    public static readonly SerialError Span = new(SerialNumber.SpanErrorCode, SerialNumber.SpanErrorMessage);
    public static readonly SerialError TooLong = new(SerialNumber.TooLongErrorCode, SerialNumber.TooLongErrorMessage);

    /// <summary>همین خطا به‌صورت ۴۰۰ روی فیلد شماره.</summary>
    public DomainValidationException ToException() => new(Message, SerialNumber.Field);
}
