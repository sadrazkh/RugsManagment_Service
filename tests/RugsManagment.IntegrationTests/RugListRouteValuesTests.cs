using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Models.Rugs;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// رفت‌وبرگشت لینک‌های فهرست فرش‌ها (spec I.3 «RugListViewModel link round-trip»): هر فیلتر <see cref="RugQuery"/> باید در
/// لینک‌های مرتب‌سازی، صفحه و چیپ‌ها بماند، وگرنه کاربر با اولین کلیک فیلترش را بی‌صدا گم می‌کند (همان اتفاقی که برای
/// <c>withoutBatch</c> پیش از فاز ۲ می‌افتاد). تبدیل برگشتی با همان TypeConverter و فرهنگ ثابتی است که model binder برای
/// query string به کار می‌برد؛ دیتابیس لازم نیست.
/// </summary>
public sealed class RugListRouteValuesTests
{
    private static readonly HashSet<string> NotFilters =
        [nameof(RugQuery.SortBy), nameof(RugQuery.Descending), nameof(RugQuery.Page), nameof(RugQuery.PageSize)];

    public static TheoryData<string> FilterProperties()
    {
        var data = new TheoryData<string>();
        foreach (var p in typeof(RugQuery).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.SetMethod is not null && !NotFilters.Contains(p.Name))
                data.Add(p.Name);
        return data;
    }

    private static object SampleValue(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(string) => "3911 - 3960",
        var t when t == typeof(bool) => true,
        var t when t == typeof(Guid) => Guid.Parse("0b6f3a52-6c1e-4c39-9a51-2f0f5a1f0c11"),
        var t when t == typeof(DateTimeOffset) => BoundDay("2026-09-06"),
        var t when t == typeof(RugStatus) => RugStatus.ReadyForSale,
        var t when t == typeof(RugLocation) => RugLocation.Sold,
        var t => throw new InvalidOperationException($"فیلتر {t.Name} نمونه ندارد؛ این آزمون را برای نوع تازه کامل کنید.")
    };

    /// <summary>
    /// تاریخی که binder از «yyyy-MM-dd» می‌سازد (نیمه‌شب وقت محلی سرور) و بعد <see cref="RugQuery.Sanitized"/> به UTC
    /// می‌برد — همان مقداری که کوئری نمایشی صفحه نگه می‌دارد.
    /// </summary>
    private static DateTimeOffset BoundDay(string day)
        => ((DateTimeOffset)Bind(typeof(DateTimeOffset), day)!).ToUniversalTime();

    private static RugListViewModel Model(RugQuery query) => new()
    {
        Result = PagedResult<RugListItemDto>.Empty(query.PageSize),
        Query = query,
        Batches = [],
        StepTypes = []
    };

    /// <summary>همان کاری که model binder با یک مقدار query string می‌کند.</summary>
    private static object? Bind(Type type, string value)
        => TypeDescriptor.GetConverter(Nullable.GetUnderlyingType(type) ?? type)
            .ConvertFromString(null, CultureInfo.InvariantCulture, value);

    [Theory]
    [MemberData(nameof(FilterProperties))]
    public void EveryFilter_SurvivesSortPageAndChipLinks(string property)
    {
        var info = typeof(RugQuery).GetProperty(property)!;
        var expected = SampleValue(info.PropertyType);
        var query = new RugQuery();
        info.SetValue(query, expected);
        var model = Model(query);
        var key = char.ToLowerInvariant(property[0]) + property[1..];

        foreach (var route in new[]
                 {
                     model.RouteValues(),
                     model.RouteValues(page: 3),
                     model.SortRoute(RugSortBy.Serial),
                     model.WithFilter("status", nameof(RugStatus.Sold)),
                     RugListViewModel.ToRouteValues(query, RugSortBy.Title, descending: false, page: 2)
                 })
        {
            if (key == "status" && route.TryGetValue("status", out var s) && s == nameof(RugStatus.Sold))
                continue; // همین لینک خودش status را عوض کرده است

            Assert.True(route.TryGetValue(key, out var text), $"«{key}» در لینک نیامده است.");
            // DateTimeOffset با لحظه مقایسه می‌شود، پس روز جابه‌جاشده همین‌جا می‌شکند
            Assert.Equal(expected, Bind(info.PropertyType, text!));
        }
    }

    [Fact]
    public void DefaultQuery_GivesACleanUrl()
        => Assert.Empty(Model(new RugQuery()).RouteValues());

    [Fact]
    public void SortAndPage_AreKeptOnlyWhenNotDefault()
    {
        var route = Model(new RugQuery { SortBy = RugSortBy.Serial, Descending = false, Page = 2, PageSize = 50 }).RouteValues();
        Assert.Equal("Serial", route["sortBy"]);
        Assert.Equal("false", route["descending"]);
        Assert.Equal("2", route["page"]);
        Assert.Equal("50", route["pageSize"]);
    }

    [Fact]
    public void LocationChips_SetOrClearTheLocation_FromPageOne()
    {
        var model = Model(new RugQuery { Serial = "3778", Location = RugLocation.InStock, Page = 4 });

        var sold = model.LocationRoute(RugLocation.Sold);
        Assert.Equal("Sold", sold["location"]);
        Assert.Equal("3778", sold["serial"]);
        Assert.False(sold.ContainsKey("page"));

        var all = model.LocationRoute(null);
        Assert.False(all.ContainsKey("location"));
        Assert.Equal("3778", all["serial"]);

        Assert.True(model.IsLocation(RugLocation.InStock));
        Assert.False(model.IsLocation(null));
    }

    [Fact]
    public void Dates_AreWrittenWithTheInvariantGregorianCalendar()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // فرهنگ fa-IR تقویم پیش‌فرضش شمسی است؛ «yyyy» بدون فرهنگ ثابت «1405» می‌نوشت و binder آن را میلادی می‌خواند
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            var model = Model(new RugQuery { CreatedFrom = BoundDay("2026-09-06") });
            Assert.Equal("2026-09-06", model.RouteValues()["createdFrom"]);
            Assert.Equal("2026-09-06", model.CreatedFromText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
