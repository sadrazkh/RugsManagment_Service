using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers.Api;
[Route("api/shipments/{id:guid}")]
public sealed class DocumentsApiController(ExportService exports) : ApiControllerBase
{
    [HttpGet("documents/preview"), EnableRateLimiting("heavy")]
    public async Task<IActionResult> Preview(Guid id, [FromQuery] Guid? template, [FromQuery] Guid? templateId, [FromQuery] int? maxGroups, CancellationToken ct)
        => Ok((await exports.ShipmentAsync(User.RequireTenantId(),id,template ?? templateId,ExportFormat.Preview,maxGroups,ct)).Model);
    [HttpGet("exports")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) => Ok(await exports.RunsAsync(User.RequireTenantId(),id,ct));
}
