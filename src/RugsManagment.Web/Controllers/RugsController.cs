using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>صفحات فرش (لیست/جزئیات/ثبت/ویرایش). فرم‌های تعاملی جزیرهٔ Vue هستند.</summary>
[Authorize(Roles = $"{nameof(UserRole.TenantAdmin)},{nameof(UserRole.Operator)}")]
public class RugsController(IRugManagementService rugs, ICustomFieldService customFields) : Controller
{
    public async Task<IActionResult> Index(RugStatus? status, CancellationToken ct)
    {
        ViewData["StatusFilter"] = status;
        var list = await rugs.ListAsync(User.RequireTenantId(), status, ct);
        return View(list);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        var rug = await rugs.GetAsync(tenantId, id, ct);
        if (rug is null) return NotFound();

        // نگاشت کلید→برچسب فیلدهای سفارشی برای نمایش خوانا
        var fields = await customFields.ListAsync(tenantId, onlyActive: false, ct);
        ViewData["CustomFieldLabels"] = fields.ToDictionary(f => f.Key, f => f.Label);
        return View(rug);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var rug = await rugs.GetAsync(User.RequireTenantId(), id, ct);
        if (rug is null) return NotFound();
        ViewData["RugId"] = id;
        return View();
    }
}
