using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>قالب‌های گردش کار کارگاه. مشاهده برای هر کاربر کارگاه؛ ساخت و ویرایش فقط تنظیم‌کننده (Configure).</summary>
[Authorize(Policy = Policies.Floor)]
public class WorkflowsController(IWorkflowManagementService workflows) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await workflows.ListTemplatesAsync(User.RequireTenantId(), ct);
        return View(list);
    }

    [Authorize(Policy = Policies.Configure)]
    public IActionResult Create() => View("Editor");

    [Authorize(Policy = Policies.Configure)]
    public IActionResult Edit(Guid id)
    {
        ViewData["TemplateId"] = id;
        return View("Editor");
    }
}
