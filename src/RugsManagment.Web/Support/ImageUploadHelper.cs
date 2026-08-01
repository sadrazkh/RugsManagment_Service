using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Services;

namespace RugsManagment.Web.Support;

/// <summary>آپلود لوگوی کارگاه — با همان قواعد امنیتیِ عکس فرش.</summary>
public interface IImageUploadHelper
{
    Task SaveTenantLogoAsync(Guid tenantId, IFormFile file, CancellationToken ct = default);
    Task RemoveTenantLogoAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// لوگو در همان محل عکس‌های فرش ذخیره می‌شود، ولی زیر پوشهٔ ثابتی که به‌جای شناسهٔ فرش
/// از یک Guid ثابت استفاده می‌کند — پس همان بررسی مسیرپیمایی و همان سرو کنترل‌شده اعمال می‌شود.
/// </summary>
public sealed class ImageUploadHelper(
    IImageStorage storage,
    ITenantSettingsService settings) : IImageUploadHelper
{
    /// <summary>«شناسهٔ فرشِ» ساختگی برای دارایی‌های خودِ کارگاه (لوگو).</summary>
    private static readonly Guid TenantAssetsFolder = new("00000000-0000-0000-0000-0000000000AA");

    private const long MaxLogoBytes = 2 * 1024 * 1024;

    public async Task SaveTenantLogoAsync(Guid tenantId, IFormFile file, CancellationToken ct = default)
    {
        if (file.Length > MaxLogoBytes)
            throw new InvalidOperationException("حجم لوگو باید کمتر از ۲ مگابایت باشد.");

        await using var content = file.OpenReadStream();

        // نوع فایل از بایت‌های ابتدایی تشخیص داده می‌شود، نه از پسوند یا هدر ارسالی
        var header = new byte[ImageSignature.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var detected = ImageSignature.Detect(header.AsSpan(0, read))
            ?? throw new InvalidOperationException("فایل ارسالی تصویر معتبر (JPEG، PNG یا WebP) نیست.");

        content.Position = 0;

        var current = await settings.GetAsync(tenantId, ct);
        var fileName = await storage.SaveAsync(tenantId, TenantAssetsFolder, content, detected.Extension, ct);
        await settings.SetLogoAsync(tenantId, fileName, ct);

        // فایل قبلی بعد از موفقیت پاک می‌شود تا اگر ذخیره شکست خورد، لوگوی فعلی از دست نرود
        if (ExtractFileName(current.LogoUrl) is { } old)
            await storage.DeleteAsync(tenantId, TenantAssetsFolder, old, ct);
    }

    public async Task RemoveTenantLogoAsync(Guid tenantId, CancellationToken ct = default)
    {
        var current = await settings.GetAsync(tenantId, ct);
        await settings.SetLogoAsync(tenantId, null, ct);

        if (ExtractFileName(current.LogoUrl) is { } old)
            await storage.DeleteAsync(tenantId, TenantAssetsFolder, old, ct);
    }

    /// <summary>پوشهٔ دارایی‌های کارگاه — کنترلر media برای سرو لوگو به آن نیاز دارد.</summary>
    public static Guid AssetsFolder => TenantAssetsFolder;

    private static string? ExtractFileName(string? url)
        => string.IsNullOrEmpty(url) ? null : url[(url.LastIndexOf('/') + 1)..];
}
