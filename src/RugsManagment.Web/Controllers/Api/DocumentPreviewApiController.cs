using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers.Api;
[Route("api/documents")]
public sealed class DocumentPreviewApiController(ExportService exports, IDocumentDataRepository data, IFieldCatalog fields, DocumentTemplateService templates) : ApiControllerBase
{
    public sealed record PreviewRequest(Guid? TemplateId, JsonElement? Layout, Guid? ShipmentId, Guid[]? RugIds, int? MaxGroups);
    [HttpPost("preview"), EnableRateLimiting("heavy")]
    public async Task<IActionResult> Preview(PreviewRequest request, CancellationToken ct)
    {
        if (request.Layout is null && request.ShipmentId is { } id)
            return Ok((await exports.ShipmentAsync(User.RequireTenantId(),id,request.TemplateId,ExportFormat.Preview,request.MaxGroups,ct)).Model);
        if (request.Layout is not null && !Policies.IsConfigure(User)) return Forbid();
        var catalogue = await fields.ListAsync(User.RequireTenantId(),FieldPurpose.Document,null,ct);
        var json = request.Layout?.GetRawText();
        if (json is null)
        {
            if (request.TemplateId is not { } tid) return BadRequest(new { message = "قالب را انتخاب کنید." });
            json = (await templates.GetAsync(User.RequireTenantId(),tid,ct)).Layout.GetRawText();
        }
        var result = LayoutValidator.Validate(json,catalogue);
        if (!result.Ok) return BadRequest(new { errors = result.Errors });
        var source = request.ShipmentId is { } shipmentId ? await data.ShipmentAsync(User.RequireTenantId(),shipmentId,Policies.IsBusiness(User),ct)
            : await data.RugsAsync(User.RequireTenantId(),request.RugIds ?? [],Policies.IsBusiness(User),ct);
        return Ok(DocumentComposer.Compose(LayoutJson.Parse(json).Layout!,source.Data,new ComposeOptions(Math.Clamp(request.MaxGroups ?? 3,1,100),Fields:catalogue)));
    }
}
