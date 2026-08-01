using RugsManagment.Application.Abstractions.Services;

namespace RugsManagment.Web.Auth;

/// <summary>
/// هویت کاربر جاری از کوکی احراز هویت.
///
/// این تنها جایی است که لایهٔ وب هویت را به Application می‌رساند؛ خود Application
/// چیزی دربارهٔ HttpContext نمی‌داند.
///
/// اگر درخواستی در کار نباشد (اجرای پس‌زمینه، seed) همهٔ مقادیر null می‌شوند.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal
    {
        get
        {
            var user = accessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true ? user : null;
        }
    }

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? DisplayName => Principal?.GetDisplayName();

    public Guid? TenantId => Principal?.GetTenantId();
}
