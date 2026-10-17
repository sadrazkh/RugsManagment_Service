using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Features.Intake.Grid;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// چیدمان ستون‌های جدول‌ها (<c>/api/settings/grid-layout/{gridId}</c>، فقط تنظیم‌کننده). PUT با نسخهٔ کهنه → ۴۰۹؛ DELETE
/// برگشت به پیش‌فرض برنامه.
/// </summary>
[Route("api/settings/grid-layout")]
[Authorize(Policy = Policies.Configure)]
public class GridLayoutApiController(IGridLayoutService layouts, GridViewerFactory viewers) : ApiControllerBase
{
    [HttpGet("{gridId}")]
    public async Task<IActionResult> Get(string gridId, CancellationToken ct)
        => Ok(await layouts.GetAsync(await viewers.CreateAsync(User.RequireTenantId(), ct), gridId, ct));

    [HttpPut("{gridId}")]
    public async Task<IActionResult> Put(string gridId, [FromBody] GridLayoutDto request, CancellationToken ct)
        => Ok(await layouts.SaveAsync(await viewers.CreateAsync(User.RequireTenantId(), ct), gridId, request, ct));

    [HttpDelete("{gridId}")]
    public async Task<IActionResult> Reset(string gridId, [FromQuery] uint version, CancellationToken ct)
        => Ok(await layouts.ResetAsync(await viewers.CreateAsync(User.RequireTenantId(), ct), gridId, version, ct));
}
