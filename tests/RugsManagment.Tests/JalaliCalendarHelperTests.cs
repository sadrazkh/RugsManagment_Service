using RugsManagment.Application.Common;

namespace RugsManagment.Tests;

/// <summary>
/// مرزهای ماه شمسی — پایهٔ گزارش روند. اشتباه اینجا یعنی فروش در ماه غلط جمع می‌شود.
/// </summary>
public class JalaliCalendarHelperTests
{
    [Fact]
    public void AddJalaliMonths_Zero_ReturnsSameMonth()
    {
        var start = JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc();
        Assert.Equal(JalaliCalendarHelper.SortKey(start),
                     JalaliCalendarHelper.SortKey(JalaliCalendarHelper.AddJalaliMonths(start, 0)));
    }

    [Fact]
    public void AddJalaliMonths_TwelveForward_AdvancesExactlyOneYear()
    {
        var start = JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc();
        var nextYear = JalaliCalendarHelper.AddJalaliMonths(start, 12);

        Assert.Equal(JalaliCalendarHelper.SortKey(start) + 100, JalaliCalendarHelper.SortKey(nextYear));
    }

    [Fact]
    public void AddJalaliMonths_BackwardsAcrossYearBoundary_Works()
    {
        var start = JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc();
        var back = JalaliCalendarHelper.AddJalaliMonths(start, -12);

        Assert.Equal(JalaliCalendarHelper.SortKey(start) - 100, JalaliCalendarHelper.SortKey(back));
    }

    [Fact]
    public void ConsecutiveMonths_AreStrictlyIncreasingAndContiguous()
    {
        // ستون‌های نمودار روند باید بدون شکاف و بدون همپوشانی باشند
        var start = JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc();

        for (var i = -12; i < 0; i++)
        {
            var current = JalaliCalendarHelper.AddJalaliMonths(start, i);
            var next = JalaliCalendarHelper.AddJalaliMonths(start, i + 1);

            Assert.True(current < next, $"ماه {i} باید قبل از {i + 1} باشد");

            // فاصله باید بین ۲۹ تا ۳۲ روز باشد (ماه‌های شمسی ۲۹/۳۰/۳۱ روزه‌اند)
            var days = (next - current).TotalDays;
            Assert.InRange(days, 29, 32);
        }
    }

    [Fact]
    public void SortKey_HasYearMonthShape()
    {
        var key = JalaliCalendarHelper.SortKey(JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc());
        var month = key % 100;

        Assert.InRange(month, 1, 12);
        Assert.InRange(key / 100, 1400, 1500);
    }

    [Fact]
    public void MonthLabel_UsesPersianMonthNameAndDigits()
    {
        var label = JalaliCalendarHelper.MonthLabel(JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc());

        string[] months =
        [
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        ];

        Assert.Contains(months, m => label.StartsWith(m));
        // سال باید با ارقام فارسی باشد، نه لاتین
        Assert.DoesNotContain(label, c => c is >= '0' and <= '9');
    }

    [Fact]
    public void MonthStart_IsStoredAsUtc()
    {
        var start = JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc();
        Assert.Equal(TimeSpan.Zero, start.Offset);
    }
}
