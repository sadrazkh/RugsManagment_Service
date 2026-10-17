using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers.Settings;
[Authorize(Policy = Policies.Configure), Route("settings/setup")]
public sealed class SetupController : Controller
{
    [HttpGet("")] public IActionResult Index() => View("~/Views/Settings/Setup.cshtml");
}
