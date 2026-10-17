using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Auth;

/// <summary>
/// خواندن/ساختن claimهای کاربر برای احراز هویت کوکی.
/// همان کلیدهایی که بک‌اند انتظار دارد: NameIdentifier / Role / tenant_id.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public const string TenantIdClaim = "tenant_id";
    public const string TenantNameClaim = "tenant_name";

    /// <summary>
    /// مهر امنیتی کاربر در لحظهٔ ورود. <see cref="SecurityStampValidator"/> آن را با مقدار دیتابیس
    /// مقایسه می‌کند؛ هر تغییر نقش/رمز/وضعیت مهر را عوض می‌کند و کوکی‌های قدیمی رد می‌شوند.
    /// </summary>
    public const string SecurityStampClaim = "sstamp";

    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static UserRole GetUserRole(this ClaimsPrincipal user)
        => Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!);

    public static Guid? GetTenantId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(TenantIdClaim);
        return value is null ? null : Guid.Parse(value);
    }

    /// <summary>برای صفحات کارگاه — ادمین سیستم Tenant ندارد و اینجا خطا می‌گیرد.</summary>
    public static Guid RequireTenantId(this ClaimsPrincipal user)
        => user.GetTenantId() ?? throw new UnauthorizedAccessException("این عملیات فقط برای کاربر کارگاه مجاز است.");

    public static string? GetTenantName(this ClaimsPrincipal user)
        => user.FindFirstValue(TenantNameClaim);

    public static string GetDisplayName(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.Email) ?? "کاربر";

    /// <summary>
    /// ساخت claimها از نتیجهٔ ورود برای نشستن در کوکی.
    /// مهر امنیتی جدا از <see cref="UserDto"/> می‌آید چون UserDto به JSON می‌رود و مهر نباید آنجا باشد.
    /// </summary>
    public static IEnumerable<Claim> ToClaims(this UserDto user, string securityStamp)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());
        yield return new Claim(ClaimTypes.Name, user.FullName);
        yield return new Claim(ClaimTypes.Email, user.Email);
        yield return new Claim(ClaimTypes.Role, user.Role);
        yield return new Claim(SecurityStampClaim, securityStamp);
        if (user.TenantId is Guid tenantId)
            yield return new Claim(TenantIdClaim, tenantId.ToString());
        if (!string.IsNullOrEmpty(user.TenantName))
            yield return new Claim(TenantNameClaim, user.TenantName);
    }
}

/// <summary>صدور کوکی ورود — یک‌جا، تا ورود و بازنویسی کوکی (بعد از تغییر رمز/نام) یکسان بمانند.</summary>
public static class CookieSignIn
{
    /// <summary>طول نشست؛ با SlidingExpiration در Program.cs تمدید می‌شود.</summary>
    private static readonly TimeSpan SessionLength = TimeSpan.FromHours(8);

    public static Task SignInUserAsync(this HttpContext http, AuthResult session, bool isPersistent)
    {
        var identity = new ClaimsIdentity(
            session.User.ToClaims(session.SecurityStamp), CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(SessionLength)
            });
    }

    /// <summary>
    /// بازنویسی کوکی همین درخواست با نشست تازه (نام یا مهر جدید)، با حفظ «مرا به خاطر بسپار».
    /// بدون حفظ آن، کوکی ماندگار بعد از تغییر رمز بی‌صدا به کوکی نشست تبدیل می‌شد.
    /// </summary>
    public static async Task ReissueAsync(this HttpContext http, AuthResult session)
    {
        var current = await http.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInUserAsync(session, current.Properties?.IsPersistent ?? false);
    }
}
