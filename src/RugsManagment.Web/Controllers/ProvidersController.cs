using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// صفحات طرف‌های خدمات (قالیشوی، رفوگر، …) و تسویه‌حساب.
/// مالی است، پس فقط مدیر کارگاه.
/// </summary>
[Authorize(Roles = nameof(UserRole.TenantAdmin))]
public class ProvidersController(IServiceProviderService providers) : Controller
{
    /// <summary>فهرست طرف‌ها همراه مانده‌حساب.</summary>
    public IActionResult Index() => View();

    /// <summary>صورت‌حساب یک طرف: کارهای انجام‌شده و پرداخت‌ها.</summary>
    public async Task<IActionResult> Statement(Guid id, CancellationToken ct)
    {
        var provider = await providers.GetAsync(User.RequireTenantId(), id, ct);
        if (provider is null) return NotFound();

        ViewData["ProviderId"] = id;
        ViewData["ProviderName"] = provider.Name;
        return View();
    }
}
