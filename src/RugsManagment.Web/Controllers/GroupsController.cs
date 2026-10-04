using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Filters;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// «ورودی‌ها»: فرش‌هایی که یک روز از یک فروشنده رسیده‌اند (<c>RugBatch</c>)، برای کارهای دسته‌ای.
///
/// در فرهنگ بازرگان «محموله» فقط اظهار صادراتی است؛ پس این صفحه‌ها (و هر پیام‌شان) هرگز آن واژه را به کار
/// نمی‌برند. آدرس <c>/Groups</c> برای سازگاری با لینک‌های قدیمی می‌ماند.
/// </summary>
[Authorize(Policy = Policies.Floor)]
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
            PageToast.SetError(TempData, "نام ورودی را بنویسید.");
            return RedirectToAction(nameof(Index));
        }
        await batches.CreateAsync(User.RequireTenantId(), new CreateRugBatchRequest(name, description, null), ct);
        TempData["Toast"] = "ورودی ساخته شد.";
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
        TempData["Toast"] = "ورودی حذف شد؛ فرش‌هایش سر جایشان ماندند.";
        return RedirectToAction(nameof(Index));
    }
}
