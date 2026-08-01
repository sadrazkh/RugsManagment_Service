using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Models.Admin;

namespace RugsManagment.Web.Controllers;

/// <summary>پنل ادمین کل — ساخت و مدیریت کارگاه‌های فروشنده. فقط SystemAdmin.</summary>
[Authorize(Roles = nameof(UserRole.SystemAdmin))]
public class AdminController(ITenantManagementService tenants) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await tenants.ListAsync(ct);
        return View(list);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateTenantViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTenantViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await tenants.CreateAsync(new CreateTenantRequest(
                model.Name, model.Slug, model.AdminEmail, model.AdminPassword,
                model.AdminFullName, model.ContactPhone, model.ContactEmail), ct);
        }
        catch (InvalidOperationException ex)
        {
            model.Error = ex.Message;
            return View(model);
        }

        TempData["Toast"] = "کارگاه با موفقیت ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>فعال/غیرفعال کردن کارگاه.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        var list = await tenants.ListAsync(ct);
        var t = list.FirstOrDefault(x => x.Id == id);
        if (t is null) return NotFound();

        await tenants.UpdateAsync(id, new UpdateTenantRequest(
            t.Name, !t.IsActive, t.ContactPhone, t.ContactEmail, t.SubscriptionExpiresAt), ct);

        TempData["Toast"] = t.IsActive ? "کارگاه غیرفعال شد." : "کارگاه فعال شد.";
        return RedirectToAction(nameof(Index));
    }
}
