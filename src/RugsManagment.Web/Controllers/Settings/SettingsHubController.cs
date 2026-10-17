using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Settings;

namespace RugsManagment.Web.Controllers.Settings;

/// <summary>
/// مرکز تنظیمات (<c>/settings</c>) — فقط تنظیم‌کننده (Configure)؛ بقیه به <c>/account/denied</c> می‌روند.
///
/// کاشی‌ها از همهٔ <see cref="ISettingsLink"/>های ثبت‌شده می‌آیند، گروه‌بندی و مرتب می‌شوند؛ هر فاز با یک فایل
/// لینک صفحه‌اش را این‌جا می‌آورد و نه این کنترلر و نه view آن ویرایش می‌شود. کلید تکراری فقط یک بار
/// (کمترین ترتیب) نشان داده می‌شود.
/// </summary>
[Authorize(Policy = Policies.Configure)]
[Route("settings")]
public sealed class SettingsHubController(IEnumerable<ISettingsLink> links) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var sections = links
            .GroupBy(l => l.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(l => l.Order).First())
            .GroupBy(l => l.Group)
            .OrderBy(g => g.Key)
            .Select(g => new SettingsHubSection(
                g.Key,
                SettingsGroups.Label(g.Key),
                g.OrderBy(l => l.Order).ThenBy(l => l.Title, StringComparer.Ordinal).ToList()))
            .ToList();

        return View("~/Views/Settings/Hub.cshtml", sections);
    }
}

/// <summary>یک گروه از کاشی‌های مرکز تنظیمات.</summary>
/// <param name="Group">گروه</param>
/// <param name="Label">سرتیتر فارسی گروه</param>
/// <param name="Links">کاشی‌ها به ترتیب</param>
public sealed record SettingsHubSection(SettingsGroup Group, string Label, IReadOnlyList<ISettingsLink> Links);
