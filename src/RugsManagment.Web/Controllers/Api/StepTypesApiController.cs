using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// کاتالوگ انواع مرحلهٔ کارگاه. خواندن برای هر کاربر کارگاه (فرم‌ها به آن نیاز دارند؛ نرخ پیش‌فرض را سرویس
/// برای انباردار null می‌کند)، ولی ساخت و ویرایش فقط تنظیم‌کننده (Configure) — چون ساختار فرایند را عوض می‌کند.
/// </summary>
[Route("api/step-types")]
public class StepTypesApiController(IStepTypeService stepTypes) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await stepTypes.ListAsync(User.RequireTenantId(), onlyActive: !includeInactive, ct));

    [HttpPost]
    [Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Create([FromBody] SaveProcessStepTypeRequest request, CancellationToken ct)
        => Ok(await stepTypes.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveProcessStepTypeRequest request, CancellationToken ct)
        => Ok(await stepTypes.UpdateAsync(User.RequireTenantId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await stepTypes.DeleteAsync(User.RequireTenantId(), id, ct);
        return NoContent();
    }
}
