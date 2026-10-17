using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Documents;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers.Api;
[Authorize(Policy = Policies.Configure), Route("api/settings")]
public sealed class SettingsPresetsApiController(PresetService preset, SetupStatusService setup) : ApiControllerBase
{
    [HttpPost("presets/export-rugs")]
    public async Task<IActionResult> Apply(PresetRequest request, CancellationToken ct) => Ok(new { items = await preset.ApplyAsync(User.RequireTenantId(),request,ct) });
    [HttpGet("setup-status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await setup.GetAsync(User.RequireTenantId(),ct));
}
