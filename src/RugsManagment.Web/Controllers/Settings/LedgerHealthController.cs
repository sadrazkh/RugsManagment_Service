using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Settings;

namespace RugsManagment.Web.Controllers.Settings;

/// <summary>
/// صفحهٔ «سلامت حساب‌ها» (<c>/settings/ledger-health</c>) — فقط تنظیم‌کننده (Configure). خودِ صفحه جزیرهٔ
/// <c>ledger-health</c> است که <c>/api/diagnostics/cost-consistency</c> را می‌خواند و «بازسازی حساب‌ها» را می‌زند؛ این
/// اکشن فقط قاب صفحه را می‌دهد (L.4 #9: صفر ناهمخوانی).
/// </summary>
[Authorize(Policy = Policies.Configure)]
[Route("settings/ledger-health")]
public sealed class LedgerHealthController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Settings/LedgerHealth.cshtml");
}

/// <summary>کاشی «سلامت حساب‌ها» در مرکز تنظیمات (گروه نگهداری)؛ Program.cs خودکار ثبتش می‌کند.</summary>
public sealed class LedgerHealthSettingsLink() : StaticSettingsLink(
    "ledger-health", "سلامت حساب‌ها",
    "سنجش اینکه بهای همهٔ فرش‌ها با سطرهای خرید و کارهای کارگاه می‌خواند، و ساختن دوبارهٔ حساب‌ها.",
    "/settings/ledger-health", "calculator", SettingsGroup.System, 20);
