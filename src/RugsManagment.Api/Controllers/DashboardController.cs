using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Api.Extensions;
using RugsManagment.Application.DTOs.Dashboard;
using RugsManagment.Application.Services;

namespace RugsManagment.Api.Controllers;

/// <summary>آمار خلاصه برای صفحهٔ اول داشبورد فرانت</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> Get(CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await dashboard.GetTenantDashboardAsync(tenantId, ct));
    }
}
