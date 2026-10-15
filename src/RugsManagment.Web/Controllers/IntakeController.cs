using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// صفحهٔ «ورود فرش» (<c>/Intake</c>، H.1): جدول ورودِ یک روز ایران (پیش‌فرض امروز) برای هر کاربر کارگاه (Floor).
///
/// خودِ صفحه جزیرهٔ <c>intake-grid</c> است (سربرگ با تاریخ رسیدن، نوار «ورودی‌های اخیر»، جدول و پانویس هر فروشنده)؛ این
/// اکشن فقط props اول را می‌دهد: روز، فروشندهٔ پیش‌فرض (فروشندهٔ تازه‌ترین ورودی همان روز)، حالت شماره و سقف ضلع.
/// <c>?date=yyyy-MM-dd</c> روز دیگری را باز می‌کند (چیپ‌های «ورودی‌های اخیر»)؛ تاریخ نادرست همان امروز است.
/// </summary>
[Authorize(Policy = Policies.Floor)]
public class IntakeController(IRugGridService grids) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? date, CancellationToken ct)
    {
        DateOnly? day = IranDay.TryParse(date, DateTimeOffset.UtcNow, out var parsed) ? parsed : null;
        var page = await grids.IntakePageAsync(User.RequireTenantId(), day, ct);
        return View(page);
    }
}
