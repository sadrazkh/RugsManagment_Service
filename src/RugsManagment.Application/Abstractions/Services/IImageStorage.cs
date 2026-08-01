namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// ذخیره‌سازی فایل تصویر. پیاده‌سازی پیش‌فرض روی دیسک است، ولی این انتزاع اجازه می‌دهد
/// بعداً به S3/MinIO مهاجرت شود بدون تغییر لایهٔ Application.
///
/// نام فایل همیشه توسط پیاده‌سازی تولید می‌شود، نه توسط ورودی کاربر.
/// </summary>
public interface IImageStorage
{
    /// <summary>
    /// محتوا را ذخیره و نام فایلِ تولیدشده را برمی‌گرداند.
    /// </summary>
    /// <param name="tenantId">برای جداسازی فیزیکی فایل‌های هر کارگاه</param>
    /// <param name="rugId">پوشهٔ فرش</param>
    /// <param name="content">محتوای فایل (قبلاً اعتبارسنجی شده)</param>
    /// <param name="extension">پسوند بدون نقطه، مثلاً webp</param>
    Task<string> SaveAsync(Guid tenantId, Guid rugId, Stream content, string extension, CancellationToken ct = default);

    /// <summary>حذف یک فایل. نبودِ فایل خطا نیست (حذف باید idempotent باشد).</summary>
    Task DeleteAsync(Guid tenantId, Guid rugId, string fileName, CancellationToken ct = default);

    /// <summary>حذف کل پوشهٔ یک فرش — هنگام حذف فرش.</summary>
    Task DeleteRugFolderAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);

    /// <summary>
    /// باز کردن فایل برای خواندن؛ null اگر وجود نداشته باشد.
    /// </summary>
    Task<Stream?> OpenReadAsync(Guid tenantId, Guid rugId, string fileName, CancellationToken ct = default);
}
