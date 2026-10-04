using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Sales;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// فروش فرش و گزارش فروش و سود.
///
/// کل کلاس Business است: هم خواندن و ثبت فروش یک فرش (مبلغ فروش، تخفیف، دریافتی) و هم گزارش کل پول است،
/// و انباردار هیچ مبلغ تومانی نمی‌بیند. صفحهٔ جزئیات فرش جزیرهٔ فروش را هم برای او اصلاً رندر نمی‌کند.
/// </summary>
[Route("api/sales")]
[Authorize(Policy = Policies.Business)]
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

    /// <summary>گزارش فروش و سود واقعی.</summary>
    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery] SalesQuery query, CancellationToken ct)
        => Ok(await sales.GetReportAsync(User.RequireTenantId(), query, ct));
}
