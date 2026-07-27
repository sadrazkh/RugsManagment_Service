using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک عکس از یک فرش. فایل روی دیسک (یا volume ماندگار) ذخیره می‌شود و اینجا فقط
/// فراداده نگه‌داری می‌شود.
///
/// نام فایل‌ها را همیشه خودِ سرور تولید می‌کند (Guid + پسوند)، نه نام ارسالی کاربر،
/// تا مسیرپیمایی (path traversal) و بازنویسی فایل ممکن نباشد.
/// </summary>
public class RugImage : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RugId { get; set; }

    /// <summary>نام فایل تصویر با کیفیت کامل (بدون مسیر)</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>نام فایل بندانگشتی برای فهرست‌ها؛ اگر خالی باشد از FileName استفاده می‌شود</summary>
    public string? ThumbnailFileName { get; set; }

    public string ContentType { get; set; } = "image/webp";
    public long SizeBytes { get; set; }

    /// <summary>ابعاد پیکسلی — برای رزرو فضا در چیدمان و جلوگیری از پرش صفحه (CLS)</summary>
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>ترتیب نمایش در گالری</summary>
    public int SortOrder { get; set; }

    /// <summary>عکس شاخص که در فهرست و برچسب نشان داده می‌شود؛ هر فرش حداکثر یکی دارد</summary>
    public bool IsPrimary { get; set; }

    public Rug Rug { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
