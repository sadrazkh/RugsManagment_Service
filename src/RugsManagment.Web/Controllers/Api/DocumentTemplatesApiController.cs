using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Documents;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers.Api;
[Route("api/document-templates")]
public sealed class DocumentTemplatesApiController(DocumentTemplateService templates, IDocumentDataRepository data, IFieldCatalog fields) : ApiControllerBase
{
    public sealed record DesignerPreviewRequest(Guid ShipmentId, JsonElement Layout);
    public sealed record VersionRequest(uint Version);
    public sealed record ValidateRequest(JsonElement Layout);
    [HttpGet] public async Task<IActionResult> List([FromQuery] DocumentKind? kind, CancellationToken ct) => Ok(await templates.ListAsync(User.RequireTenantId(),kind,ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await templates.GetAsync(User.RequireTenantId(),id,ct));
    [HttpPost, Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Create(SaveDocumentTemplateRequest request, CancellationToken ct) => await Save(null,request,ct);
    [HttpPut("{id:guid}"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Update(Guid id, SaveDocumentTemplateRequest request, CancellationToken ct) => await Save(id,request,ct);
    private async Task<IActionResult> Save(Guid? id, SaveDocumentTemplateRequest request, CancellationToken ct)
    {
        var result = await templates.ValidateAsync(User.RequireTenantId(),request.Layout.GetRawText(),ct);
        if (!result.Ok) return BadRequest(new { message = "قالب را اصلاح کنید.",errors = result.Errors });
        return Ok(await templates.SaveAsync(User.RequireTenantId(),id,request,ct));
    }
    [HttpPost("validate"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Validate(ValidateRequest request, CancellationToken ct)
    { var result = await templates.ValidateAsync(User.RequireTenantId(),request.Layout.GetRawText(),ct); return result.Ok ? Ok(result) : BadRequest(new { errors = result.Errors }); }
    [HttpPost("preview"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Preview(DesignerPreviewRequest request, CancellationToken ct)
    {
        var catalogue = await fields.ListAsync(User.RequireTenantId(),FieldPurpose.Document,null,ct);
        var validation = LayoutValidator.Validate(request.Layout.GetRawText(),catalogue);
        if (!validation.Ok) return BadRequest(new { errors = validation.Errors });
        var source = await data.ShipmentAsync(User.RequireTenantId(),request.ShipmentId,true,ct);
        return Ok(DocumentComposer.Compose(LayoutJson.Parse(request.Layout.GetRawText()).Layout!,source.Data,new ComposeOptions(3,Fields:catalogue)));
    }
    [HttpPost("validate-expression"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Expression(ExpressionRequest request, CancellationToken ct)
    {
        var defaultKey = request.Kind switch { "ShipmentCost" or "1" => "shipment-cost", "RugList" or "2" => "rug-list",_ => "shipment-declaration" };
        var (layout,error) = LayoutJson.Parse(request.Layout?.GetRawText() ?? LayoutJson.Default(defaultKey).Json);
        if (layout is null) return BadRequest(new { errors = new[] { error } });
        var main = layout.Main;
        if (main is null) return BadRequest(new { errors = new[] { new { path = "$.columnKey",message = "ستون پیدا نشد." } } });
        var key = request.ColumnKey ?? "expressionPreview";
        var columns = main.Columns.ToList();
        if (!columns.Any(x => x.Key == key)) columns.Add(new ColumnLayout { Key = key, Label = "نمونه", Width = 12 });
        foreach (var columnKey in request.ColumnKeys ?? [])
            if (!columns.Any(x => x.Key == columnKey)) columns.Add(new ColumnLayout { Key = columnKey,Label = columnKey,Width = 10,Expr = "0",VisibleWhen = "never" });
        var updated = main with { Columns = columns.Select(c => c.Key == key ? c with { Field = null, Expr = request.Expression ?? request.Expr } : c).ToArray() };
        layout = layout with { Sheets = layout.Sheets.Select(s => ReferenceEquals(s,main) ? updated : s).ToArray() };
        var catalogue = await fields.ListAsync(User.RequireTenantId(),FieldPurpose.Document,null,ct);
        var result = LayoutValidator.Validate(layout,catalogue);
        if (!result.Ok) return BadRequest(new { errors = result.Errors });
        if ((request.ShipmentId ?? request.SampleShipmentId) is not { } shipmentId) return Ok(new { valid = true,ok = true, type = "Number/Text", formula = (string?)null,excel = (string?)null, samples = Array.Empty<string>() });
        var source = await data.ShipmentAsync(User.RequireTenantId(),shipmentId,true,ct);
        var render = DocumentComposer.Compose(layout,source.Data,new ComposeOptions(2,Fields:catalogue));
        var sheet = render.Sheets.First(); var column = sheet.Columns.FirstOrDefault(x => x.Key == key);
        var cells = sheet.Rows.Where(x => x.Kind == RenderRowKinds.Data).SelectMany(x => x.Cells).Where(x => x.Col == column?.Index).Take(5).ToArray();
        return Ok(new { valid = true,ok = true,type = cells.FirstOrDefault()?.IsNumber == true ? "Number" : "Text",formula = cells.FirstOrDefault()?.Formula,excel = cells.FirstOrDefault()?.Formula,samples = cells.Select(x => x.Display) });
    }
    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] uint version, CancellationToken ct) { await templates.DeleteAsync(User.RequireTenantId(),id,version,ct); return NoContent(); }
    [HttpPost("{id:guid}/duplicate"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct) => Ok(await templates.DuplicateAsync(User.RequireTenantId(),id,ct));
    [HttpPost("{id:guid}/reset"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Reset(Guid id, VersionRequest request, CancellationToken ct) => Ok(await templates.RestoreAsync(User.RequireTenantId(),id,null,request.Version,ct));
    [HttpPost("{id:guid}/set-default"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Default(Guid id, VersionRequest request, CancellationToken ct) => Ok(await templates.SetDefaultAsync(User.RequireTenantId(),id,request.Version,ct));
    [HttpGet("{id:guid}/revisions"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Revisions(Guid id, CancellationToken ct) => Ok(await templates.RevisionsAsync(User.RequireTenantId(),id,ct));
    [HttpPost("{id:guid}/revisions/{revision:int}/restore"), Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Restore(Guid id, int revision, VersionRequest request, CancellationToken ct) => Ok(await templates.RestoreAsync(User.RequireTenantId(),id,revision,request.Version,ct));
}
