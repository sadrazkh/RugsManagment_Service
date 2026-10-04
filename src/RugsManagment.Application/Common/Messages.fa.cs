using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RugsManagment.Application.Common;

/// <summary>
/// یک کد پیام و الگوی فارسی آن. الگو ممکن است نشانه‌هایی مثل <c>{n}</c>، <c>{label}</c>، <c>{amount}</c>
/// یا <c>{baleOf}</c> داشته باشد که <c>Format</c> پر می‌کند.
/// </summary>
/// <param name="Code">کد ثابت و انگلیسی (E_ خطا، W_ هشدار، I_ اطلاع، Q_ پرسش) — همان کد در <c>CA/lib/messages</c>.</param>
/// <param name="Text">الگوی فارسی؛ بیرون از نشانه‌ها هیچ حرف لاتینی ندارد (آزمون واحد بررسی می‌کند).</param>
public sealed record MessageTemplate(string Code, string Text)
{
    /// <summary>الگو با نشانه‌های پرشده؛ عدد با ارقام فارسی و جداکنندهٔ «٬» نوشته می‌شود.</summary>
    public string Format(params (string Key, object? Value)[] tokens) => Messages.Fill(Text, tokens);

    /// <summary>همان <c>Format</c> با واژه‌های نگله/عدل کارگاه (نشانه‌های <c>{bale}</c>، <c>{baleOf}</c>، …).</summary>
    public string Format(BaleTermWords terms, params (string Key, object? Value)[] tokens)
        => Messages.Fill(Text, [.. terms.AsTokens(), .. tokens]);

    public override string ToString() => Text;
}

/// <summary>
/// همهٔ پیام‌های فارسی که سرور به کاربر نشان می‌دهد، با کد ثابت — یک منبع.
///
/// چرا کد و نه متن پراکنده: فرانت همان کدها را در <c>CA/lib/messages/*.ts</c> دارد، پس پیامِ یک خطا در
/// جزیره و در toast صفحه یکی است؛ آزمون واحد هم بررسی می‌کند هر کد پیام دارد و هیچ پیامی بیرون از
/// نشانه‌هایش حرف لاتین ندارد (بازرگان «SKU» و «null» نمی‌بیند).
///
/// این کلاس partial است: این فایل کدهای پایه و سکو را دارد و هر فاز فایل خودش را اضافه می‌کند
/// (<c>Messages.fa.Shipments.cs</c>، <c>Messages.fa.Costs.cs</c>، …) بی‌آنکه این فایل را ویرایش کند.
/// قاعدهٔ هر فایل: فقط فیلدهای <c>public static readonly MessageTemplate</c>؛ فهرست <see cref="All"/> آن‌ها را
/// خودش با reflection پیدا می‌کند، پس جدول مشترکی برای ویرایش وجود ندارد.
/// </summary>
public static partial class Messages
{
    // ── خطاهای عمومی ─────────────────────────────────────────────────────

    public static readonly MessageTemplate Forbidden =
        new("E_FORBIDDEN", "این کار برای شما مجاز نیست.");

    public static readonly MessageTemplate NotFound =
        new("E_NOT_FOUND", "پیدا نشد؛ شاید حذف شده باشد.");

    public static readonly MessageTemplate Conflict =
        new("E_CONFLICT", "این ردیف را همین الان کس دیگری هم عوض کرد. صفحه را تازه کنید و دوباره بنویسید.");

    public static readonly MessageTemplate ValidationFailed =
        new("E_VALIDATION", "چند خانه درست پر نشده؛ پیام زیر هر خانه را ببینید.");

    public static readonly MessageTemplate ServerError =
        new("E_SERVER", "کار انجام نشد؛ دوباره امتحان کنید.");

    public static readonly MessageTemplate NetworkError =
        new("E_NETWORK", "ذخیره نشد؛ اینترنت را نگاه کنید.");

    // ── دسترسی و پول ─────────────────────────────────────────────────────

    /// <summary>نوشتن مبلغ از کاربری که Business نیست (۴۰۳).</summary>
    public static readonly MessageTemplate MoneyWriteForbidden =
        new("E_MONEY_FORBIDDEN", "مبلغ‌ها را فقط بازرگان یا مدیر می‌تواند ثبت یا عوض کند.");

    public static readonly MessageTemplate RugDeleteForbidden =
        new("E_RUG_DELETE_FORBIDDEN", "این فرش را فقط بازرگان می‌تواند حذف کند.");

    /// <summary>تغییر واحد پول یا تعداد رقم اعشار بعد از ثبت اولین مبلغ (۴۰۹).</summary>
    public static readonly MessageTemplate MoneyLocked =
        new("E_MONEY_LOCKED", "واحد پول و تعداد رقم اعشار مبلغ‌ها بعد از ثبت اولین مبلغ عوض نمی‌شود.");

    // ── خانه‌های فرم ─────────────────────────────────────────────────────

    public static readonly MessageTemplate Required =
        new("E_REQUIRED", "«{label}» را بنویسید.");

    public static readonly MessageTemplate InvalidValue =
        new("E_INVALID_VALUE", "«{label}» درست نیست.");

    public static readonly MessageTemplate OutOfRange =
        new("E_OUT_OF_RANGE", "«{label}» باید از {min} تا {max} باشد.");

    public static readonly MessageTemplate TooLong =
        new("E_TOO_LONG", "«{label}» حداکثر {n} حرف است.");

    // ── کاربران و نقش‌ها ─────────────────────────────────────────────────

    public static readonly MessageTemplate InvalidRole =
        new("E_INVALID_ROLE", "این نقش برای کاربر کارگاه نیست.");

    public static readonly MessageTemplate LastAdmin =
        new("E_LAST_ADMIN", "کارگاه باید دست‌کم یک تنظیم‌کنندهٔ فعال داشته باشد.");

    public static readonly MessageTemplate SelfDemote =
        new("E_SELF_DEMOTE", "نقش خودتان را نمی‌توانید پایین بیاورید و حساب خودتان را نمی‌توانید غیرفعال کنید.");

    // ── ترجیحات کارگاه ───────────────────────────────────────────────────

    public static readonly MessageTemplate CurrencyNotAllowed =
        new("E_CURRENCY_NOT_ALLOWED", "واحد پول کارگاه فقط تومان یا ریال است.");

    public static readonly MessageTemplate SkuPrefixInvalid =
        new("E_SKU_PREFIX", "پیشوند کد باید ۱ تا ۱۲ حرف انگلیسی یا رقم باشد.");

    // ── عدد، اندازه و تاریخ ──────────────────────────────────────────────

    public static readonly MessageTemplate Dimension =
        new("E_DIMENSION", "اندازه را به متر بنویسید، مثل ۲٫۰۸ (از ۵ سانت تا ۳۰ متر)");

    /// <summary>مبلغ اعشاری وقتی تعداد رقم اعشار مبلغ‌ها صفر است؛ <c>{currency}</c> = «تومان» یا «ریال».</summary>
    public static readonly MessageTemplate MoneyNoDecimals =
        new("E_MONEY_DECIMALS", "مبلغ {currency} اعشار ندارد");

    public static readonly MessageTemplate NumberInvalid =
        new("E_NUMBER", "عدد درست نیست؛ فقط رقم و یک ممیز بنویسید.");

    public static readonly MessageTemplate NegativeNotAllowed =
        new("E_NEGATIVE", "عدد منفی این‌جا پذیرفته نیست.");

    /// <summary><c>{date}</c> همان چیزی است که کاربر نوشت، مثل «۱۴۰۵/۰۶/۳۲».</summary>
    public static readonly MessageTemplate DateImpossible =
        new("E_DATE_IMPOSSIBLE", "{date} وجود ندارد");

    public static readonly MessageTemplate DateGregorianYear =
        new("E_DATE_YEAR", "سال را شمسی بنویسید (مثل ۱۴۰۵)");

    /// <summary>«10.000» در مبلغ بدون اعشار: شاید منظور ده هزار بوده.</summary>
    public static readonly MessageTemplate MoneyThousandsQuestion =
        new("Q_MONEY_THOUSANDS", "منظورتان {amount} است؟");

    // ── وضعیت ذخیره ──────────────────────────────────────────────────────

    public static readonly MessageTemplate Saving =
        new("I_SAVING", "در حال ذخیره…");

    public static readonly MessageTemplate Saved =
        new("I_SAVED", "ذخیره شد");

    public static readonly MessageTemplate Retrying =
        new("I_RETRYING", "ذخیره نشد؛ دوباره امتحان می‌کنیم");

    public static readonly MessageTemplate Offline =
        new("I_OFFLINE", "اینترنت قطع است. نوشته‌هایتان روی همین دستگاه مانده و از بین نمی‌رود.");

    public static readonly MessageTemplate Deleted =
        new("I_DELETED", "حذف شد");

    public static readonly MessageTemplate PreferencesSaved =
        new("I_PREFERENCES_SAVED", "ترجیحات ذخیره شد.");

    // ═════════════════════════════════════════════════════════════════════
    // جست‌وجو و پر کردن
    // ═════════════════════════════════════════════════════════════════════

    private static readonly Lazy<Registry> Lookup = new(BuildRegistry);

    /// <summary>همهٔ پیام‌های همهٔ فایل‌های partial، به کد.</summary>
    public static IReadOnlyDictionary<string, MessageTemplate> All => Lookup.Value.ByCode;

    /// <summary>کدهایی که در دو فیلد تکرار شده‌اند (باید خالی باشد؛ آزمون واحد بررسی می‌کند).</summary>
    public static IReadOnlyList<string> DuplicateCodes => Lookup.Value.Duplicates;

    /// <summary>پیام یک کد، یا null اگر چنین کدی نیست.</summary>
    public static MessageTemplate? Find(string code)
        => All.TryGetValue(code, out var template) ? template : null;

    /// <summary>متن پرشدهٔ یک کد؛ کدِ ناشناخته پیام عمومی «کار انجام نشد» می‌شود، نه خودِ کد انگلیسی.</summary>
    public static string Text(string code, params (string Key, object? Value)[] tokens)
        => (Find(code) ?? ServerError).Format(tokens);

    /// <summary>
    /// نشانه‌های <c>{key}</c> الگو را پر می‌کند. عدد (int، long، decimal، double) با ارقام فارسی و جداکنندهٔ
    /// هزارگان «٬» و ممیز «٫» نوشته می‌شود (با فرهنگ Invariant، هرگز fa که علامت منفی‌اش ASCII نیست)؛ بقیه
    /// با ToString. نشانه‌ای که مقدار ندارد دست‌نخورده می‌ماند تا جاافتادنش در آزمون دیده شود.
    /// </summary>
    public static string Fill(string template, IEnumerable<(string Key, object? Value)> tokens)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in tokens) values[key] = value;
        if (values.Count == 0 || !template.Contains('{')) return template;

        return TokenPattern().Replace(template, m =>
            values.TryGetValue(m.Groups[1].Value, out var value) ? Render(value) : m.Value);
    }

    /// <summary>عدد برای متن فارسی: «۱۰٬۰۰۰٬۰۰۰» و «۲٫۵».</summary>
    public static string FormatNumber(decimal value)
    {
        var invariant = value.ToString("#,0.##", CultureInfo.InvariantCulture);
        var builder = new StringBuilder(invariant.Length);
        foreach (var c in invariant)
        {
            builder.Append(c switch
            {
                >= '0' and <= '9' => (char)(c - '0' + '۰'),
                ',' => '٬',
                '.' => '٫',
                _ => c
            });
        }
        return builder.ToString();
    }

    private static string Render(object? value) => value switch
    {
        null => string.Empty,
        decimal d => FormatNumber(d),
        int i => FormatNumber(i),
        long l => FormatNumber(l),
        short s => FormatNumber(s),
        double f when !double.IsNaN(f) && !double.IsInfinity(f) => FormatNumber((decimal)f),
        _ => value.ToString() ?? string.Empty
    };

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}")]
    private static partial Regex TokenPattern();

    private sealed record Registry(IReadOnlyDictionary<string, MessageTemplate> ByCode, IReadOnlyList<string> Duplicates);

    private static Registry BuildRegistry()
    {
        var byCode = new Dictionary<string, MessageTemplate>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        var fields = typeof(Messages)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(MessageTemplate));

        foreach (var field in fields)
        {
            if (field.GetValue(null) is not MessageTemplate template) continue;
            if (!byCode.TryAdd(template.Code, template)) duplicates.Add(template.Code);
        }

        return new Registry(byCode, duplicates);
    }
}
