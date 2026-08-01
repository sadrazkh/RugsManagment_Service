using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Sales;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// فروش فرش. ثبت فروش کار روزمرهٔ اپراتور است، ولی گزارش کل فروش و سود
/// اطلاعات مالی کارگاه است و فقط برای مدیر باز می‌شود.
/// </summary>
[Route("api/sales")]
public class SalesApiController(IRugSaleService sales) : ApiControllerBase
{
    [HttpGet("rug/{rugId:guid}")]
    public async Task<IActionResult> GetForRug(Guid rugId, CancellationToken ct)
    {
        var sale = await sales.GetForRugAsync(User.RequireTenantId(), rugId, ct);
        return sale is null ? NoContent() : Ok(sale);
    }

    [HttpPut("rug/{rugId:guid}")]
    public async Task<IActionResult> Save(Guid rugId, [FromBody] SaveRugSaleRequest request, CancellationToken ct)
        => Ok(await sales.SaveAsync(User.RequireTenantId(), rugId, request, ct));

    [HttpDelete("rug/{rugId:guid}")]
    public async Task<IActionResult> Cancel(Guid rugId, CancellationToken ct)
    {
        await sales.CancelAsync(User.RequireTenantId(), rugId, ct);
        return NoContent();
    }

    /// <summary>گزارش فروش و سود واقعی — فقط مدیر کارگاه.</summary>
    [HttpGet("report")]
    [Authorize(Roles = nameof(UserRole.TenantAdmin))]
    public async Task<IActionResult> Report([FromQuery] SalesQuery query, CancellationToken ct)
        => Ok(await sales.GetReportAsync(User.RequireTenantId(), query, ct));
}
