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
    void Record(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string summary,
        string? entityLabel = null);
}
