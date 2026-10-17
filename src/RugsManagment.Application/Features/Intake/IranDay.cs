using System.Globalization;

namespace RugsManagment.Application.Features.Intake;

/// <summary>
/// «روز ایران» برای کلید ورودی (B.5) و جدول ورود: کدام روز تقویم، به وقت تهران.
///
/// چرا جدا از تاریخ UTC: ورودی‌ای که ساعت ۰۱:۰۰ بامداد تهران ثبت می‌شود در UTC هنوز «دیروز» است؛ اگر کلید ورودی روز UTC
/// بود، فرش‌های یک صبحِ یک فروشنده دو ورودی می‌شدند. ارقام همیشه با فرهنگ Invariant (فرهنگ fa ارقام و علامت منفی‌اش
/// ASCII نیست).
/// </summary>
public static class IranDay
{
    private static readonly TimeZoneInfo Zone = Resolve();
    private static readonly PersianCalendar Jalali = new();

    /// <summary>امروز به وقت ایران.</summary>
    public static DateOnly Today(DateTimeOffset? now = null) => Of(now ?? DateTimeOffset.UtcNow);

    /// <summary>روز ایرانِ یک لحظه.</summary>
    public static DateOnly Of(DateTimeOffset moment)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, Zone).DateTime);

    /// <summary>آغاز یک روز ایران به‌صورت لحظهٔ UTC.</summary>
    public static DateTimeOffset StartUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>
    /// لحظهٔ «رسیدن» برای ورودی تازه: اگر روز همین امروز است همین حالا، وگرنه ظهر همان روز به وقت ایران (تا هر تبدیل
    /// منطقهٔ زمانی همان روز را بدهد).
    /// </summary>
    public static DateTimeOffset ReceivedMoment(DateOnly day, DateTimeOffset now)
        => Of(now) == day ? now : StartUtc(day).AddHours(12);

    /// <summary>تاریخ شمسی با ارقام لاتین: «1405/06/15».</summary>
    public static string JalaliLatin(DateOnly day)
    {
        var dt = day.ToDateTime(TimeOnly.MinValue);
        return string.Create(CultureInfo.InvariantCulture,
            $"{Jalali.GetYear(dt):0000}/{Jalali.GetMonth(dt):00}/{Jalali.GetDayOfMonth(dt):00}");
    }

    /// <summary>
    /// متن روز از درخواست: <c>yyyy-MM-dd</c> (روز ایران، همان که <c>JalaliDateField</c> می‌فرستد) یا یک لحظهٔ ISO کامل
    /// (که روز ایرانش گرفته می‌شود). ارقام فارسی هم پذیرفته است. نادرست → false.
    /// </summary>
    public static bool TryParse(string? text, DateTimeOffset now, out DateOnly day)
    {
        day = Of(now);
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var normalized = Common.PersianText.Normalize(text);
        if (DateOnly.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            day = date;
            return true;
        }

        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment))
        {
            day = Of(moment);
            return true;
        }

        return false;
    }

    private static TimeZoneInfo Resolve()
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
