using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// ردیف سبک فهرست فرش‌ها.
///
/// عمداً از <see cref="RugDto"/> جداست: فهرست نباید همهٔ مراحل هر فرش را لود کند.
/// همهٔ مقادیر (از جمله هزینه و مرحلهٔ جاری) مستقیم در SQL محاسبه می‌شوند.
/// </summary>
public record RugListItemDto(
    Guid Id,
    string Sku,
    string? Title,
    string? Origin,
    string? Pattern,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal AreaSquareMeters,
    RugStatus Status,
    /// <summary>بندانگشتیِ عکس شاخص؛ null یعنی فرش هنوز عکسی ندارد</summary>
    string? ThumbnailUrl,
    Guid? BatchId,
    string? BatchName,
    string? CurrentStepNameFa,
    /// <summary>شناسهٔ مرحلهٔ در حال انجام — برای دکمهٔ «مرحلهٔ بعد» در لیست؛ null یعنی مرحلهٔ فعالی نیست</summary>
    Guid? ActiveStepId,
    decimal TotalInvestment,
    /// <summary>مراحل تمام‌شده (تکمیل یا رد‌شده) — برای نوار پیشرفت بدون لود کردن خود مراحل</summary>
    int CompletedStepCount,
    int TotalStepCount,
    DateTimeOffset CreatedAt);

/// <summary>ستون‌های قابل مرتب‌سازی در فهرست فرش‌ها.</summary>
public enum RugSortBy
{
    CreatedAt = 0,
    Sku = 1,
    Title = 2,
    Area = 3,
    TotalCost = 4,
    Status = 5,
    CurrentStep = 6
}

/// <summary>
/// فیلتر/مرتب‌سازی/صفحه‌بندی فهرست فرش‌ها.
/// مقادیر مستقیماً از query string صفحه bind می‌شوند تا آدرس قابل اشتراک و bookmark باشد.
/// </summary>
public record RugQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>جستجو در کد، عنوان، اصالت، طرح، جنس و یادداشت</summary>
    public string? Search { get; init; }

    public RugStatus? Status { get; init; }
    public Guid? BatchId { get; init; }

    /// <summary>فقط فرش‌هایی که به هیچ گروهی تعلق ندارند (برای انتخابگر «افزودن به گروه»)</summary>
    public bool WithoutBatch { get; init; }

    /// <summary>فقط فرش‌هایی که همین حالا روی این نوع مرحله هستند</summary>
    public Guid? StepTypeId { get; init; }

    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }

    public RugSortBy SortBy { get; init; } = RugSortBy.CreatedAt;
    public bool Descending { get; init; } = true;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// ورودی را امن و قابل‌استفاده می‌کند: صفحه و اندازهٔ صفحه در بازهٔ معتبر،
    /// جستجوی خالی به null، و تاریخ‌ها به UTC.
    ///
    /// تبدیل به UTC ضروری است چون PostgreSQL برای ستون timestamptz فقط offset صفر
    /// می‌پذیرد؛ model binder اما تاریخِ بدون ساعت را با offset محلیِ سرور می‌سازد.
    /// </summary>
    public RugQuery Sanitized() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize < 5 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize),
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        CreatedFrom = CreatedFrom?.ToUniversalTime(),
        CreatedTo = CreatedTo?.ToUniversalTime()
    };

    /// <summary>آیا فیلتری غیر از مرتب‌سازی/صفحه فعال است؟ (برای نمایش دکمهٔ «حذف فیلترها»)</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || Status.HasValue
        || BatchId.HasValue
        || WithoutBatch
        || StepTypeId.HasValue
        || CreatedFrom.HasValue
        || CreatedTo.HasValue;
}
