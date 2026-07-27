using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک پرداخت به طرف خدمات (تسویه‌حساب).
///
/// مانده‌حساب هرگز ذخیره نمی‌شود؛ همیشه از «مجموع هزینهٔ مراحل تکمیل‌شدهٔ آن طرف»
/// منهای «مجموع پرداخت‌ها» محاسبه می‌شود تا هیچ‌وقت با واقعیت اختلاف پیدا نکند.
/// </summary>
public class ProviderPayment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ServiceProviderId { get; set; }

    /// <summary>مبلغ پرداختی (تومان) — همیشه مثبت</summary>
    public decimal Amount { get; set; }

    /// <summary>زمان پرداخت (UTC) — ممکن است با زمان ثبت فرق کند</summary>
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>شمارهٔ فیش، چک یا ارجاع بانکی</summary>
    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public ServiceProvider ServiceProvider { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
