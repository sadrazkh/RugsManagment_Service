using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Api.Extensions;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Api.Controllers;

/// <summary>
/// API فرش‌ها — همهٔ متدها tenantId را از JWT می‌گیرند (جداسازی کارگاه‌ها).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RugsController(IRugManagementService rugs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RugDto>>> List([FromQuery] RugStatus? status, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.ListAsync(tenantId, status, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RugDto>> Get(Guid id, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        var rug = await rugs.GetAsync(tenantId, id, ct);
        return rug is null ? NotFound() : Ok(rug);
    }

    [HttpPost]
    public async Task<ActionResult<RugDto>> Create([FromBody] CreateRugRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.CreateAsync(tenantId, request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RugDto>> Update(Guid id, [FromBody] UpdateRugRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.UpdateAsync(tenantId, id, request, ct));
    }

    /// <summary>تکمیل یا به‌روزرسانی مرحلهٔ جاری فرش</summary>
    [HttpPost("{rugId:guid}/steps/{stepId:guid}/advance")]
    public async Task<ActionResult<RugDto>> Advance(
        Guid rugId, Guid stepId, [FromBody] AdvanceRugStepRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.AdvanceStepAsync(tenantId, rugId, stepId, request, ct));
    }

    /// <summary>ذخیرهٔ روش و مبلغ هزینهٔ یک مرحله بدون تکمیل آن</summary>
    [HttpPut("{rugId:guid}/steps/{stepId:guid}/pricing")]
    public async Task<ActionResult<RugDto>> UpdateStepPricing(
        Guid rugId, Guid stepId, [FromBody] AdvanceRugStepRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.UpdateStepPricingAsync(tenantId, rugId, stepId, request, ct));
    }

    /// <summary>رد مرحله اختیاری (مثلاً بدون دارکشی)</summary>
    [HttpPost("{rugId:guid}/steps/{stepId:guid}/skip")]
    public async Task<ActionResult<RugDto>> Skip(Guid rugId, Guid stepId, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.SkipStepAsync(tenantId, rugId, stepId, ct));
    }

    [HttpPost("{rugId:guid}/workflow/back")]
    public async Task<ActionResult<RugDto>> GoBack(Guid rugId, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.GoBackStepAsync(tenantId, rugId, ct));
    }

    [HttpPost("{rugId:guid}/steps/{stepId:guid}/activate")]
    public async Task<ActionResult<RugDto>> ActivateStep(Guid rugId, Guid stepId, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.ActivateStepAsync(tenantId, rugId, stepId, ct));
    }

    [HttpPut("{rugId:guid}/workflow")]
    public async Task<ActionResult<RugDto>> UpdateWorkflow(
        Guid rugId, [FromBody] UpdateRugWorkflowRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.UpdateWorkflowAsync(tenantId, rugId, request, ct));
    }

    [HttpPost("bulk/advance")]
    public async Task<ActionResult<BulkOperationResultDto>> BulkAdvance(
        [FromBody] BulkAdvanceRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.BulkAdvanceAsync(tenantId, request, ct));
    }

    [HttpPut("bulk/workflow")]
    public async Task<ActionResult<BulkOperationResultDto>> BulkWorkflow(
        [FromBody] BulkUpdateWorkflowRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await rugs.BulkUpdateWorkflowAsync(tenantId, request, ct));
    }
}
