using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>گالری عکس فرش — آپلود، حذف، عکس شاخص و ترتیب.</summary>
[Route("api/rugs/{rugId:guid}/images")]
public class RugImagesApiController(IRugImageService images) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid rugId, CancellationToken ct)
        => Ok(await images.ListAsync(User.RequireTenantId(), rugId, ct));

    /// <summary>
    /// آپلود یک عکس. فرانت تصویر را قبل از ارسال کوچک و به WebP تبدیل می‌کند و
    /// نسخهٔ بندانگشتی را هم می‌فرستد؛ اگر نفرستد، همان تصویر اصلی استفاده می‌شود.
    /// نوع فایل از روی بایت‌های ابتدایی بررسی می‌شود، نه از هدر یا پسوند.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        Guid rugId,
        IFormFile file,
        IFormFile? thumbnail,
        [FromForm] int width,
        [FromForm] int height,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "فایلی ارسال نشده است." });

        await using var content = file.OpenReadStream();
        await using var thumbContent = thumbnail?.OpenReadStream();

        var dto = await images.AddAsync(
            User.RequireTenantId(),
            rugId,
            new UploadedFile(content, file.Length),
            thumbContent is null ? null : new UploadedFile(thumbContent, thumbnail!.Length),
            width,
            height,
            ct);

        return Ok(dto);
    }

    [HttpDelete("{imageId:guid}")]
    public async Task<IActionResult> Delete(Guid rugId, Guid imageId, CancellationToken ct)
    {
        await images.DeleteAsync(User.RequireTenantId(), rugId, imageId, ct);
        return NoContent();
    }

    [HttpPost("{imageId:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid rugId, Guid imageId, CancellationToken ct)
        => Ok(await images.SetPrimaryAsync(User.RequireTenantId(), rugId, imageId, ct));

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(
        Guid rugId, [FromBody] ReorderRugImagesRequest request, CancellationToken ct)
        => Ok(await images.ReorderAsync(User.RequireTenantId(), rugId, request.ImageIds, ct));
}
