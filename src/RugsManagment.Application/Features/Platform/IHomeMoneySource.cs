namespace RugsManagment.Application.Features.Platform;

/// <summary>کارت پولیِ خانه (فقط Business).</summary>
/// <param name="OpenShipmentCostPerM2">قیمت تمام‌شدهٔ هر متر اولین محمولهٔ باز</param>
/// <param name="UnallocatedEntries">تعداد هزینه‌های «تقسیم‌نشده»</param>
public sealed record HomeMoney(decimal? OpenShipmentCostPerM2, int? UnallocatedEntries)
{
    /// <summary>هنوز دفتر هزینه‌ای نیست: هر دو «معلوم نیست»، نه صفر.</summary>
    public static readonly HomeMoney Unknown = new(null, null);
}

/// <summary>
/// منبع کارت پولیِ خانه — درزی که فاز هزینه پر می‌کند.
///
/// سرویس خلاصهٔ خانه فقط وقتی این را صدا می‌زند که بیننده Business است؛ پس پیاده‌سازی لازم نیست خودش
/// پنهان‌سازی کند. فاز سکو <see cref="UnknownHomeMoneySource"/> را ثبت می‌کند و فاز هزینه پیاده‌سازی خودش را
/// با <c>services.Replace(ServiceDescriptor.Scoped&lt;IHomeMoneySource, …&gt;())</c> جایگزین می‌کند.
/// </summary>
public interface IHomeMoneySource
{
    /// <param name="tenantId">کارگاه</param>
    /// <param name="firstOpenShipmentId">اولین محمولهٔ باز (همان که کاشی‌ها هدف می‌گیرند)؛ null یعنی محمولهٔ بازی نیست</param>
    /// <param name="cancellationToken">لغو</param>
    Task<HomeMoney> GetAsync(Guid tenantId, Guid? firstOpenShipmentId, CancellationToken cancellationToken = default);
}

/// <summary>پیاده‌سازی فاز سکو: هنوز هزینه‌ای ثبت نمی‌شود.</summary>
public sealed class UnknownHomeMoneySource : IHomeMoneySource
{
    public Task<HomeMoney> GetAsync(Guid tenantId, Guid? firstOpenShipmentId, CancellationToken cancellationToken = default)
        => Task.FromResult(HomeMoney.Unknown);
}
