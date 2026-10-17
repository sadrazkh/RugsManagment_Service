using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
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
    /// <summary>
    /// جزئیات بزرگ‌تر از این (نویسه) نوشته نمی‌شود: تاریخچه جای نگه داشتن داده نیست و یک
    /// فهرست بزرگ (مثلاً شناسهٔ هزار فرش) جدول را بی‌جهت سنگین می‌کرد.
    /// </summary>
    internal const int MaxDetailsLength = 8_000;

    /// <summary>
    /// camelCase و enum به نام (نه عدد) تا جزئیات بدون جدول ترجمه خوانا باشد؛ فارسی escape نمی‌شود تا در
    /// پستگرس هم خوانا بماند؛ عددها با فرهنگ Invariant (System.Text.Json همیشه Invariant است).
    /// </summary>
    internal static readonly JsonSerializerOptions DetailsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };

    public void Record(
        AuditAction action,
        string entityType,
        Guid? entityId,
        string summary,
        string? entityLabel = null,
        object? details = null)
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
                Summary = Truncate(summary, 500) ?? string.Empty,
                DetailsJson = SerializeDetails(details, action, entityType)
            });
        }
        catch (Exception ex)
        {
            // حسابرسی نباید عملیات اصلی را بشکند
            logger.LogWarning(ex, "ثبت رویداد حسابرسی ناموفق بود: {Action} {EntityType}", action, entityType);
        }
    }

    /// <summary>
    /// جزئیات به JSON؛ رشته‌ای که خودش JSON است همان‌طور نوشته می‌شود (اگر معتبر باشد). خطای سریال‌سازی
    /// یا جزئیات خیلی بزرگ فقط جزئیات را حذف می‌کند، نه خود رویداد را.
    /// </summary>
    private string? SerializeDetails(object? details, AuditAction action, string entityType)
    {
        if (details is null) return null;

        try
        {
            string json;
            if (details is string text)
            {
                using var parsed = JsonDocument.Parse(text); // فقط اعتبارسنجی؛ jsonb متن نامعتبر را رد می‌کرد
                json = text;
            }
            else
            {
                json = JsonSerializer.Serialize(details, details.GetType(), DetailsJsonOptions);
            }

            if (json.Length <= MaxDetailsLength) return json;

            logger.LogWarning("جزئیات رویداد {Action} {EntityType} بزرگ بود ({Length} نویسه) و ثبت نشد.",
                action, entityType, json.Length);
            return null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(ex, "جزئیات رویداد {Action} {EntityType} به JSON تبدیل نشد.", action, entityType);
            return null;
        }
    }

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
