namespace RugsManagment.Application.DTOs.Common;

/// <summary>
/// یک صفحه از نتایج به‌همراه اطلاعات لازم برای ساخت نوار صفحه‌بندی.
/// </summary>
/// <param name="Items">ردیف‌های همین صفحه</param>
/// <param name="TotalCount">تعداد کل ردیف‌های منطبق با فیلتر (نه فقط این صفحه)</param>
/// <param name="Page">شمارهٔ صفحهٔ جاری، از ۱</param>
/// <param name="PageSize">تعداد ردیف در هر صفحه</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>تعداد کل صفحات — حداقل ۱ تا نوار صفحه‌بندی همیشه معنا داشته باشد.</summary>
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    /// <summary>شمارهٔ ردیف اول این صفحه (برای متن «۲۶ تا ۵۰ از ۱۳۴»).</summary>
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItemNumber => Math.Min(Page * PageSize, TotalCount);

    public static PagedResult<T> Empty(int pageSize) => new([], 0, 1, pageSize);
}
