using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>گروه‌ها/محموله‌های فرش برای عملیات دسته‌ای.</summary>
[Authorize(Roles = $"{nameof(UserRole.TenantAdmin)},{nameof(UserRole.Operator)}")]
public class GroupsController(IRugBatchService batches) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await batches.ListAsync(User.RequireTenantId(), ct);
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Toast"] = "نام گروه لازم است.";
            return RedirectToAction(nameof(Index));
        }
        await batches.CreateAsync(User.RequireTenantId(), new CreateRugBatchRequest(name, description, null), ct);
        TempData["Toast"] = "گروه ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var list = await batches.ListAsync(User.RequireTenantId(), ct);
        var batch = list.FirstOrDefault(b => b.Id == id);
        if (batch is null) return NotFound();
        ViewData["GroupId"] = id;
        ViewData["GroupName"] = batch.Name;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await batches.DeleteAsync(User.RequireTenantId(), id, ct);
        TempData["Toast"] = "گروه حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
