using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// «ورودی»: فرش‌هایی که یک روز از یک فروشنده رسیدند (spec B.5، A.2) — برای پیش بردن هم‌زمان مسیر و برای
/// هزینه‌هایی که روی کل یک خرید می‌آیند (خرید یکجا، کرایه).
///
/// کلید «ورودی» (کارگاه، روز ایران، فروشندهٔ یکسان‌شده) با ایندکس یکتای جزئی نگه داشته می‌شود: یک ورودی برای هر
/// فروشنده در هر روز؛ دو فروشنده در یک روز دو ورودی‌اند. ورودی‌های پیش از M2 روز (<see cref="ReceivedOn"/>) ندارند و
/// در آن کلید شرکت نمی‌کنند. هم‌زمانی با xmin (پیکربندی در Infrastructure).
/// </summary>
public class RugBatch : BaseEntity, ITenantScoped
{
    /// <summary>طول ستون‌های نام و کلید فروشنده.</summary>
    public const int SellerMaxLength = 200;

    public Guid TenantId { get; set; }

    /// <summary>
    /// شمارهٔ ورودی در کارگاه (شمارندهٔ <c>batch</c>)؛ یکتا. نام پیش‌فرض «ورودی {شماره} · {فروشنده} · {تاریخ}» است.
    /// M2 ورودی‌های قدیمی را به ترتیب ساختشان شماره زد.
    /// </summary>
    public int Number { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }

    /// <summary>نام فروشنده همان‌طور که نوشته شد؛ null یعنی بی‌فروشنده.</summary>
    public string? SellerName { get; set; }

    /// <summary>
    /// نام فروشندهٔ یکسان‌شده (<c>KeyNormalize</c>: حروف فارسی یکسان، ارقام لاتین، فاصله‌های جمع‌شده) برای کلید ورودی؛
    /// رشتهٔ خالی برای «بی‌فروشنده» تا خودش یک کلید باشد.
    /// </summary>
    public string? SellerKey { get; set; }

    /// <summary>روز ایرانِ <see cref="ReceivedAt"/> — بخش روزِ کلید ورودی؛ null برای ورودی‌های قدیمی.</summary>
    public DateOnly? ReceivedOn { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Rug> Rugs { get; set; } = [];
}
