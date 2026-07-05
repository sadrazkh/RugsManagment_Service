using System.Security.Claims;
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

    /// <summary>ساخت claimها از نتیجهٔ ورود برای نشستن در کوکی.</summary>
    public static IEnumerable<Claim> ToClaims(this UserDto user)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());
        yield return new Claim(ClaimTypes.Name, user.FullName);
        yield return new Claim(ClaimTypes.Email, user.Email);
        yield return new Claim(ClaimTypes.Role, user.Role);
        if (user.TenantId is Guid tenantId)
            yield return new Claim(TenantIdClaim, tenantId.ToString());
        if (!string.IsNullOrEmpty(user.TenantName))
            yield return new Claim(TenantNameClaim, user.TenantName);
    }
}
