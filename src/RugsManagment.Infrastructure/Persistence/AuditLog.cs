using Microsoft.Extensions.Logging;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence;

/// <summary>
/// ثبت رویداد در جدول AuditEntries.
///
/// رکورد فقط به DbContext اضافه می‌شود و با همان SaveChanges عملیات اصلی ذخیره
/// می‌گردد — پس لاگ و خودِ تغییر یا هر دو ثبت می‌شوند یا هیچ‌کدام.
///
/// اگر کاربر جاری کارگاه نداشته باشد (ادمین سیستم یا اجرای پس‌زمینه) رویداد
/// نادیده گرفته می‌شود، چون تاریخچهٔ فعالیت متعلق به یک کارگاه است.
/// </summary>
public sealed class AuditLog(
    AppDbContext db,
    ICurrentUser currentUser,
    ILogger<AuditLog> logger) : IAuditLog
{
    public void Record(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string summary,
        string? entityLabel = null)
    {
        try
        {
            if (currentUser.TenantId is not Guid tenantId) return;

            db.AuditEntries.Add(new AuditEntry
            {
                TenantId = tenantId,
                UserId = currentUser.UserId,
                UserName = currentUser.DisplayName ?? "سیستم",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityLabel = Truncate(entityLabel, 200),
                Summary = Truncate(summary, 500) ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            // حسابرسی نباید عملیات اصلی را بشکند
            logger.LogWarning(ex, "ثبت رویداد حسابرسی ناموفق بود: {Action} {EntityType}", action, entityType);
        }
    }

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
