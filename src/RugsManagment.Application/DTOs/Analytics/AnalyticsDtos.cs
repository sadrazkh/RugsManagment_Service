namespace RugsManagment.Application.DTOs.Analytics;

/// <summary>شدت هشدار کهنگی — با آیکون و برچسب نمایش داده می‌شود، نه فقط رنگ.</summary>
public enum AgingSeverity
{
    /// <summary>در محدودهٔ عادی</summary>
    Normal = 0,

    /// <summary>کمی طولانی شده — نیاز به پیگیری</summary>
    Warning = 1,

    /// <summary>خیلی طولانی — احتمالاً گیر کرده</summary>
    Serious = 2,

    /// <summary>بحرانی — فرش عملاً فراموش شده</summary>
    Critical = 3
}

/// <summary>یک فرش که در مرحلهٔ جاری‌اش مانده است.</summary>
public record AgingItemDto(
    Guid RugId,
    string Sku,
    string? Title,
    string StepName,
    /// <summary>چند روز است در همین مرحله مانده</summary>
    int DaysInStep,
    DateTimeOffset? StepStartedAt,
    string? ServiceProviderName,
    AgingSeverity Severity);

/// <summary>
/// گزارش کهنگی: فرش‌هایی که در مرحلهٔ جاری‌شان بیش از حد معمول مانده‌اند.
/// این «گلوگاه» کارگاه را نشان می‌دهد — کاری که هیچ گزارش دیگری نمی‌گوید.
/// </summary>
public record AgingReportDto(
    IReadOnlyList<AgingItemDto> Items,
    int WarningCount,
    int SeriousCount,
    int CriticalCount);

/// <summary>هزینه و زمان یک نوع مرحله در کل کارگاه.</summary>
public record StepBreakdownDto(
    Guid StepTypeId,
    string StepName,
    /// <summary>چند بار این مرحله انجام شده</summary>
    int CompletedCount,
    decimal TotalCost,
    decimal AverageCost,
    /// <summary>میانگین روزهای طول کشیدن؛ null وقتی زمان‌بندی ثبت نشده</summary>
    double? AverageDurationDays,
    /// <summary>چند فرش همین حالا روی این مرحله‌اند</summary>
    int InProgressCount);

/// <summary>یک ماه شمسی در نمودار روند.</summary>
public record TrendPointDto(
    /// <summary>کلید مرتب‌سازی: yyyyMM شمسی</summary>
    int SortKey,
    /// <summary>برچسب فارسی: «مرداد ۱۴۰۵»</summary>
    string Label,
    int RugsAdded,
    int RugsSold,
    decimal SalesNet,
    decimal Profit);

public record AnalyticsReportDto(
    AgingReportDto Aging,
    IReadOnlyList<StepBreakdownDto> StepBreakdown,
    IReadOnlyList<TrendPointDto> Trend);
