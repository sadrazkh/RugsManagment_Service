using System.Net;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// کش ۵ دقیقه‌ایِ SecurityStampValidator نباید کاربرِ معتبر را بیرون بیندازد و نباید ابطالِ لازم را عقب بیندازد:
/// <list type="bullet">
/// <item>وضعیتِ کش‌شده‌ای که رد می‌کند پیش از رد کردن یک بار از دیتابیس دوباره خوانده می‌شود
/// (کارگاهی که دوباره فعال شده، اشتراکی که تمدید شده، مهری که روی نمونهٔ دیگری چرخیده).</item>
/// <item>ورود موفق وضعیت کش‌شدهٔ همان کاربر را پاک می‌کند.</item>
/// <item>فعال/غیرفعال کردن کارگاه به دست ادمین سیستم، وضعیت کش‌شدهٔ همهٔ کاربران آن کارگاه را پاک می‌کند.</item>
/// </list>
/// </summary>
public sealed class SessionRevalidationTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task CachedRejectingState_IsReReadFromTheDatabase_BeforeRejecting()
    {
        var tenantId = await factory.CreateTenantAsync();
        var user = await factory.CreateUserAsync(tenantId, UserRole.Operator);

        var session = factory.NewSession();
        await session.LoginAsync(user.Email, user.Password);
        var sameCookie = session.Clone(); // پاسخ رد، کوکی را از ظرفِ همان درخواست پاک می‌کند

        // کارگاه غیرفعال → اولین درخواست وضعیت «کارگاه غیرفعال» را کش می‌کند و رد می‌شود
        await factory.SetTenantActiveBehindTheAppsBackAsync(tenantId, false);
        HttpAssert.RedirectsToLogin(await session.GetAsync("/dashboard"));

        // دوباره فعال (بدون پاک شدن کش) → همان کوکی باید پذیرفته شود، نه تا ۵ دقیقه رد
        await factory.SetTenantActiveBehindTheAppsBackAsync(tenantId, true);
        HttpAssert.Ok(await sameCookie.GetAsync("/dashboard"));
    }

    [PostgresFact]
    public async Task SuccessfulLogin_EvictsTheUsersCachedState()
    {
        var (tenantId, _) = await factory.GetDemoTenantAsync();
        var user = await factory.CreateUserAsync(tenantId, UserRole.Operator);

        var oldDevice = factory.NewSession();
        await oldDevice.LoginAsync(user.Email, user.Password);
        HttpAssert.Ok(await oldDevice.GetAsync("/dashboard")); // مهر قدیمی کش شد

        // مهر روی نمونهٔ دیگری چرخید؛ کش این نمونه هنوز مهر قدیمی را دارد
        await factory.RotateStampBehindTheAppsBackAsync(user.Id);

        // ورود تازه (با مهر جدید) کش این کاربر را پاک می‌کند
        var newDevice = factory.NewSession();
        await newDevice.LoginAsync(user.Email, user.Password);

        HttpAssert.RedirectsToLogin(await oldDevice.GetAsync("/dashboard"));
        HttpAssert.Ok(await newDevice.GetAsync("/dashboard"));
    }

    [PostgresFact]
    public async Task SystemAdminTenantToggle_EvictsTheTenantsUsers()
    {
        var tenantId = await factory.CreateTenantAsync();
        var user = await factory.CreateUserAsync(tenantId, UserRole.Operator);

        var session = factory.NewSession();
        await session.LoginAsync(user.Email, user.Password);
        HttpAssert.Ok(await session.GetAsync("/dashboard")); // «کارگاه فعال» کش شد

        var systemAdminId = await factory.GetSystemAdminIdAsync();
        using var admin = factory.CreateClientAs(TestPrincipal.SystemAdmin(systemAdminId));
        using (var toggle = await AntiforgeryHelper.PostFormAsync(admin, "/admin", "/admin/toggleactive",
                   [new("id", tenantId.ToString())]))
        {
            Assert.Equal(HttpStatusCode.Redirect, toggle.StatusCode);
        }

        // همان درخواست بعدی رد می‌شود، نه ۵ دقیقه بعد
        HttpAssert.RedirectsToLogin(await session.GetAsync("/dashboard"));
    }
}
