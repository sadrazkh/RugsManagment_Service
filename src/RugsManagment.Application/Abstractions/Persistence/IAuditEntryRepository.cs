using RugsManagment.Application.DTOs.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>
/// خواندن تاریخچهٔ فعالیت — شمارش و صفحه‌بندی در SQL.
///
/// پیش از این همهٔ رویدادهای کارگاه به حافظه می‌آمد و بعد صفحه‌بندی می‌شد؛ با ورود از اکسل (صدها
/// رویداد در یک بار) این هر بار باز کردن صفحهٔ تاریخچه را کندتر می‌کرد. ترتیب همیشه «تازه‌ترین اول» و
/// برای زمان برابر با شناسه، تا مرز صفحه‌ها پایدار باشد.
/// </summary>
public interface IAuditEntryRepository
{
    /// <summary>
    /// یک صفحه از رویدادهای کارگاه با فیلترهای اختیاری. <paramref name="from"/> و <paramref name="to"/> به UTC
    /// تبدیل می‌شوند (پستگرس برای timestamptz فقط offset صفر می‌پذیرد).
    /// </summary>
    Task<PagedResult<AuditEntry>> PageAsync(
        Guid tenantId,
        AuditAction? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>رویدادهای یک موجودیت (مثلاً تاریخچهٔ یک فرش)، تازه‌ترین اول.</summary>
    Task<IReadOnlyList<AuditEntry>> ListForEntityAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// تازه‌ترین رویدادهای کارگاه برای «فعالیت‌های اخیر» صفحهٔ خانه، به‌جز نوع‌هایی که بیننده نباید ببیند
    /// (مثلاً رویدادهای پولی برای انباردار).
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> ListRecentAsync(
        Guid tenantId,
        int take,
        IReadOnlyCollection<AuditAction> excludedActions,
        CancellationToken cancellationToken = default);
}
