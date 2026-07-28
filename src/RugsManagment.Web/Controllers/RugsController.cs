using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Models.Rugs;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.Controllers;

/// <summary>صفحات فرش (لیست/جزئیات/ثبت/ویرایش). فرم‌های تعاملی جزیرهٔ Vue هستند.</summary>
[Authorize(Roles = $"{nameof(UserRole.TenantAdmin)},{nameof(UserRole.Operator)}")]
public class RugsController(
    IRugManagementService rugs,
    ICustomFieldService customFields,
    IRugBatchService batches,
    ILookupService lookups) : Controller
{
    /// <summary>
    /// فهرست فرش‌ها. همهٔ فیلترها از query string می‌آیند تا آدرس صفحه قابل اشتراک،
    /// bookmark و بازگشت با دکمهٔ back مرورگر باشد.
    /// </summary>
    public async Task<IActionResult> Index([FromQuery] RugQuery query, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();

        // کاربر یک «روز» انتخاب می‌کند، نه یک لحظه: بازه را به ابتدای و انتهای همان روز
        // به وقت ایران می‌بریم تا «تا تاریخ» شامل خود آن روز هم بشود.
        var displayQuery = query.Sanitized();
        var searchQuery = displayQuery with
        {
            CreatedFrom = PersianFormat.IranDayStartUtc(query.CreatedFrom),
            CreatedTo = PersianFormat.IranDayEndUtc(query.CreatedTo)
        };

        // پشت سر هم، نه Task.WhenAll: هر سه سرویس یک DbContext اسکوپ‌شده را share می‌کنند
        // و EF Core اجازهٔ دو عملیات هم‌زمان روی یک context را نمی‌دهد.
        var result = await rugs.SearchAsync(tenantId, searchQuery, ct);

        // اگر کاربر روی صفحه‌ای فراتر از آخرین صفحه باشد (مثلاً بعد از حذف)، به آخرین صفحه برگرد
        if (displayQuery.Page > result.TotalPages && result.TotalCount > 0)
            return RedirectToAction(nameof(Index), BuildRoute(displayQuery with { Page = result.TotalPages }));

        return View(new RugListViewModel
        {
            Result = result,
            // کوئریِ نمایشی (با تاریخ‌های اصلیِ کاربر) تا فرم و لینک‌ها همان چیزی را نشان دهند که انتخاب کرده
            Query = displayQuery,
            Batches = await batches.ListAsync(tenantId, ct),
            StepTypes = await lookups.StepTypesAsync(tenantId, ct)
        });
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

    // ── سطل زباله ─────────────────────────────────────────────────
    // حذف و بازگردانی فقط دست مدیر کارگاه است: اپراتور فرش را پیش می‌برد، از چرخه بیرون نمی‌برد.

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public async Task<IActionResult> Trash(CancellationToken ct)
        => View(await rugs.ListDeletedAsync(User.RequireTenantId(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await rugs.SoftDeleteAsync(User.RequireTenantId(), id, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            // مثلاً فرش فروخته‌شده — کاربر باید بداند چرا نشد، نه اینکه صفحه بی‌صدا برگردد
            TempData["Toast"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Toast"] = "فرش به سطل زباله منتقل شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        try
        {
            await rugs.RestoreAsync(User.RequireTenantId(), id, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["Toast"] = "فرش بازگردانده شد.";
        return RedirectToAction(nameof(Trash));
    }

    /// <summary>کوئری را به route value تبدیل می‌کند (فقط برای redirect داخلی این کنترلر).</summary>
    private static object BuildRoute(RugQuery query) => new
    {
        search = query.Search,
        status = query.Status,
        batchId = query.BatchId,
        stepTypeId = query.StepTypeId,
        createdFrom = query.CreatedFrom?.ToString("yyyy-MM-dd"),
        createdTo = query.CreatedTo?.ToString("yyyy-MM-dd"),
        sortBy = query.SortBy,
        descending = query.Descending,
        page = query.Page,
        pageSize = query.PageSize
    };
}
