using System.Security.Claims;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Api.Extensions;

/// <summary>
/// خواندن اطلاعات کاربر از توکن JWT — بعد از [Authorize] در User در دسترس است.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static UserRole GetUserRole(this ClaimsPrincipal user)
        => Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!);

    public static Guid? GetTenantId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("tenant_id");
        return value is null ? null : Guid.Parse(value);
    }

    /// <summary>
    /// برای APIهای فرش و داشبورد — ادمین سیستم tenant ندارد و این متد خطا می‌دهد.
    /// </summary>
    public static Guid RequireTenantId(this ClaimsPrincipal user)
        => user.GetTenantId() ?? throw new UnauthorizedAccessException("این عملیات فقط برای کاربر کارگاه مجاز است.");
}
