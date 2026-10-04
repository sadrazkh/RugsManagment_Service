using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Filters;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// برچسب‌ها. فهرست و چاپ برای هر کاربر کارگاه (Floor) — بازرگان و انباردار برچسب چاپ می‌کنند؛ طراحی و حذف
/// قالب پیکربندی کارگاه است و فقط تنظیم‌کننده (Configure).
/// </summary>
[Authorize(Policy = Policies.Floor)]
public class LabelsController(ILabelTemplateService labels) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await labels.ListAsync(User.RequireTenantId(), ct);
        return View(list);
    }

    [Authorize(Policy = Policies.Configure)]
    public IActionResult Design(Guid? id)
    {
        ViewData["TemplateId"] = id;
        return View();
    }

    /// <summary>
    /// صفحهٔ چاپ برچسب برای یک یا چند فرش.
    ///
    /// آدرس قابل اشتراک است تا انباردار بتواند همان صفحه را دوباره باز و چاپ کند.
    /// اگر قالبی انتخاب نشده باشد، اولین قالب کارگاه استفاده می‌شود.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Print(
        [FromQuery] Guid[] rugIds, [FromQuery] Guid? templateId, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        var templates = await labels.ListAsync(tenantId, ct);

        if (templates.Count == 0)
        {
            PageToast.SetWarning(TempData, "برای چاپ ابتدا یک قالب برچسب بسازید.");
            return RedirectToAction(nameof(Index));
        }

        if (rugIds.Length == 0)
        {
            PageToast.SetWarning(TempData, "فرشی برای چاپ انتخاب نشده است.");
            return RedirectToAction("Index", "Rugs");
        }

        var selected = templateId.HasValue
            ? templates.FirstOrDefault(t => t.Id == templateId.Value) ?? templates[0]
            : templates[0];

        ViewData["Templates"] = templates;
        ViewData["SelectedTemplateId"] = selected.Id;
        ViewData["RugIds"] = rugIds;
        return View("Print");
    }

    [HttpPost]
    [Authorize(Policy = Policies.Configure)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await labels.DeleteAsync(User.RequireTenantId(), id, ct);
        TempData["Toast"] = "قالب برچسب حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
