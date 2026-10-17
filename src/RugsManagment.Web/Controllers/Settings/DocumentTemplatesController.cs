using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers.Settings;
[Authorize(Policy = Policies.Configure), Route("settings/documents")]
public sealed class DocumentTemplatesController(DocumentTemplateService templates) : Controller
{
    [HttpGet("")] public IActionResult Index() => View("~/Views/Settings/DocumentTemplates.cshtml");
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Designer(Guid id, CancellationToken ct)
    {
        try { await templates.GetAsync(User.RequireTenantId(),id,ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        return View("~/Views/Settings/DocumentDesigner.cshtml",id);
    }
}
