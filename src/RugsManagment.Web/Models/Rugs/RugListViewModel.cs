using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Workflows;

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
    {
        var values = new Dictionary<string, string?>();

        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value)) values[key] = value;
        }

        Add("search", Query.Search);
        Add("status", Query.Status?.ToString());
        Add("batchId", Query.BatchId?.ToString());
        Add("stepTypeId", Query.StepTypeId?.ToString());
        Add("createdFrom", Query.CreatedFrom?.ToString("yyyy-MM-dd"));
        Add("createdTo", Query.CreatedTo?.ToString("yyyy-MM-dd"));

        var effectiveSort = sortBy ?? Query.SortBy;
        var effectiveDescending = descending ?? Query.Descending;

        // پیش‌فرض‌ها را در آدرس نمی‌گذاریم تا لینک‌ها کوتاه و خوانا بمانند
        if (effectiveSort != RugSortBy.CreatedAt) Add("sortBy", effectiveSort.ToString());
        if (!effectiveDescending) values["descending"] = "false";
        if (Query.PageSize != RugQuery.DefaultPageSize) Add("pageSize", Query.PageSize.ToString());

        var effectivePage = page ?? Query.Page;
        if (effectivePage > 1) Add("page", effectivePage.ToString());

        return values;
    }

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
