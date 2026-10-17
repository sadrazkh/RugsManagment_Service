using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// ریشهٔ سایت و صفحه‌های خطا.
///
/// <c>[Authorize]</c> فقط روی <see cref="Index"/> است، نه کلاس: صفحهٔ خطا و صفحهٔ کدهای وضعیت باید برای کاربر
/// واردنشده هم باز شوند (۴۰۴ِ یک آدرس عمومی، خطای پیش از ورود)، وگرنه خودِ صفحهٔ خطا به ورود ریدایرکت می‌شد.
/// </summary>
public class HomeController : Controller
{
    /// <summary>
    /// خانه (<c>/</c>): کاربر واردنشده → صفحهٔ ورود (challenge کوکی)؛ مدیر سامانه → <c>/Admin</c>؛ کاربر کارگاه
    /// → صفحهٔ خانه که از کارت‌های <c>IHomeCard</c> ساخته می‌شود (view و کامپوننت کارت‌ها مال پوسته‌اند و خودشان
    /// خلاصه را می‌خوانند، پس این اکشن مدلی نمی‌دهد).
    /// </summary>
    [Authorize]
    public IActionResult Index()
    {
        if (User.IsInRole(nameof(UserRole.SystemAdmin)))
            return RedirectToAction("Index", "Admin");

        // نقش ناشناخته (کوکیِ کهنه) نه کارگاه دارد نه پنل مدیر
        if (!Policies.IsFloor(User))
            return Forbid();

        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    /// <summary>
    /// صفحهٔ کدهای وضعیت (۴۰۳/۴۰۴/…) — از میان‌افزار UseStatusCodePagesWithReExecute صدا زده می‌شود.
    /// </summary>
    [AllowAnonymous]
    [Route("/status/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodeError(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }
}
