namespace RugsManagment.Application.Common;

/// <summary>
/// تشخیص نوع تصویر از روی بایت‌های ابتدای فایل.
///
/// چرا از Content-Type یا پسوند استفاده نمی‌کنیم: هر دو کاملاً تحت کنترل فرستنده‌اند.
/// کسی می‌تواند یک اسکریپت را با نام <c>x.webp</c> و هدر <c>image/webp</c> بفرستد.
/// امضای بایتی تنها چیزی است که واقعاً محتوا را توصیف می‌کند.
/// </summary>
public static class ImageSignature
{
    /// <summary>حداقل بایت لازم برای تشخیص (WebP بلندترین الگو را دارد).</summary>
    public const int HeaderLength = 12;

    public sealed record Detected(string ContentType, string Extension);

    /// <summary>
    /// نوع تصویر را تشخیص می‌دهد؛ null یعنی «تصویر شناخته‌شده‌ای نیست» و باید رد شود.
    /// </summary>
    public static Detected? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4) return null;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return new Detected("image/png", "png");
        }

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new Detected("image/jpeg", "jpg");
        }

        // WebP: "RIFF" .... "WEBP"
        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
        {
            return new Detected("image/webp", "webp");
        }

        return null;
    }
}
