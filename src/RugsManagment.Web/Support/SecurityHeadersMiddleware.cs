using System.Security.Cryptography;

namespace RugsManagment.Web.Support;

/// <summary>
/// هدرهای امنیتی و Content-Security-Policy.
///
/// CSP با nonce کار می‌کند نه 'unsafe-inline' برای اسکریپت: تنها اسکریپت درون‌خطی سامانه
/// (تعیین تم قبل از رنگ‌آمیزی صفحه) nonce می‌گیرد و بقیه بلاک می‌شوند — یعنی اگر جایی
/// XSS رخ دهد، اسکریپت تزریق‌شده اجرا نمی‌شود.
///
/// برای style ناچار به 'unsafe-inline' هستیم چون Vue برای :style پویا (مثل عرض نوار پیشرفت)
/// صفت style تولید می‌کند. این پذیرفتنی است: تزریق style به‌تنهایی اجرای کد نیست.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>کلید nonce در HttpContext.Items — layout از همین می‌خواند.</summary>
    public const string NonceKey = "csp-nonce";

    /// <summary>فونت فارسی از این CDN می‌آید (هم CSS و هم فایل فونت).</summary>
    private const string FontCdn = "https://cdn.jsdelivr.net";

    public async Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceKey] = nonce;

        var headers = context.Response.Headers;

        // جلوگیری از حدس نوع محتوا — فایل آپلودی نباید به‌عنوان HTML اجرا شود
        headers["X-Content-Type-Options"] = "nosniff";
        // اطلاعات آدرس به سایت دیگر نشت نکند
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        // این اپ به دوربین نیاز دارد (اسکن برچسب)؛ بقیهٔ دسترسی‌ها بسته
        headers["Permissions-Policy"] = "camera=(self), geolocation=(), microphone=(), payment=()";
        headers["X-Frame-Options"] = "DENY";

        headers["Content-Security-Policy"] = string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}'",
            $"style-src 'self' {FontCdn} 'unsafe-inline'",
            $"font-src 'self' {FontCdn} data:",
            // تصاویر: خودمان + data/blob برای QR و بارکدِ تولیدشده در مرورگر
            "img-src 'self' data: blob:",
            "connect-src 'self'",
            "media-src 'self' blob:",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            // جایگزین مدرن X-Frame-Options
            "frame-ancestors 'none'");

        await next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();

    /// <summary>nonce درخواست جاری — برای تگ script درون‌خطی در layout.</summary>
    public static string? CspNonce(this HttpContext context)
        => context.Items[SecurityHeadersMiddleware.NonceKey] as string;
}
