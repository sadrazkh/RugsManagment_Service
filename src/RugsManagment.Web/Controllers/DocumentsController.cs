using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Controllers.Api;
namespace RugsManagment.Web.Controllers;
[Authorize(Policy = Policies.Floor)]
[ServiceFilter(typeof(ApiExceptionFilter))]
public sealed class DocumentsController(ExportService exports) : Controller
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    [HttpGet("shipments/{id:guid}/export.xlsx"), EnableRateLimiting("heavy")]
    public async Task<IActionResult> Xlsx(Guid id, [FromQuery] Guid? template, CancellationToken ct)
        => Download(await exports.ShipmentAsync(User.RequireTenantId(),id,template,ExportFormat.Xlsx,null,ct));
    [HttpGet("shipments/{id:guid}/print"), EnableRateLimiting("heavy")]
    public async Task<IActionResult> Print(Guid id, [FromQuery] Guid? template, [FromQuery] bool autoprint, CancellationToken ct)
    { ViewData["Autoprint"] = autoprint; return View("~/Views/Documents/Print.cshtml",(await exports.ShipmentAsync(User.RequireTenantId(),id,template,ExportFormat.Print,null,ct)).Model); }
    [HttpPost("documents/rugs.xlsx"), ValidateAntiForgeryToken, EnableRateLimiting("heavy")]
    public async Task<IActionResult> Rugs([FromForm] Guid[] rugIds, [FromForm] Guid? templateId, CancellationToken ct)
        => Download(await exports.RugsAsync(User.RequireTenantId(),rugIds,templateId,ct));
    private IActionResult Download(DocumentExport export)
    {
        Response.Headers.ContentDisposition = DocumentFileName.ContentDisposition(DocumentFileName.Ascii("declaration",export.Number,export.Date),
            DocumentFileName.Persian("اظهار",export.Number,export.Date,export.Model.IsDraft));
        return File(export.File!,XlsxType);
    }
}
