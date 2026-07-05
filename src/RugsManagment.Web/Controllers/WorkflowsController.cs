using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>قالب‌های گردش کار کارگاه. مشاهده برای همه؛ ویرایش فقط مدیر کارگاه.</summary>
[Authorize(Roles = $"{nameof(UserRole.TenantAdmin)},{nameof(UserRole.Operator)}")]
public class WorkflowsController(IWorkflowManagementService workflows) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await workflows.ListTemplatesAsync(User.RequireTenantId(), ct);
        return View(list);
    }

    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public IActionResult Create() => View("Editor");

    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public IActionResult Edit(Guid id)
    {
        ViewData["TemplateId"] = id;
        return View("Editor");
    }
}
