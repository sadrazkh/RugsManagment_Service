using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Controllers;

/// <summary>داشبورد کارگاه — خلاصهٔ کامل در Phase 7 تکمیل می‌شود.</summary>
[Authorize(Roles = $"{nameof(UserRole.TenantAdmin)},{nameof(UserRole.Operator)}")]
public class DashboardController : Controller
{
    public IActionResult Index() => View();
}
