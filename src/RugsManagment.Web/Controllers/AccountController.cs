using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Models.Account;
using System.Security.Claims;

namespace RugsManagment.Web.Controllers;

/// <summary>ورود/خروج مبتنی بر کوکی — همان اعتبارسنجی IAuthService، بدون توکن در فرانت.</summary>
[AllowAnonymous]
public class AccountController(IAuthService auth, IUnitOfWork unitOfWork) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // اگر قبلاً وارد شده، به مقصد مناسب نقشش برو
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocalOrHome(returnUrl);

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        AuthResponse result;
        try
        {
            result = await auth.LoginAsync(new LoginRequest(model.Email, model.Password), ct);
            await unitOfWork.SaveChangesAsync(ct); // ذخیرهٔ LastLoginAt
        }
        catch (UnauthorizedAccessException ex)
        {
            model.Error = ex.Message;
            model.Password = string.Empty;
            return View(model);
        }

        var identity = new ClaimsIdentity(result.User.ToClaims(), CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            props);

        return RedirectToLocalOrHome(model.ReturnUrl, result.User.Role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Denied() => View();

    private IActionResult RedirectToLocalOrHome(string? returnUrl, string? role = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        role ??= User.FindFirstValue(ClaimTypes.Role);
        return role == nameof(UserRole.SystemAdmin)
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction("Index", "Dashboard");
    }
}
