using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Models.Users;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// مدیریت کاربران کارگاه — فقط تنظیم‌کننده (Configure)، محدود به همان کارگاه. نقش‌ها: بازرگان، کاربر / انباردار
/// و تنظیم‌کننده؛ حالت نمایش و اندازهٔ نوشتهٔ پیش‌فرض هر نقش را سرویس هنگام ساخت و تغییر نقش می‌گذارد.
/// </summary>
[Authorize(Policy = Policies.Configure)]
public class UsersController(
    IUserManagementService users,
    IMemoryCache cache) : Controller
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
            // مقدار واقعی، نه true ثابت: قبلاً هر ویرایشِ کاربرِ غیرفعال او را بی‌صدا دوباره فعال می‌کرد
            IsActive = u.IsActive
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
                var userId = model.Id!.Value;
                var updated = await users.UpdateAsync(tenantId, userId,
                    new UpdateUserRequest(model.FullName, model.Role, model.IsActive, model.Password), ct);

                // اگر نقش/وضعیت/رمز عوض شده باشد مهر چرخیده؛ کش همین پروسه پاک می‌شود تا
                // نشست‌های باز این کاربر همان درخواست بعدی رد شوند (نه تا ۵ دقیقه بعد).
                SecurityStampValidator.Evict(cache, userId);

                // مدیری که حساب خودش را ویرایش کرده نباید با مهر تازهٔ خودش از سیستم بیرون بیفتد؛
                // اگر حسابش هنوز فعال است، کوکی همین دستگاه با نام/نقش تازه بازنویسی می‌شود. مهرِ کوکی فقط
                // وقتی عوض می‌شود که «همین» ویرایش مهر را چرخانده باشد (همان مهری که سرویس نوشت)؛ وگرنه مهرِ
                // کوکیِ فعلی می‌ماند. کپی کردن مهرِ دیتابیس، چرخشی را که جای دیگری انجام شده (بازنشانی رمز روی
                // نمونهٔ دیگر) به این کوکی می‌آورد و کوکیِ باطل‌شده‌ای را که در پنجرهٔ کش رسیده بود زنده می‌کرد.
                if (userId == User.GetUserId() && updated.User.IsActive)
                {
                    var stamp = updated.RotatedSecurityStamp
                                ?? User.FindFirstValue(ClaimsPrincipalExtensions.SecurityStampClaim)
                                ?? string.Empty;
                    await HttpContext.ReissueAsync(new AuthResult(updated.User, stamp));
                }

                TempData["Toast"] = "کاربر به‌روزرسانی شد.";
            }
            else
            {
                await users.CreateAsync(tenantId,
                    new CreateUserRequest(model.FullName, model.Email, model.Password!, model.Role), ct);
                TempData["Toast"] = "کاربر ساخته شد.";
            }
        }
        // خطاهای قابل‌نمایش (ایمیل تکراری، «آخرین تنظیم‌کننده»، «نقش خودتان»، نقش نامعتبر) کنار همان فرم پر‌شده
        // نشان داده می‌شوند تا تنظیم‌کننده نوشته‌هایش را از دست ندهد.
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException
                                       or DomainConflictException or DomainValidationException or AppValidationException
                                       or DuplicateKeyException)
        {
            model.Error = ex.Message;
            return View("Form", model);
        }

        return RedirectToAction(nameof(Index));
    }
}
