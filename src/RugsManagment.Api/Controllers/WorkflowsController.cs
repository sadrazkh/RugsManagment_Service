using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Api.Extensions;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Services;

namespace RugsManagment.Api.Controllers;

/// <summary>قالب مسیر، انواع مرحله، و لیست قالیشوی/رفوگر</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowsController(IWorkflowManagementService workflows) : ControllerBase
{
    [HttpGet("step-types")]
    public async Task<ActionResult<IReadOnlyList<ProcessStepTypeDto>>> StepTypes(CancellationToken ct)
        => Ok(await workflows.ListStepTypesAsync(ct));

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<WorkflowTemplateDto>>> Templates(CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await workflows.ListTemplatesAsync(tenantId, ct));
    }

    [HttpPost("templates")]
    public async Task<ActionResult<WorkflowTemplateDto>> CreateTemplate(
        [FromBody] CreateWorkflowTemplateRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await workflows.CreateTemplateAsync(tenantId, request, ct));
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<ActionResult<WorkflowTemplateDto>> UpdateTemplate(
        Guid id, [FromBody] UpdateWorkflowTemplateRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await workflows.UpdateTemplateAsync(tenantId, id, request, ct));
    }

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<ServiceProviderDto>>> Providers(CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await workflows.ListProvidersAsync(tenantId, ct));
    }

    [HttpPost("providers")]
    public async Task<ActionResult<ServiceProviderDto>> CreateProvider(
        [FromBody] CreateServiceProviderRequest request, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        return Ok(await workflows.CreateProviderAsync(tenantId, request, ct));
    }
}
