namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>یک عکس در گالری فرش، آماده برای نمایش در فرانت.</summary>
public record RugImageDto(
    Guid Id,
    /// <summary>آدرس تصویر با کیفیت کامل</summary>
    string Url,
    /// <summary>آدرس بندانگشتی (اگر جدا نداشته باشد، همان Url)</summary>
    string ThumbnailUrl,
    int Width,
    int Height,
    long SizeBytes,
    int SortOrder,
    bool IsPrimary);

/// <summary>محتوای یک فایل آپلودشده، بعد از اینکه لایهٔ وب آن را از multipart بیرون کشید.</summary>
public record UploadedFile(Stream Content, long Length);

/// <summary>ترتیب جدید عکس‌های یک فرش (به ترتیب دلخواه کاربر).</summary>
public record ReorderRugImagesRequest(IReadOnlyList<Guid> ImageIds);
