using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Controllers;

/// <summary>تنظیمات کارگاه (فیلدهای سفارشی و…). فقط مدیر کارگاه.</summary>
[Authorize(Roles = nameof(UserRole.TenantAdmin))]
public class SettingsController : Controller
{
    public IActionResult CustomFields() => View();
}
