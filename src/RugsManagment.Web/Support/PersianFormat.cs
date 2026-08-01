using System.Globalization;

namespace RugsManagment.Web.Support;

/// <summary>
/// قالب‌بندی فارسی: تاریخ شمسی، ارقام فارسی و واحد پول.
///
/// نکته: همهٔ تاریخ‌ها در دیتابیس UTC می‌مانند؛ تبدیل فقط هنگام نمایش انجام می‌شود.
/// منطقهٔ زمانی نمایش «ساعت رسمی ایران» است تا «۲ ساعت پیش» برای کاربر درست باشد.
/// </summary>
public static class PersianFormat
{
    private static readonly PersianCalendar Calendar = new();

    private static readonly string[] MonthNames =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    ];

    private static readonly TimeZoneInfo IranTimeZone = ResolveIranTimeZone();

    /// <summary>ارقام لاتین ۰-۹ را به ارقام فارسی ۰-۹ تبدیل می‌کند (جداکننده‌ها دست‌نخورده).</summary>
    public static string ToPersianDigits(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is >= '0' and <= '9' ? (char)(c - '0' + '۰') : c;
            }
        });
    }

    /// <summary>تاریخ شمسی کوتاه: ۱۴۰۵/۰۵/۰۵</summary>
    public static string Date(DateTimeOffset? value)
    {
        if (!value.HasValue) return "—";
        var local = ToIranTime(value.Value);
        return ToPersianDigits(
            $"{Calendar.GetYear(local):0000}/{Calendar.GetMonth(local):00}/{Calendar.GetDayOfMonth(local):00}");
    }

    /// <summary>تاریخ خوانا: ۵ مرداد ۱۴۰۵</summary>
    public static string DateLong(DateTimeOffset? value)
    {
        if (!value.HasValue) return "—";
        var local = ToIranTime(value.Value);
        return ToPersianDigits(
            $"{Calendar.GetDayOfMonth(local)} {MonthNames[Calendar.GetMonth(local) - 1]} {Calendar.GetYear(local)}");
    }

    /// <summary>تاریخ و ساعت: ۵ مرداد ۱۴۰۵ · ۱۴:۳۰</summary>
    public static string DateTime(DateTimeOffset? value)
    {
        if (!value.HasValue) return "—";
        var local = ToIranTime(value.Value);
        return $"{DateLong(value)} · {ToPersianDigits($"{local:HH:mm}")}";
    }

    /// <summary>فاصلهٔ نسبی: «۳ روز پیش»، «۲ ساعت پیش»، «همین حالا».</summary>
    public static string Relative(DateTimeOffset? value)
    {
        if (!value.HasValue) return "—";

        var span = DateTimeOffset.UtcNow - value.Value;
        if (span < TimeSpan.Zero) return DateLong(value);

        if (span.TotalMinutes < 1) return "همین حالا";
        if (span.TotalHours < 1) return ToPersianDigits($"{(int)span.TotalMinutes}") + " دقیقه پیش";
        if (span.TotalDays < 1) return ToPersianDigits($"{(int)span.TotalHours}") + " ساعت پیش";
        if (span.TotalDays < 30) return ToPersianDigits($"{(int)span.TotalDays}") + " روز پیش";
        return DateLong(value);
    }

    /// <summary>مبلغ با جداکنندهٔ هزارگان و ارقام فارسی — بدون واحد.</summary>
    public static string Money(decimal? value)
        => value.HasValue
            ? ToPersianDigits(value.Value.ToString("#,0", CultureInfo.InvariantCulture))
            : "—";

    /// <summary>مبلغ همراه واحد: «۱٬۲۵۰٬۰۰۰ تومان»</summary>
    public static string MoneyWithUnit(decimal? value)
        => value.HasValue ? $"{Money(value)} تومان" : "—";

    /// <summary>عدد اعشاری کوتاه با ارقام فارسی (مثلاً ابعاد فرش).</summary>
    public static string Number(decimal value)
        => ToPersianDigits(value.ToString("0.##", CultureInfo.InvariantCulture));

    /// <summary>عدد صحیح با ارقام فارسی (شمارنده‌ها).</summary>
    public static string Count(int value)
        => ToPersianDigits(value.ToString("#,0", CultureInfo.InvariantCulture));

    /// <summary>
    /// شروع روزِ انتخاب‌شده به وقت ایران، به‌صورت لحظهٔ UTC.
    ///
    /// چرا لازم است: وقتی کاربر در فیلتر «از تاریخ» یک روز را انتخاب می‌کند منظورش
    /// ابتدای همان روز به وقت ایران است، نه نیمه‌شب منطقهٔ زمانی سرور. ضمناً پستگرس
    /// برای ستون timestamptz فقط offset صفر (UTC) می‌پذیرد.
    /// </summary>
    public static DateTimeOffset? IranDayStartUtc(DateTimeOffset? value)
    {
        if (!value.HasValue) return null;
        var day = value.Value.Date;
        var offset = IranTimeZone.GetUtcOffset(day);
        return new DateTimeOffset(day, offset).ToUniversalTime();
    }

    /// <summary>
    /// پایان روزِ انتخاب‌شده به وقت ایران (لحظهٔ UTC) — تا فیلتر «تا تاریخ» شاملِ خود آن روز باشد.
    /// </summary>
    public static DateTimeOffset? IranDayEndUtc(DateTimeOffset? value)
    {
        var start = IranDayStartUtc(value);
        return start?.AddDays(1).AddTicks(-1);
    }

    // ─────────────────────────────────────────────────────────
    private static DateTime ToIranTime(DateTimeOffset value)
        => TimeZoneInfo.ConvertTime(value, IranTimeZone).DateTime;

    /// <summary>
    /// شناسهٔ منطقهٔ زمانی روی ویندوز و لینوکس فرق دارد؛ هر دو را امتحان می‌کنیم
    /// و اگر هیچ‌کدام نبود روی UTC+3:30 ثابت برمی‌گردیم.
    /// </summary>
    private static TimeZoneInfo ResolveIranTimeZone()
    {
        foreach (var id in new[] { "Iran Standard Time", "Asia/Tehran" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Iran-Fallback", TimeSpan.FromMinutes(210), "ایران", "ایران");
    }
}
