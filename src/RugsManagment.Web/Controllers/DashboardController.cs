using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// داشبورد کارگاه — خلاصهٔ آماری (همهٔ اعداد در DashboardService محاسبه می‌شوند). برای هر کاربر کارگاه؛
/// عضوهای پولی را سرویس برای انباردار null می‌کند و view کارت‌های پولی را برایش اصلاً رندر نمی‌کند.
/// </summary>
[Authorize(Policy = Policies.Floor)]
public class DashboardController(IDashboardService dashboard) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var stats = await dashboard.GetTenantDashboardAsync(User.RequireTenantId(), ct);
        return View(stats);
    }
}
