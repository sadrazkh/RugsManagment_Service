using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Settings;

/// <summary>
/// صفحهٔ «ستون‌های جدول» (<c>/settings/grid</c> و <c>/settings/grid/{gridId}</c>) — فقط تنظیم‌کننده (Configure). یک زبانه
/// برای هر جدول شناخته‌شده (ورود فرش، فرش‌ها، و نگله‌ها وقتی فاز نگله‌بندی منبعش را آورد)؛ ویرایشگر جزیرهٔ
/// <c>grid-columns-editor</c> است که از <c>/api/settings/grid-layout/{gridId}</c> می‌خواند و ذخیره می‌کند.
/// </summary>
[Authorize(Policy = Policies.Configure)]
[Route("settings/grid")]
public sealed class GridSettingsController(IGridLayoutService layouts) : Controller
{
    /// <summary>زبانه‌ها: (شناسهٔ جدول، عنوان).</summary>
    public sealed record GridSettingsModel(string GridId, IReadOnlyList<(string GridId, string Title)> Grids);

    [HttpGet("")]
    [HttpGet("{gridId}")]
    public IActionResult Index(string? gridId)
    {
        var sources = layouts.Sources;
        if (sources.Count == 0)
            return NotFound();

        var selected = string.IsNullOrWhiteSpace(gridId)
            ? sources[0]
            : sources.FirstOrDefault(s => string.Equals(s.GridId, gridId, StringComparison.Ordinal));
        if (selected is null)
            return NotFound();

        return View("~/Views/Settings/GridSettings.cshtml",
            new GridSettingsModel(selected.GridId, sources.Select(s => (s.GridId, s.Title)).ToList()));
    }
}
