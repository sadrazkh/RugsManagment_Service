using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Settings;

/// <summary>
/// صفحهٔ ترجیحات کارگاه (<c>/settings/preferences</c>) — فقط تنظیم‌کننده (Configure). خودِ فرم جزیرهٔ
/// <c>preferences-form</c> است که از <c>/api/settings/preferences</c> می‌خواند و ذخیره می‌کند؛ این اکشن فقط
/// قاب صفحه را می‌دهد.
/// </summary>
[Authorize(Policy = Policies.Configure)]
[Route("settings/preferences")]
public sealed class PreferencesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Settings/Preferences.cshtml");
}
