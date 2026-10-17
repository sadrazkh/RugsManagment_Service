using System.Globalization;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Models.Rugs;

/// <summary>
/// همه‌چیزِ لازم برای صفحهٔ فهرست فرش‌ها: نتیجهٔ صفحه‌بندی‌شده + کوئری فعلی
/// (برای ساخت لینک‌های مرتب‌سازی و صفحه) + دادهٔ انتخابگرهای فیلتر.
/// </summary>
public sealed class RugListViewModel
{
    public required PagedResult<RugListItemDto> Result { get; init; }

    /// <summary>کوئری اعمال‌شده — پایهٔ ساخت هر لینک دیگری در صفحه</summary>
    public required RugQuery Query { get; init; }

    public required IReadOnlyList<RugBatchDto> Batches { get; init; }
    public required IReadOnlyList<ProcessStepTypeDto> StepTypes { get; init; }

    /// <summary>
    /// کوئری فعلی را به دیکشنری route value تبدیل می‌کند تا لینک‌ها
    /// فیلترهای فعال را حفظ کنند. مقادیر خالی حذف می‌شوند تا آدرس تمیز بماند.
    /// </summary>
    public Dictionary<string, string?> RouteValues(
        RugSortBy? sortBy = null,
        bool? descending = null,
        int? page = null)
        => ToRouteValues(Query, sortBy, descending, page);

    /// <summary>
    /// همان <see cref="RouteValues"/> برای هر کوئری — تا redirectهای کنترلر هم همهٔ فیلترها را نگه دارند و فهرست کلیدها
    /// فقط همین یک جا باشد. هر عضو فیلتر <see cref="RugQuery"/> این‌جا یک کلید دارد، با همان نامی که model binder از
    /// query string می‌خواند؛ عضو تازه‌ای که این‌جا نیاید بعد از اولین مرتب‌سازی یا صفحهٔ بعد بی‌صدا گم می‌شود.
    /// تاریخ‌ها همان روزی‌اند که کاربر انتخاب کرد (yyyy-MM-dd)؛ پس کوئریِ نمایشی را بدهید، نه نسخهٔ UTC جستجو.
    /// </summary>
    public static Dictionary<string, string?> ToRouteValues(
        RugQuery query,
        RugSortBy? sortBy = null,
        bool? descending = null,
        int? page = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        var values = new Dictionary<string, string?>();

        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value)) values[key] = value;
        }

        Add("search", query.Search);
        Add("serial", query.Serial);
        Add("status", query.Status?.ToString());
        Add("location", query.Location?.ToString());
        Add("batchId", query.BatchId?.ToString());
        if (query.WithoutBatch) values["withoutBatch"] = "true";
        Add("shipmentId", query.ShipmentId?.ToString());
        Add("baleId", query.BaleId?.ToString());
        if (query.NotBaled) values["notBaled"] = "true";
        Add("stepTypeId", query.StepTypeId?.ToString());
        Add("createdFrom", DayText(query.CreatedFrom));
        Add("createdTo", DayText(query.CreatedTo));

        var effectiveSort = sortBy ?? query.SortBy;
        var effectiveDescending = descending ?? query.Descending;

        // پیش‌فرض‌ها را در آدرس نمی‌گذاریم تا لینک‌ها کوتاه و خوانا بمانند
        if (effectiveSort != RugSortBy.CreatedAt) Add("sortBy", effectiveSort.ToString());
        if (!effectiveDescending) values["descending"] = "false";
        if (query.PageSize != RugQuery.DefaultPageSize)
            Add("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture));

        var effectivePage = page ?? query.Page;
        if (effectivePage > 1) Add("page", effectivePage.ToString(CultureInfo.InvariantCulture));

        return values;
    }

    /// <summary>
    /// لینک یک چیپ یا گزینهٔ فیلتر: همهٔ فیلترهای فعلی، با <paramref name="key"/> = <paramref name="value"/> (خالی =
    /// برداشتن همان فیلتر)، از صفحهٔ ۱. کلید همان نام query string است (مثل <c>location</c> یا <c>status</c>).
    /// </summary>
    public Dictionary<string, string?> WithFilter(string key, string? value)
    {
        var values = RouteValues(page: 1);
        if (string.IsNullOrEmpty(value)) values.Remove(key);
        else values[key] = value;
        return values;
    }

    /// <summary>
    /// لینک چیپ جا («در انبار»، «در نگله»، «صادر شده»، «فروخته‌شده»)؛ null = «همه». چیپی که همین حالا فعال است با
    /// <see cref="IsLocation"/> شناخته می‌شود.
    /// </summary>
    public Dictionary<string, string?> LocationRoute(RugLocation? location)
        => WithFilter("location", location?.ToString());

    /// <summary>آیا فیلتر جای فعلی همین است (null = «همه»).</summary>
    public bool IsLocation(RugLocation? location) => Query.Location == location;

    /// <summary>مقدار خانهٔ تاریخ «از» در فرم فیلتر (yyyy-MM-dd)؛ همان قاعدهٔ <see cref="DayText"/>.</summary>
    public string? CreatedFromText => DayText(Query.CreatedFrom);

    /// <summary>مقدار خانهٔ تاریخ «تا» در فرم فیلتر.</summary>
    public string? CreatedToText => DayText(Query.CreatedTo);

    /// <summary>
    /// روزِ یک فیلتر تاریخ به شکل yyyy-MM-dd، همان روزی که کاربر انتخاب کرد.
    ///
    /// چرا <c>ToLocalTime</c>: model binder تاریخِ بی‌ساعت را نیمه‌شبِ وقت محلیِ سرور می‌سازد و <see cref="RugQuery.Sanitized"/>
    /// آن را به UTC می‌برد؛ نوشتن همان UTC روی سروری شرق گرینویچ (مثل +۳:۳۰) روز قبل را می‌نوشت و هر کلیک فیلتر را یک روز
    /// عقب می‌برد. برگرداندن به وقت محلی همان نیمه‌شب را می‌دهد، هر منطقهٔ زمانی‌ای که سرور داشته باشد. فرهنگ ثابت لازم
    /// است، چون تقویم پیش‌فرض fa-IR شمسی است و binder عدد سال را میلادی می‌خواند.
    /// </summary>
    public static string? DayText(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// route valueهای لازم برای سرستون قابل مرتب‌سازی.
    /// کلیک روی ستون فعال، جهت را برعکس می‌کند؛ کلیک روی ستون دیگر با جهت پیش‌فرض شروع می‌شود.
    /// همیشه به صفحهٔ ۱ برمی‌گردد چون ترتیب عوض شده است.
    /// </summary>
    public Dictionary<string, string?> SortRoute(RugSortBy column)
    {
        var isActive = Query.SortBy == column;
        // ستون‌های متنی صعودی طبیعی‌ترند؛ تاریخ و مبلغ نزولی
        var defaultDescending = column is RugSortBy.CreatedAt or RugSortBy.TotalCost or RugSortBy.Area;
        var descending = isActive ? !Query.Descending : defaultDescending;

        return RouteValues(sortBy: column, descending: descending, page: 1);
    }

    /// <summary>نشانگر جهت مرتب‌سازی برای سرستون؛ null یعنی این ستون فعال نیست.</summary>
    public string? SortIcon(RugSortBy column)
        => Query.SortBy != column ? null : Query.Descending ? "chevron-down" : "chevron-up";

    /// <summary>مقدار aria-sort برای صفحه‌خوان.</summary>
    public string AriaSort(RugSortBy column)
        => Query.SortBy != column ? "none" : Query.Descending ? "descending" : "ascending";
}
