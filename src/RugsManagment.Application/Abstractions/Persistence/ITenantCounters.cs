namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>
/// شمارنده‌های اتمیک هر کارگاه.
/// </summary>
public interface ITenantCounters
{
    /// <summary>
    /// <paramref name="count"/> عدد پشت‌سرهم رزرو می‌کند و <b>آخرین</b> عدد بلوک را برمی‌گرداند؛
    /// بلوک یعنی <c>[last - count + 1 .. last]</c>.
    /// اتمیک است (UPSERT در دیتابیس) و در فراخوانی‌های هم‌زمان هرگز عدد تکراری نمی‌دهد.
    /// اولین استفاده از کلیدی که با <c>sku:</c> شروع می‌شود، از بزرگ‌ترین SKU موجود آن ماه
    /// (حتی فرش‌های حذف‌شده) ادامه می‌دهد تا با دادهٔ قدیمی برخورد نکند.
    /// </summary>
    Task<long> NextBlockAsync(Guid tenantId, string key, int count = 1, CancellationToken cancellationToken = default);
}
