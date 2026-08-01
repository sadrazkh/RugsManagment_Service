using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Services;

/// <summary>مدیریت گالری عکس هر فرش — آپلود، حذف، انتخاب عکس شاخص و ترتیب.</summary>
public interface IRugImageService
{
    Task<IReadOnlyList<RugImageDto>> ListAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);

    Task<RugImageDto> AddAsync(
        Guid tenantId, Guid rugId, UploadedFile full, UploadedFile? thumbnail,
        int width, int height, CancellationToken ct = default);

    Task DeleteAsync(Guid tenantId, Guid rugId, Guid imageId, CancellationToken ct = default);
    Task<IReadOnlyList<RugImageDto>> SetPrimaryAsync(Guid tenantId, Guid rugId, Guid imageId, CancellationToken ct = default);
    Task<IReadOnlyList<RugImageDto>> ReorderAsync(Guid tenantId, Guid rugId, IReadOnlyList<Guid> imageIds, CancellationToken ct = default);
}

public sealed class RugImageService(
    IRepository<RugImage> images,
    IRugRepository rugs,
    IImageStorage storage,
    IUnitOfWork unitOfWork) : IRugImageService
{
    /// <summary>سقف اندازهٔ هر فایل. فرانت قبل از آپلود تصویر را کوچک می‌کند، پس این فقط سقف ایمنی است.</summary>
    public const long MaxFileSizeBytes = 8 * 1024 * 1024;

    /// <summary>سقف تعداد عکس هر فرش — جلوگیری از پر شدن دیسک.</summary>
    public const int MaxImagesPerRug = 12;

    public async Task<IReadOnlyList<RugImageDto>> ListAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
        => (await LoadAsync(tenantId, rugId, ct)).Select(ToDto).ToList();

    public async Task<RugImageDto> AddAsync(
        Guid tenantId, Guid rugId, UploadedFile full, UploadedFile? thumbnail,
        int width, int height, CancellationToken ct = default)
    {
        await EnsureRugExistsAsync(tenantId, rugId, ct);

        var existing = await LoadAsync(tenantId, rugId, ct);
        if (existing.Count >= MaxImagesPerRug)
            throw new InvalidOperationException(
                $"هر فرش حداکثر {PersianText.ToPersianDigits(MaxImagesPerRug.ToString())} عکس می‌تواند داشته باشد.");

        var detected = await ValidateAsync(full, ct);
        var fileName = await storage.SaveAsync(tenantId, rugId, full.Content, detected.Extension, ct);

        string? thumbName = null;
        if (thumbnail is not null)
        {
            var thumbType = await ValidateAsync(thumbnail, ct);
            thumbName = await storage.SaveAsync(tenantId, rugId, thumbnail.Content, thumbType.Extension, ct);
        }

        var image = new RugImage
        {
            TenantId = tenantId,
            RugId = rugId,
            FileName = fileName,
            ThumbnailFileName = thumbName,
            ContentType = detected.ContentType,
            SizeBytes = full.Length,
            Width = width > 0 ? width : 0,
            Height = height > 0 ? height : 0,
            SortOrder = existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder) + 1,
            // اولین عکس خودکار شاخص می‌شود تا فرش همیشه تصویری برای نمایش داشته باشد
            IsPrimary = existing.Count == 0
        };

        await images.AddAsync(image, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(image);
    }

    public async Task DeleteAsync(Guid tenantId, Guid rugId, Guid imageId, CancellationToken ct = default)
    {
        var all = await LoadAsync(tenantId, rugId, ct);
        var image = all.FirstOrDefault(i => i.Id == imageId)
            ?? throw new KeyNotFoundException("عکس یافت نشد.");

        images.Remove(image);

        // اگر عکس شاخص حذف شد، عکس بعدی جایش را می‌گیرد تا فرش بی‌تصویر نماند
        if (image.IsPrimary)
        {
            var next = all.Where(i => i.Id != imageId).OrderBy(i => i.SortOrder).FirstOrDefault();
            if (next is not null)
            {
                next.IsPrimary = true;
                images.Update(next);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        // فایل‌ها بعد از موفقیت دیتابیس پاک می‌شوند: فایلِ یتیم بهتر از رکوردِ بدون فایل است
        await storage.DeleteAsync(tenantId, rugId, image.FileName, ct);
        if (!string.IsNullOrEmpty(image.ThumbnailFileName))
            await storage.DeleteAsync(tenantId, rugId, image.ThumbnailFileName, ct);
    }

    public async Task<IReadOnlyList<RugImageDto>> SetPrimaryAsync(
        Guid tenantId, Guid rugId, Guid imageId, CancellationToken ct = default)
    {
        var all = await LoadAsync(tenantId, rugId, ct);
        if (all.All(i => i.Id != imageId))
            throw new KeyNotFoundException("عکس یافت نشد.");

        foreach (var image in all)
        {
            var shouldBePrimary = image.Id == imageId;
            if (image.IsPrimary == shouldBePrimary) continue;

            image.IsPrimary = shouldBePrimary;
            images.Update(image);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return all.OrderBy(i => i.SortOrder).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<RugImageDto>> ReorderAsync(
        Guid tenantId, Guid rugId, IReadOnlyList<Guid> imageIds, CancellationToken ct = default)
    {
        var all = await LoadAsync(tenantId, rugId, ct);

        for (var index = 0; index < imageIds.Count; index++)
        {
            var image = all.FirstOrDefault(i => i.Id == imageIds[index]);
            if (image is null || image.SortOrder == index) continue;

            image.SortOrder = index;
            images.Update(image);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return all.OrderBy(i => i.SortOrder).Select(ToDto).ToList();
    }

    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// اعتبارسنجی فایل: اندازه و امضای بایتی. Content-Type ارسالی کاربر عمداً نادیده گرفته می‌شود.
    /// موقعیت استریم بعد از خواندن هدر به ابتدا برمی‌گردد تا ذخیره کامل انجام شود.
    /// </summary>
    private static async Task<ImageSignature.Detected> ValidateAsync(UploadedFile file, CancellationToken ct)
    {
        if (file.Length <= 0)
            throw new InvalidOperationException("فایل خالی است.");

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"حجم فایل بیشتر از {PersianText.ToPersianDigits((MaxFileSizeBytes / (1024 * 1024)).ToString())} مگابایت است.");

        var header = new byte[ImageSignature.HeaderLength];
        var read = await file.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        var detected = ImageSignature.Detect(header.AsSpan(0, read))
            ?? throw new InvalidOperationException("فایل ارسالی تصویر معتبر (JPEG، PNG یا WebP) نیست.");

        file.Content.Position = 0;
        return detected;
    }

    private async Task<List<RugImage>> LoadAsync(Guid tenantId, Guid rugId, CancellationToken ct)
    {
        var list = await images.ListAsync(i => i.TenantId == tenantId && i.RugId == rugId, ct);
        return list.OrderBy(i => i.SortOrder).ToList();
    }

    private async Task EnsureRugExistsAsync(Guid tenantId, Guid rugId, CancellationToken ct)
    {
        _ = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
    }

    /// <summary>نگاشت مشترک با EntityMappers تا آدرس‌ها فقط یک جا تعریف شوند.</summary>
    private static RugImageDto ToDto(RugImage image) => Mapping.EntityMappers.ToDto(image);
}
