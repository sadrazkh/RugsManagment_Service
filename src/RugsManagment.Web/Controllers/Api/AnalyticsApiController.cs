using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// گزارش‌های تحلیلی.
///
/// گزارش کامل (هزینهٔ مراحل، فروش خالص، سود ماهانه) پول است و فقط Business. کهنگی و گلوگاه هیچ مبلغی
/// ندارد و اطلاعات عملیاتی است، پس انباردار هم می‌بیند تا کار گیرکرده را جلو ببرد (Floor، از کلاس پایه).
/// </summary>
[Route("api/analytics")]
public class AnalyticsApiController(IAnalyticsService analytics) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Get([FromQuery] int months = 12, CancellationToken ct = default)
        => Ok(await analytics.GetAsync(User.RequireTenantId(), months, ct));

    [HttpGet("aging")]
    public async Task<IActionResult> Aging(CancellationToken ct)
        => Ok(await analytics.GetAgingAsync(User.RequireTenantId(), ct));
}
