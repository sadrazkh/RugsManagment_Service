using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>طراح برچسب. مشاهده/چاپ برای همه؛ طراحی برای مدیر کارگاه.</summary>
[Authorize(Roles = $"{nameof(UserRole.TenantAdmin)},{nameof(UserRole.Operator)}")]
public class LabelsController(ILabelTemplateService labels) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await labels.ListAsync(User.RequireTenantId(), ct);
        return View(list);
    }

    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public IActionResult Design(Guid? id)
    {
        ViewData["TemplateId"] = id;
        return View();
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await labels.DeleteAsync(User.RequireTenantId(), id, ct);
        TempData["Toast"] = "قالب برچسب حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
