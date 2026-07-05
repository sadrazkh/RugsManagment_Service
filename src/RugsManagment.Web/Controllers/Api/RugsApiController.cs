using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>API فرش‌ها که جزیره‌های Vue مصرف می‌کنند — tenantId از کوکی کاربر (جداسازی کارگاه).</summary>
[Route("api/rugs")]
public class RugsApiController(IRugManagementService rugs) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] RugStatus? status, CancellationToken ct)
        => Ok(await rugs.ListAsync(User.RequireTenantId(), status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var rug = await rugs.GetAsync(User.RequireTenantId(), id, ct);
        return rug is null ? NotFound(new { message = "فرش یافت نشد." }) : Ok(rug);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRugRequest request, CancellationToken ct)
        => Ok(await rugs.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRugRequest request, CancellationToken ct)
        => Ok(await rugs.UpdateAsync(User.RequireTenantId(), id, request, ct));

    // ── حرکت مرحله و ثبت هزینه (استفاده در صفحهٔ جزئیات) ──

    [HttpPost("{rugId:guid}/steps/{stepId:guid}/advance")]
    public async Task<IActionResult> Advance(Guid rugId, Guid stepId, [FromBody] AdvanceRugStepRequest request, CancellationToken ct)
        => Ok(await rugs.AdvanceStepAsync(User.RequireTenantId(), rugId, stepId, request, ct));

    [HttpPut("{rugId:guid}/steps/{stepId:guid}/pricing")]
    public async Task<IActionResult> UpdatePricing(Guid rugId, Guid stepId, [FromBody] AdvanceRugStepRequest request, CancellationToken ct)
        => Ok(await rugs.UpdateStepPricingAsync(User.RequireTenantId(), rugId, stepId, request, ct));

    [HttpPost("{rugId:guid}/steps/{stepId:guid}/skip")]
    public async Task<IActionResult> Skip(Guid rugId, Guid stepId, CancellationToken ct)
        => Ok(await rugs.SkipStepAsync(User.RequireTenantId(), rugId, stepId, ct));

    [HttpPost("{rugId:guid}/steps/{stepId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid rugId, Guid stepId, CancellationToken ct)
        => Ok(await rugs.ActivateStepAsync(User.RequireTenantId(), rugId, stepId, ct));

    [HttpPost("{rugId:guid}/workflow/back")]
    public async Task<IActionResult> GoBack(Guid rugId, CancellationToken ct)
        => Ok(await rugs.GoBackStepAsync(User.RequireTenantId(), rugId, ct));
}
