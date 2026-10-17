using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// راهنما. «کارت راهنما» (<c>/Help/Card</c>) یک برگ A4 با پنج قدم کار روزانه است که بازرگان چاپ می‌کند و
/// کنار رایانه می‌گذارد (view بدون layout). راهنمای هر صفحه کشوی «؟ راهنما» است و صفحهٔ جدا ندارد.
/// </summary>
[Authorize(Policy = Policies.Floor)]
public class HelpController : Controller
{
    /// <summary><c>/Help</c> همان کارت راهنماست.</summary>
    public IActionResult Index() => RedirectToAction(nameof(Card));

    public IActionResult Card() => View();
}
