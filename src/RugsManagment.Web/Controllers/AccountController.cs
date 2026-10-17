using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Models.Account;
using RugsManagment.Web.Support;
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
    IUnitOfWork unitOfWork,
    IMemoryCache cache) : Controller
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

        AuthResult result;
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

        // مهر امنیتی (sstamp) هم داخل کوکی می‌نشیند تا SecurityStampValidator بتواند نشست را باطل کند
        await HttpContext.SignInUserAsync(result, model.RememberMe);

        // ورود همین حالا وضعیت تازه را از دیتابیس خواند؛ وضعیت کش‌شدهٔ قبلی (مثلاً «کارگاه غیرفعال» از پیش
        // از فعال‌سازی دوباره، یا مهری که روی نمونهٔ دیگری چرخیده) نباید کوکیِ تازه را رد کند یا کوکی قدیمی را بپذیرد.
        SecurityStampValidator.Evict(cache, result.User.Id);

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

        UserDto updated;
        string? newStamp = null;
        try
        {
            updated = await profile.UpdateNameAsync(userId, model.FullName, ct);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                    throw new InvalidOperationException("برای تغییر رمز، رمز فعلی را وارد کنید.");
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                    throw new InvalidOperationException("رمز جدید را وارد کنید.");

                newStamp = await profile.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword, ct);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
        {
            model.Error = ex.Message;
            return View(await RefillAsync(model, ct));
        }

        // نام نمایشی داخل کوکی است، پس کوکی همین دستگاه بازنویسی می‌شود — ولی مهرش فقط وقتی عوض می‌شود که
        // «همین درخواست» رمز را عوض کرده باشد (مهری که ChangePasswordAsync نوشت). در غیر این صورت همان مهرِ
        // کوکیِ فعلی می‌ماند. هرگز مهرِ دیتابیس کپی نمی‌شود: اگر مهر جای دیگری چرخیده باشد (بازنشانی رمز روی
        // نمونهٔ دیگر) و کش این نمونه هنوز کهنه باشد، یک ذخیرهٔ سادهٔ نام (که رمز فعلی نمی‌خواهد) کوکیِ باطل‌شده
        // را با مهر تازه «شست‌وشو» می‌داد و ابطال تا پایان عمر کوکی بی‌اثر می‌شد.
        var stamp = newStamp ?? User.FindFirstValue(ClaimsPrincipalExtensions.SecurityStampClaim) ?? string.Empty;
        await HttpContext.ReissueAsync(new AuthResult(updated, stamp));

        // وضعیت کش‌شدهٔ این کاربر کنار می‌رود تا کوکی‌های قدیمیِ دستگاه‌های دیگر همان درخواست بعدی رد شوند
        if (newStamp is not null)
            SecurityStampValidator.Evict(cache, userId);

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

    /// <summary>برچسب نقش از تنها منبع برچسب‌ها (بازرگان، تنظیم‌کننده، کاربر / انباردار).</summary>
    private static string RoleLabel(string role) => RoleLabels.For(role);

    /// <summary>
    /// بعد از ورود: آدرس محلیِ درخواستی، وگرنه مدیر سامانه به پنل خودش و هر کاربر کارگاه به خانه (<c>/</c>)
    /// که کاشی‌های «ثبت فرش تازه»، «نگله‌بندی» و … را دارد.
    /// </summary>
    private IActionResult RedirectToLocalOrHome(string? returnUrl, string? role = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        role ??= User.FindFirstValue(ClaimTypes.Role);
        return role == nameof(UserRole.SystemAdmin)
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction("Index", "Home");
    }
}
