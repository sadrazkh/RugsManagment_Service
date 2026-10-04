using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// خلاصهٔ صفحهٔ خانه (<c>GET /api/home/summary</c>, Floor): محموله‌های باز، آمادگی، انبار، پول و فعالیت اخیر.
/// برای انباردار امن است: سرویس عضوهای پولی را null و رویدادهای پولی را از «فعالیت اخیر» حذف می‌کند.
/// </summary>
[Route("api/home")]
public sealed class HomeApiController(IHomeSummaryService home) : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => Ok(await home.GetAsync(User.RequireTenantId(), ct));
}
