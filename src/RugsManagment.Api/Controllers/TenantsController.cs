using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Api.Controllers;

/// <summary>فقط SystemAdmin — ایجاد و مدیریت کارگاه‌های مشتری</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.SystemAdmin))]
public class TenantsController(ITenantManagementService tenants) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> List(CancellationToken ct)
        => Ok(await tenants.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
        => Ok(await tenants.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantDto>> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
        => Ok(await tenants.UpdateAsync(id, request, ct));
}
