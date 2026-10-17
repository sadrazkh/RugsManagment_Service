using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// صفحهٔ «بیشتر» (<c>/More</c>): هر صفحه‌ای که بیننده اجازه دارد ولی در نوار اصلی نیست (ورودی‌ها، برچسب‌ها،
/// کارهای کارگاه، فروش، کارگاه‌ها، گزارش‌ها، راهنما، حساب). آیتم‌ها را view از منوساز (<c>NavSlot.More</c>)
/// می‌خواند، پس این اکشن مدلی نمی‌دهد و هر فاز فقط آیتم منو اضافه می‌کند.
/// </summary>
[Authorize(Policy = Policies.Floor)]
public class MoreController : Controller
{
    public IActionResult Index() => View();
}
