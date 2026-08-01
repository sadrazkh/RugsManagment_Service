using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>API قالب‌های گردش کار و مسیر هر فرش. ساخت/ویرایش قالب فقط مدیر کارگاه.</summary>
[Route("api/workflows")]
public class WorkflowsApiController(
    IWorkflowManagementService workflows,
    IRugManagementService rugs) : ApiControllerBase
{
    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
        => Ok(await workflows.ListTemplatesAsync(User.RequireTenantId(), ct));

    [HttpPost("templates")]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateWorkflowTemplateRequest request, CancellationToken ct)
        => Ok(await workflows.CreateTemplateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("templates/{id:guid}")]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateWorkflowTemplateRequest request, CancellationToken ct)
        => Ok(await workflows.UpdateTemplateAsync(User.RequireTenantId(), id, request, ct));

    [HttpPost("providers")]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public async Task<IActionResult> CreateProvider([FromBody] CreateServiceProviderRequest request, CancellationToken ct)
        => Ok(await workflows.CreateProviderAsync(User.RequireTenantId(), request, ct));

    /// <summary>بازنویسی مسیر مراحلِ باقی‌ماندهٔ یک فرش (مراحل تمام‌شده حفظ می‌شوند).</summary>
    [HttpPut("rugs/{rugId:guid}/path")]
    public async Task<IActionResult> UpdateRugPath(Guid rugId, [FromBody] UpdateRugWorkflowRequest request, CancellationToken ct)
        => Ok(await rugs.UpdateWorkflowAsync(User.RequireTenantId(), rugId, request, ct));
}
