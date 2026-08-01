using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Labels;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>API قالب‌های برچسب — محدود به کارگاه کاربر.</summary>
[Route("api/labels")]
public class LabelsApiController(ILabelTemplateService labels) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await labels.ListAsync(User.RequireTenantId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var label = await labels.GetAsync(User.RequireTenantId(), id, ct);
        return label is null ? NotFound(new { message = "قالب یافت نشد." }) : Ok(label);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveLabelTemplateRequest request, CancellationToken ct)
        => Ok(await labels.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveLabelTemplateRequest request, CancellationToken ct)
        => Ok(await labels.UpdateAsync(User.RequireTenantId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await labels.DeleteAsync(User.RequireTenantId(), id, ct);
        return NoContent();
    }
}
