using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Models.Users;

namespace RugsManagment.Web.Controllers;

/// <summary>مدیریت کاربران کارگاه — فقط مدیر کارگاه، محدود به همان کارگاه.</summary>
[Authorize(Roles = nameof(UserRole.TenantAdmin))]
public class UsersController(IUserManagementService users) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await users.ListByTenantAsync(User.RequireTenantId(), ct);
        return View(list);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new UserFormViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var list = await users.ListByTenantAsync(User.RequireTenantId(), ct);
        var u = list.FirstOrDefault(x => x.Id == id);
        if (u is null) return NotFound();

        return View("Form", new UserFormViewModel
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = Enum.Parse<UserRole>(u.Role),
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(UserFormViewModel model, CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();

        if (!model.IsEdit && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "رمز عبور هنگام ساخت الزامی است.");

        if (!ModelState.IsValid)
            return View("Form", model);

        try
        {
            if (model.IsEdit)
            {
                await users.UpdateAsync(tenantId, model.Id!.Value,
                    new UpdateUserRequest(model.FullName, model.Role, model.IsActive, model.Password), ct);
                TempData["Toast"] = "کاربر به‌روزرسانی شد.";
            }
            else
            {
                await users.CreateAsync(tenantId,
                    new CreateUserRequest(model.FullName, model.Email, model.Password!, model.Role), ct);
                TempData["Toast"] = "کاربر ساخته شد.";
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
        {
            model.Error = ex.Message;
            return View("Form", model);
        }

        return RedirectToAction(nameof(Index));
    }
}
