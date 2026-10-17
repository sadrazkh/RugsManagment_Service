using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// کاربر سیستم — یا ادمین کل (بدون Tenant) یا عضو یک کارگاه.
/// </summary>
public class User : BaseEntity
{
    /// <summary>برای SystemAdmin خالی است؛ برای بقیه شناسه کارگاه</summary>
    public Guid? TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>رمز هش‌شده — هرگز متن خام ذخیره نمی‌شود</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// مهر امنیتی نشست. داخل کوکی هم نوشته می‌شود؛ هر بار نقش، رمز یا فعال‌بودن کاربر عوض شود
    /// مقدار تازه می‌گیرد و کوکی‌های قبلی (روی همهٔ دستگاه‌ها) دیگر پذیرفته نمی‌شوند.
    /// </summary>
    public string SecurityStamp { get; set; } = NewSecurityStamp();

    /// <summary>
    /// حالت رابط (ساده/کامل). فقط پوسته را عوض می‌کند، نه دسترسی را. پیش‌فرضش به نقش بستگی دارد
    /// (<see cref="ApplyRoleDefaultPreferences"/>)؛ مقدار اولیهٔ این‌جا فقط برای شیء تازه است.
    /// </summary>
    public UiMode UiMode { get; set; } = UiMode.Simple;

    /// <summary>اندازهٔ نوشته (۱۶ / ۱۸ / ۲۰ پیکسل) — خودِ کاربر از منوی کاربر یا راهنما عوضش می‌کند.</summary>
    public TextScale TextScale { get; set; } = TextScale.Normal;

    /// <summary>Enter در جدول‌های ورود اطلاعات به کدام سو برود.</summary>
    public GridEnterDirection GridEnter { get; set; } = GridEnterDirection.Down;

    /// <summary>
    /// راهنماهای شروع (coach mark) که کاربر بسته است — آرایهٔ JSON از کلیدها (jsonb)؛ null یعنی هیچ‌کدام.
    /// برای هر کاربر جداست تا بستن راهنما روی دستگاه پسر، راهنمای پدر را نبندد.
    /// </summary>
    public string? CoachDismissedJson { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>مهر تازه — هر جا نقش/رمز/وضعیت کاربر عوض می‌شود باید صدا زده شود.</summary>
    public void RotateSecurityStamp() => SecurityStamp = NewSecurityStamp();

    /// <summary>
    /// پیش‌فرض‌های نقش را روی حالت رابط و اندازهٔ نوشته می‌گذارد: بازرگان «ساده + بزرگ»، انباردار
    /// «ساده + معمولی»، تنظیم‌کننده و مدیر سامانه «کامل + معمولی». جهت Enter دست نمی‌خورد.
    ///
    /// برای کاربر تازه و برای تغییر نقش صدا زده می‌شود؛ بازرگانی که پدرِ کارگاه است با نوشتهٔ بزرگ
    /// شروع می‌کند بی‌آنکه پسر جایی را تنظیم کند. ترجیحی که خودِ کاربر بعداً عوض کند سر جایش می‌ماند
    /// چون این متد فقط در همان دو لحظه صدا زده می‌شود.
    /// </summary>
    public void ApplyRoleDefaultPreferences()
    {
        (UiMode, TextScale) = Role switch
        {
            UserRole.Merchant => (UiMode.Simple, TextScale.Large),
            UserRole.Operator => (UiMode.Simple, TextScale.Normal),
            _ => (UiMode.Full, TextScale.Normal)
        };
    }

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
