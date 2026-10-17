using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// چیدمان ستون‌های یکی از جدول‌های ورود اطلاعات برای یک کارگاه (کلید اصلی: کارگاه + شناسهٔ جدول).
///
/// نبودِ ردیف یعنی «پیش‌فرض برنامه»؛ ردیف فقط وقتی ساخته می‌شود که تنظیم‌کننده چیدمان را عوض کند.
/// پس پیش‌فرض‌های نسخه‌های بعدی به کارگاه‌هایی که دست نزده‌اند خودبه‌خود می‌رسد. xmin دارد تا دو
/// ذخیرهٔ هم‌زمان از دو مرورگر همدیگر را پاک نکنند (پیکربندی در Infrastructure).
/// </summary>
public class TenantGridLayout : ITenantScoped
{
    /// <summary>بیشترین طول شناسهٔ جدول (varchar(20)).</summary>
    public const int GridIdMaxLength = 20;

    /// <summary>شناسه‌های شناخته‌شدهٔ جدول‌ها.</summary>
    public const string IntakeGridId = "intake";
    public const string BalesGridId = "bales";
    public const string RugsGridId = "rugs";

    public Guid TenantId { get; set; }

    /// <summary>کدام جدول: <see cref="IntakeGridId"/>، <see cref="BalesGridId"/> یا <see cref="RugsGridId"/>.</summary>
    public string GridId { get; set; } = string.Empty;

    /// <summary>
    /// ستون‌ها به ترتیب نمایش (آرایهٔ JSON از ستون‌ها، jsonb). شکل دقیق را سرویس چیدمان جدول تعیین
    /// می‌کند؛ موجودیت فقط آن را نگه می‌دارد.
    /// </summary>
    public string ColumnsJson { get; set; } = "[]";
}
