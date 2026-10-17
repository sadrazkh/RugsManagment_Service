using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک رویداد ثبت‌شده در تاریخچهٔ فعالیت کارگاه — «چه کسی، کِی، چه کرد».
///
/// نام کاربر عمداً به‌صورت متن کپی می‌شود (نه فقط کلید خارجی): اگر کاربری بعداً
/// حذف یا تغییر نام داد، تاریخچه نباید بی‌معنا شود. لاگ فقط افزودنی است و ویرایش نمی‌شود.
/// </summary>
public class AuditEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>ممکن است null باشد (عملیات سیستمی مثل seed)</summary>
    public Guid? UserId { get; set; }

    /// <summary>نام کاربر در لحظهٔ رویداد</summary>
    public string UserName { get; set; } = "سیستم";

    public AuditAction Action { get; set; }

    /// <summary>نوع موجودیت: Rug، RugSale، User، Tenant، ServiceProvider…</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>عنوان خواندنی موجودیت — مثلاً SKU یا نام، تا در فهرست بدون join دیده شود</summary>
    public string? EntityLabel { get; set; }

    /// <summary>توضیح فارسی آنچه اتفاق افتاد</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// جزئیات ساخت‌یافتهٔ رویداد (jsonb، اختیاری) — مثلاً کدام ترجیح از چه به چه عوض شد، یا شمارهٔ
    /// محموله و تعداد تخته‌ها. خلاصهٔ فارسی برای خواندن است و این برای فیلتر، «برگرداندن» و بررسی؛
    /// هیچ‌وقت مبلغ تومانی در آن نوشته نمی‌شود تا تاریخچه برای انباردار هم امن بماند.
    /// </summary>
    public string? DetailsJson { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
