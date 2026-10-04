namespace RugsManagment.Application.Common;

/// <summary>
/// عدد به حروف فارسی — «ده میلیون تومان» زیر هر خانهٔ مبلغ و روی چاپ.
///
/// چرا: بازرگان صفرهای یک مبلغ را با چشم نمی‌شمارد و یک صفر اضافه یعنی ده برابر قیمت. خواندن مبلغ به
/// حروف این خطا را همان لحظه نشان می‌دهد. نسخهٔ فرانت <c>CA/lib/numWords.ts</c> باید دقیقاً همین متن را
/// بسازد؛ قاعده‌ها:
/// <list type="bullet">
/// <item>۰ → «صفر»؛ منفی → «منفی » + بقیه.</item>
/// <item>گروه‌های سه‌رقمی با «هزار، میلیون، میلیارد، تریلیون، کوادریلیون» و میان گروه‌ها « و ».</item>
/// <item>۱۰۰۰ تنها «هزار» است (نه «یک هزار»)، ولی «یک میلیون» و «یک میلیارد».</item>
/// <item>درون گروه: صدگان « و » دهگان « و » یکان (۱۲۵ → «صد و بیست و پنج»؛ ۱۱ تا ۱۹ یک واژه).</item>
/// <item>اعشار تا سه رقم (بیشتر، دور از صفر گرد می‌شود؛ صفرهای انتهایی حذف): «دو و پنج دهم»،
/// «بیست و پنج صدم»، «سه و صد و بیست و پنج هزارم».</item>
/// <item>بزرگ‌تر از ۹۹۹ کوادریلیون: همان عدد با ارقام فارسی (بدون حروف).</item>
/// </list>
/// </summary>
public static class NumberWords
{
    private const string Zero = "صفر";
    private const string Negative = "منفی";
    private const string And = " و ";
    private const long MaxSupported = 999_999_999_999_999_999;

    private static readonly string[] Ones =
    [
        "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه",
        "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده"
    ];

    private static readonly string[] Tens =
        ["", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"];

    private static readonly string[] Hundreds =
        ["", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد"];

    private static readonly string[] Scales =
        ["", "هزار", "میلیون", "میلیارد", "تریلیون", "کوادریلیون"];

    private static readonly string[] FractionUnits = ["دهم", "صدم", "هزارم"];

    /// <summary>عدد به حروف: ۱۲۵۰۰۰۰ → «یک میلیون و دویست و پنجاه هزار»؛ ۲٫۵ → «دو و پنج دهم».</summary>
    public static string ToWords(decimal value)
    {
        var rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        if (rounded == 0m) return Zero;

        var abs = Math.Abs(rounded);
        var integer = decimal.Truncate(abs);
        if (integer > MaxSupported) return Messages.FormatNumber(rounded);

        var words = new List<string>(2);
        if (integer > 0m) words.Add(IntegerWords((long)integer));

        var fraction = FractionWords(abs - integer);
        if (fraction is not null) words.Add(fraction);

        var text = string.Join(And, words);
        return rounded < 0m ? $"{Negative} {text}" : text;
    }

    /// <summary>مبلغ به حروف همراه واحد: (۱۰٬۰۰۰٬۰۰۰، «تومان») → «ده میلیون تومان».</summary>
    public static string Money(decimal amount, string currencyLabel)
        => string.IsNullOrWhiteSpace(currencyLabel) ? ToWords(amount) : $"{ToWords(amount)} {currencyLabel}";

    private static string IntegerWords(long value)
    {
        var groups = new List<string>();
        var scale = 0;

        while (value > 0)
        {
            var group = (int)(value % 1000);
            value /= 1000;

            if (group != 0)
            {
                // «هزار» تنها، نه «یک هزار»؛ ولی «یک میلیون»
                var text = group == 1 && scale == 1
                    ? Scales[1]
                    : scale == 0 ? GroupWords(group) : $"{GroupWords(group)} {Scales[scale]}";
                groups.Add(text);
            }

            scale++;
        }

        groups.Reverse();
        return string.Join(And, groups);
    }

    private static string GroupWords(int value)
    {
        var parts = new List<string>(3);

        var hundreds = value / 100;
        if (hundreds > 0) parts.Add(Hundreds[hundreds]);

        var rest = value % 100;
        if (rest is > 0 and < 20)
        {
            parts.Add(Ones[rest]);
        }
        else if (rest >= 20)
        {
            parts.Add(Tens[rest / 10]);
            if (rest % 10 > 0) parts.Add(Ones[rest % 10]);
        }

        return string.Join(And, parts);
    }

    private static string? FractionWords(decimal fraction)
    {
        if (fraction == 0m) return null;

        // fraction < 1 با حداکثر سه رقم؛ صفرهای انتهایی را حذف می‌کنیم تا ۰٫۵۰ «پنج دهم» شود
        var digits = 3;
        var scaled = (long)(fraction * 1000m);
        while (digits > 1 && scaled % 10 == 0)
        {
            scaled /= 10;
            digits--;
        }

        return $"{IntegerWords(scaled)} {FractionUnits[digits - 1]}";
    }
}
