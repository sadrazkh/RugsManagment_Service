using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>API گروه‌ها/محموله‌ها — محدود به کارگاه کاربر.</summary>
[Route("api/batches")]
public class BatchesApiController(IRugBatchService batches) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await batches.ListAsync(User.RequireTenantId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRugBatchRequest request, CancellationToken ct)
        => Ok(await batches.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRugBatchRequest request, CancellationToken ct)
        => Ok(await batches.UpdateAsync(User.RequireTenantId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await batches.DeleteAsync(User.RequireTenantId(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/rugs")]
    public async Task<IActionResult> AssignRugs(Guid id, [FromBody] BatchRugIdsRequest request, CancellationToken ct)
    {
        await batches.AssignRugsAsync(User.RequireTenantId(), id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/rugs")]
    public async Task<IActionResult> RemoveRugs(Guid id, [FromBody] BatchRugIdsRequest request, CancellationToken ct)
    {
        await batches.RemoveRugsAsync(User.RequireTenantId(), id, request, ct);
        return NoContent();
    }
}
