using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// سرو کردن فایل تصویر فرش‌ها.
///
/// چرا از static files استفاده نمی‌کنیم: فایل‌ها عمداً بیرون از wwwroot ذخیره می‌شوند تا
/// هیچ‌کس بدون احراز هویت و بدون بررسی مالکیتِ کارگاه به عکس‌های یک مشتری دیگر نرسد.
/// شناسهٔ کارگاه از کوکی کاربر می‌آید، نه از آدرس — پس دستکاری آدرس بی‌فایده است.
/// </summary>
[Authorize(Roles = $"{nameof(Domain.Enums.UserRole.TenantAdmin)},{nameof(Domain.Enums.UserRole.Operator)}")]
[Route("media/rugs")]
public class MediaController(IRepository<RugImage> images, IImageStorage storage) : Controller
{
    /// <summary>مدت کش مرورگر. نام فایل‌ها Guid و تغییرناپذیرند، پس کش طولانی امن است.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(30);

    [HttpGet("{rugId:guid}/{fileName}")]
    public async Task<IActionResult> Get(Guid rugId, string fileName, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();

        // رکورد باید متعلق به همین کارگاه و همین فرش باشد؛ در غیر این صورت ۴۰۴
        // (عمداً ۴۰۴ و نه ۴۰۳، تا وجود یا نبود فایل کارگاه دیگر لو نرود)
        var match = await images.ListAsync(
            i => i.TenantId == tenantId
                 && i.RugId == rugId
                 && (i.FileName == fileName || i.ThumbnailFileName == fileName),
            ct);

        var image = match.FirstOrDefault();
        if (image is null) return NotFound();

        var stream = await storage.OpenReadAsync(tenantId, rugId, fileName, ct);
        if (stream is null) return NotFound();

        // کش خصوصی: پاسخ به کاربرِ همین کارگاه اختصاص دارد و نباید در پراکسی مشترک ذخیره شود
        Response.Headers.CacheControl = $"private, max-age={(int)CacheDuration.TotalSeconds}, immutable";

        return File(stream, image.ContentType);
    }
}
