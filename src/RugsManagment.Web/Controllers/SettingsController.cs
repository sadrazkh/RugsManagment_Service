using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Activity;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.Controllers;

/// <summary>تنظیمات کارگاه: فیلدهای سفارشی، مشخصات کارگاه و تاریخچهٔ فعالیت. فقط مدیر کارگاه.</summary>
[Authorize(Roles = nameof(UserRole.TenantAdmin))]
public class SettingsController(
    ITenantSettingsService settings,
    IActivityLogService activity,
    ILookupService lookups,
    IImageUploadHelper uploads) : Controller
{
    public IActionResult CustomFields() => View();

    /// <summary>کاتالوگ انواع مرحله — مرحله‌های اختصاصی کارگاه و فرم داینامیک هر کدام.</summary>
    public IActionResult StepTypes() => View();

    // ── مشخصات کارگاه ─────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Tenant(CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        ViewData["Templates"] = await lookups.WorkflowTemplatesAsync(tenantId, ct);
        return View(await settings.GetAsync(tenantId, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tenant(
        string name, string? contactPhone, string? contactEmail,
        CurrencyUnit currency, Guid? defaultWorkflowTemplateId, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();

        try
        {
            await settings.UpdateAsync(tenantId,
                new UpdateTenantSettingsRequest(name, contactPhone, contactEmail, currency, defaultWorkflowTemplateId), ct);
            TempData["Toast"] = "تنظیمات کارگاه ذخیره شد.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            TempData["Toast"] = ex.Message;
        }

        return RedirectToAction(nameof(Tenant));
    }

    /// <summary>آپلود لوگو — همان اعتبارسنجی امضای بایتی که برای عکس فرش استفاده می‌شود.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Logo(IFormFile? file, bool remove, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();

        try
        {
            if (remove)
            {
                await uploads.RemoveTenantLogoAsync(tenantId, ct);
                TempData["Toast"] = "لوگو حذف شد.";
            }
            else if (file is { Length: > 0 })
            {
                await uploads.SaveTenantLogoAsync(tenantId, file, ct);
                TempData["Toast"] = "لوگو ذخیره شد.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Toast"] = ex.Message;
        }

        return RedirectToAction(nameof(Tenant));
    }

    // ── تاریخچهٔ فعالیت ───────────────────────────────────────

    /// <summary>
    /// پارامتر عمداً «type» نام دارد نه «action»: «action» توکن رزروشدهٔ مسیریابی MVC است
    /// و همیشه با نام اکشن جاری پر می‌شود، پس فیلتر هرگز اعمال نمی‌شد.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Activity(
        AuditAction? type, DateTimeOffset? from, DateTimeOffset? to, int page = 1, CancellationToken ct = default)
    {
        var query = new ActivityQuery
        {
            Action = type,
            From = PersianFormat.IranDayStartUtc(from),
            To = PersianFormat.IranDayEndUtc(to),
            Page = page
        };

        ViewData["Filter"] = query;
        ViewData["FromRaw"] = from;
        ViewData["ToRaw"] = to;
        return View(await activity.ListAsync(User.RequireTenantId(), query, ct));
    }
}
