using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// ثبت رویداد در تاریخچهٔ فعالیت.
///
/// قرارداد مهم: ثبت لاگ هرگز نباید عملیات اصلی را بشکند. اگر نوشتن لاگ خطا داد،
/// پیاده‌سازی باید آن را ببلعد و لاگ کند — کاربر نباید به‌خاطر خطای حسابرسی،
/// ثبت فروش یا پیشبرد مرحله‌اش را از دست بدهد.
///
/// رکورد در همان UnitOfWork عملیات اصلی ذخیره می‌شود، پس اگر تراکنش برگردد
/// لاگِ رویدادی که رخ نداده هم باقی نمی‌ماند.
/// </summary>
public interface IAuditLog
{
    /// <param name="action">نوع رویداد</param>
    /// <param name="entityType">نوع موجودیت (Rug، User، TenantPreferences، …)</param>
    /// <param name="entityId">شناسهٔ موجودیت، اگر یکی است</param>
    /// <param name="summary">خلاصهٔ فارسی برای خواندن</param>
    /// <param name="entityLabel">عنوان خواندنی موجودیت</param>
    /// <param name="details">
    /// جزئیات ساخت‌یافته (اختیاری) که به‌صورت JSON در <c>AuditEntry.DetailsJson</c> می‌نشیند — مثلاً
    /// <c>{ changes: { baleTerm: { from: "Negleh", to: "Adl" } } }</c>. هرگز مبلغ تومانی در آن نگذارید:
    /// تاریخچه برای همهٔ نقش‌ها خواندنی است و پنهان‌سازی پول به آن نمی‌رسد.
    /// </param>
    void Record(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string summary,
        string? entityLabel = null,
        object? details = null);
}
