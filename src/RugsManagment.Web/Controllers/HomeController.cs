using Microsoft.AspNetCore.Mvc;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

public class HomeController : Controller
{
    /// <summary>ریشه — بر اساس وضعیت ورود و نقش به مقصد مناسب هدایت می‌شود.</summary>
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        return User.GetUserRole() == UserRole.SystemAdmin
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction("Index", "Dashboard");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    /// <summary>
    /// صفحهٔ کدهای وضعیت (۴۰۳/۴۰۴/…) — از میان‌افزار UseStatusCodePagesWithReExecute صدا زده می‌شود.
    /// </summary>
    [Route("/status/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodeError(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }
}
