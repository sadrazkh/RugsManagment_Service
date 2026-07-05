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
}
