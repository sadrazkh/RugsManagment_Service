using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>دادهٔ کمکی فرم‌ها: قالب‌های گردش کار، انواع مرحله، طرف‌های خدمات، فیلدهای سفارشی.</summary>
[Route("api/lookups")]
public class LookupsApiController(ILookupService lookups, ICustomFieldService customFields) : ApiControllerBase
{
    [HttpGet("workflow-templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
        => Ok(await lookups.WorkflowTemplatesAsync(User.RequireTenantId(), ct));

    [HttpGet("step-types")]
    public async Task<IActionResult> StepTypes(CancellationToken ct)
        => Ok(await lookups.StepTypesAsync(ct));

    [HttpGet("service-providers")]
    public async Task<IActionResult> ServiceProviders(CancellationToken ct)
        => Ok(await lookups.ServiceProvidersAsync(User.RequireTenantId(), ct));

    [HttpGet("custom-fields")]
    public async Task<IActionResult> CustomFields(CancellationToken ct)
        => Ok(await customFields.ListAsync(User.RequireTenantId(), onlyActive: true, ct));
}
