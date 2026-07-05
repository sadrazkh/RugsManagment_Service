using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.CustomFields;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>مدیریت فیلدهای سفارشی کارگاه — فقط مدیر کارگاه.</summary>
[Route("api/custom-fields")]
[Authorize(Roles = nameof(UserRole.TenantAdmin))]
public class CustomFieldsApiController(ICustomFieldService fields) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await fields.ListAsync(User.RequireTenantId(), onlyActive: false, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomFieldRequest request, CancellationToken ct)
        => Ok(await fields.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomFieldRequest request, CancellationToken ct)
        => Ok(await fields.UpdateAsync(User.RequireTenantId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await fields.DeleteAsync(User.RequireTenantId(), id, ct);
        return NoContent();
    }
}
