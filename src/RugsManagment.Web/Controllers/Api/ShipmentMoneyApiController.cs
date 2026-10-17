using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// پول یک محموله (spec I.2 «Revenue»/«Profit»، D.6): «حساب محموله» — بهای تمام‌شده، هر متر، فروش و سود. در کنترلر جدا از
/// <see cref="ShipmentsApiController"/> (فاز ۴a، Floor) تا آن کنترلر دست نخورد و کل این کلاس Business باشد: سود و بها پول‌اند و
/// انباردار هیچ‌کدام را نمی‌بیند (A.5). مسیرها زیر همان <c>/api/shipments/{id}</c> هستند.
///
/// فروش با نسخهٔ محموله ذخیره می‌شود (نسخهٔ کهنه ۴۰۹) و سهم درآمد هر فرش در همان درخواست حساب می‌شود؛ محمولهٔ قطعی هم فروش
/// می‌پذیرد (فقط سود عوض می‌شود، نه اظهار). محمولهٔ کارگاه دیگر ۴۰۴.
/// </summary>
[Route("api/shipments")]
[Authorize(Policy = Policies.Business)]
public sealed class ShipmentMoneyApiController(IProfitService profits) : ApiControllerBase
{
    /// <summary><c>ShipmentProfitDto</c>: تخته، متراژ، دلار، خرید، کارهای کارگاه، هزینه‌ها، جمع، هر متر، فروش و سود.</summary>
    [HttpGet("{id:guid}/profit")]
    public async Task<IActionResult> Profit(Guid id, CancellationToken ct)
        => Ok(await profits.GetAsync(User.RequireTenantId(), id, ct));

    /// <summary>فروش محموله: <c>{amount, currency, fx, version}</c> → <c>ShipmentProfitDto</c>؛ مبلغ خالی یعنی «فروشی ثبت نشده».</summary>
    [HttpPut("{id:guid}/revenue")]
    public async Task<IActionResult> Revenue(Guid id, [FromBody] SaveRevenueRequest request, CancellationToken ct)
        => Ok(await profits.SetRevenueAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>سود هر فرش محموله (سهم فروش − بها، D.6)، به ترتیب شماره.</summary>
    [HttpGet("{id:guid}/profit/rugs")]
    public async Task<IActionResult> RugProfits(Guid id, CancellationToken ct)
        => Ok(await profits.RugProfitsAsync(User.RequireTenantId(), id, ct));
}
