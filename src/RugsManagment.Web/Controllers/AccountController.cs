using Microsoft.AspNetCore.RateLimiting;
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

/// <summary>
/// ورود/خروج مبتنی بر کوکی و حساب کاربری.
///
/// پیش‌فرض کلاس «نیازمند ورود» است و فقط صفحات عمومی (ورود و منع دسترسی) با
/// AllowAnonymous باز می‌شوند — عکسِ این ترتیب باعث می‌شد صفحهٔ پروفایل هم عمومی شود.
/// </summary>
[Authorize]
public class AccountController(
    IAuthService auth,
    RugsManagment.Application.Services.IProfileService profile,
    IUnitOfWork unitOfWork) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // اگر قبلاً وارد شده، به مقصد مناسب نقشش برو
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocalOrHome(returnUrl);

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>محدودیت نرخ: ۱۰ تلاش در ۵ دقیقه از هر آی‌پی — دفاع در برابر حدس رمز.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
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

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Denied() => View();

    // ── حساب کاربری خودِ کاربر ─────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken ct)
        => View(await BuildProfileAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken ct)
    {
        var userId = User.GetUserId();

        // اعتبارسنجی رمز فقط وقتی کاربر واقعاً قصد تغییر رمز دارد
        var wantsPasswordChange = !string.IsNullOrWhiteSpace(model.NewPassword)
                                  || !string.IsNullOrWhiteSpace(model.CurrentPassword);

        if (!ModelState.IsValid)
            return View(await RefillAsync(model, ct));

        try
        {
            await profile.UpdateNameAsync(userId, model.FullName, ct);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                    throw new InvalidOperationException("برای تغییر رمز، رمز فعلی را وارد کنید.");
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                    throw new InvalidOperationException("رمز جدید را وارد کنید.");

                await profile.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword, ct);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
        {
            model.Error = ex.Message;
            return View(await RefillAsync(model, ct));
        }

        // نام نمایشی داخل کوکی است؛ بدون تازه‌سازی، هدر نام قدیمی را نشان می‌دهد
        var refreshed = await auth.RefreshAsync(userId, ct);
        var identity = new ClaimsIdentity(refreshed.User.ToClaims(), CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        TempData["Toast"] = wantsPasswordChange ? "حساب و رمز عبور به‌روزرسانی شد." : "حساب به‌روزرسانی شد.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<ProfileViewModel> BuildProfileAsync(CancellationToken ct)
    {
        var user = await profile.GetAsync(User.GetUserId(), ct);
        return new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            RoleLabel = RoleLabel(user.Role),
            TenantName = user.TenantName
        };
    }

    /// <summary>مقادیر فقط‌خواندنی را بعد از خطای اعتبارسنجی دوباره پر می‌کند.</summary>
    private async Task<ProfileViewModel> RefillAsync(ProfileViewModel model, CancellationToken ct)
    {
        var current = await BuildProfileAsync(ct);
        model.Email = current.Email;
        model.RoleLabel = current.RoleLabel;
        model.TenantName = current.TenantName;
        model.CurrentPassword = model.NewPassword = model.ConfirmPassword = null;
        return model;
    }

    private static string RoleLabel(string role) => role switch
    {
        nameof(UserRole.SystemAdmin) => "مدیر سامانه",
        nameof(UserRole.TenantAdmin) => "مدیر کارگاه",
        nameof(UserRole.Operator) => "اپراتور",
        _ => role
    };

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
