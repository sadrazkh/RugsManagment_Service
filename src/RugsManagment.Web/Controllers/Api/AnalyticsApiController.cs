using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Services;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// گزارش‌های تحلیلی. برخلاف گزارش فروش (که مالی است و فقط برای مدیر)، کهنگی و
/// گلوگاه اطلاعات عملیاتی است و اپراتور هم باید ببیند تا کار گیرکرده را جلو ببرد.
/// </summary>
[Route("api/analytics")]
public class AnalyticsApiController(IAnalyticsService analytics) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int months = 12, CancellationToken ct = default)
        => Ok(await analytics.GetAsync(User.RequireTenantId(), months, ct));

    [HttpGet("aging")]
    public async Task<IActionResult> Aging(CancellationToken ct)
        => Ok(await analytics.GetAgingAsync(User.RequireTenantId(), ct));
}
