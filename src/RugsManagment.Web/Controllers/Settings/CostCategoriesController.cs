using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Settings;

/// <summary>
/// صفحهٔ نوع‌های هزینه (<c>/settings/cost-categories</c>) — فقط تنظیم‌کننده (Configure؛ I.4). خودِ فهرست و ویرایشگر جزیرهٔ
/// <c>cost-categories-manager</c> است که از <c>/api/cost-categories</c> می‌خواند و می‌نویسد؛ این اکشن فقط قاب صفحه است.
/// کاربر بی Configure به <c>/account/denied</c> فرستاده می‌شود (سیاست کوکی).
/// </summary>
[Authorize(Policy = Policies.Configure)]
[Route("settings/cost-categories")]
public sealed class CostCategoriesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Settings/CostCategories.cshtml");
}
