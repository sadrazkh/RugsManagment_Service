using System.Globalization;

namespace RugsManagment.Application.Common;

/// <summary>
/// سطل‌بندی ماهانه بر پایهٔ تقویم شمسی.
///
/// چرا در Application و نه در لایهٔ وب: مرزهای ماه بخشی از خودِ محاسبهٔ گزارش است
/// (فروش «مرداد» باید دقیقاً از یکم تا آخر مرداد جمع شود)، نه صرفاً قالب‌بندی نمایش.
///
/// مرزها به وقت ایران محاسبه و به UTC تبدیل می‌شوند، چون داده‌ها UTC ذخیره شده‌اند.
/// </summary>
public static class JalaliCalendarHelper
{
    private static readonly PersianCalendar Calendar = new();

    private static readonly string[] MonthNames =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    ];

    private static readonly TimeZoneInfo IranTimeZone = ResolveIranTimeZone();

    /// <summary>ابتدای ماه شمسی جاری، به‌صورت لحظهٔ UTC.</summary>
    public static DateTimeOffset StartOfCurrentJalaliMonthUtc()
    {
        var nowInIran = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, IranTimeZone).DateTime;
        return StartOfJalaliMonthUtc(Calendar.GetYear(nowInIran), Calendar.GetMonth(nowInIran));
    }

    /// <summary>
    /// n ماه شمسی جلو/عقب می‌رود. جمع ساده روی روز کار نمی‌کند چون ماه‌های شمسی
    /// ۲۹، ۳۰ و ۳۱ روزه‌اند؛ پس روی خودِ تقویم حرکت می‌کنیم.
    /// </summary>
    public static DateTimeOffset AddJalaliMonths(DateTimeOffset monthStartUtc, int delta)
    {
        var inIran = TimeZoneInfo.ConvertTime(monthStartUtc, IranTimeZone).DateTime;

        var year = Calendar.GetYear(inIran);
        var month = Calendar.GetMonth(inIran) + delta;

        // نرمال‌سازی به بازهٔ ۱..۱۲ با انتقال سال
        year += (int)Math.Floor((month - 1) / 12.0);
        month = ((month - 1) % 12 + 12) % 12 + 1;

        return StartOfJalaliMonthUtc(year, month);
    }

    /// <summary>کلید مرتب‌سازی yyyyMM شمسی — مثلاً ۱۴۰۵۰۵</summary>
    public static int SortKey(DateTimeOffset monthStartUtc)
    {
        var inIran = TimeZoneInfo.ConvertTime(monthStartUtc, IranTimeZone).DateTime;
        return Calendar.GetYear(inIran) * 100 + Calendar.GetMonth(inIran);
    }

    /// <summary>برچسب فارسی ماه: «مرداد ۱۴۰۵»</summary>
    public static string MonthLabel(DateTimeOffset monthStartUtc)
    {
        var inIran = TimeZoneInfo.ConvertTime(monthStartUtc, IranTimeZone).DateTime;
        var label = $"{MonthNames[Calendar.GetMonth(inIran) - 1]} {Calendar.GetYear(inIran)}";
        return PersianText.ToPersianDigits(label);
    }

    // ─────────────────────────────────────────────────────────
    private static DateTimeOffset StartOfJalaliMonthUtc(int jalaliYear, int jalaliMonth)
    {
        var gregorian = Calendar.ToDateTime(jalaliYear, jalaliMonth, 1, 0, 0, 0, 0);
        var offset = IranTimeZone.GetUtcOffset(gregorian);
        return new DateTimeOffset(gregorian, offset).ToUniversalTime();
    }

    /// <summary>شناسهٔ منطقهٔ زمانی روی ویندوز و لینوکس فرق دارد؛ هر دو امتحان می‌شوند.</summary>
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
