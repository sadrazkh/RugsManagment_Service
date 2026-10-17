using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers;
[Authorize(Policy = Policies.Business)]
[Route("Import")]
public sealed class ImportController : Controller
{
    [HttpGet("")]
    public IActionResult Index([FromQuery] Guid? id) => View("~/Views/Import/Index.cshtml", id);
    [HttpGet("History")]
    public IActionResult History() => View("~/Views/Import/History.cshtml");
}
