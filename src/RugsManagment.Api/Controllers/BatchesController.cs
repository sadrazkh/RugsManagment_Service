using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Api.Extensions;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.Services;

namespace RugsManagment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BatchesController(IRugBatchService batches) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RugBatchDto>>> List(CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await batches.ListAsync(tenantId, ct));
    }

    [HttpPost]
    public async Task<ActionResult<RugBatchDto>> Create([FromBody] CreateRugBatchRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await batches.CreateAsync(tenantId, request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RugBatchDto>> Update(Guid id, [FromBody] UpdateRugBatchRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await batches.UpdateAsync(tenantId, id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        await batches.DeleteAsync(tenantId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/rugs")]
    public async Task<IActionResult> AssignRugs(Guid id, [FromBody] BatchRugIdsRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        await batches.AssignRugsAsync(tenantId, id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/rugs")]
    public async Task<IActionResult> RemoveRugs(Guid id, [FromBody] BatchRugIdsRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        await batches.RemoveRugsAsync(tenantId, id, request, ct);
        return NoContent();
    }
}
