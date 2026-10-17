using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// گزارش‌های عملیاتی: کهنگی/گلوگاه، شکست هزینه به تفکیک مرحله و روند ماهانه.
///
/// فقط Business (بازرگان و تنظیم‌کننده): این صفحه هزینهٔ مراحل، فروش خالص و سود ماهانه را نشان می‌دهد و
/// انباردار هیچ مبلغ تومانی نمی‌بیند. هشدار کهنگی (بدون مبلغ) برای انباردار در داشبورد می‌ماند.
/// </summary>
[Authorize(Policy = Policies.Business)]
public class ReportsController : Controller
{
    public IActionResult Index() => View();
}
