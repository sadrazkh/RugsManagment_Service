using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// گزارش‌های عملیاتی: کهنگی/گلوگاه، شکست هزینه به تفکیک مرحله و روند ماهانه.
/// اپراتور هم دسترسی دارد چون این‌ها ابزار جلو بردن کار است، نه اطلاعات مالیِ محرمانه.
/// </summary>
[Authorize(Roles = $"{nameof(UserRole.TenantAdmin)},{nameof(UserRole.Operator)}")]
public class ReportsController : Controller
{
    public IActionResult Index() => View();
}
