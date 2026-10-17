using System.Net;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// بازنویسی کوکیِ خودِ کاربر (پروفایل و ویرایش حساب خودِ مدیر) نباید کوکیِ باطل‌شده را «بشوید».
///
/// سناریو: مهر کاربر روی نمونهٔ دیگری از اپ چرخیده (بازنشانی رمز) ولی کش این نمونه هنوز مهر قدیمی را دارد
/// (پنجرهٔ پذیرفته‌شدهٔ حداکثر ۵ دقیقه). کوکیِ قدیمی (مثلاً دزدیده‌شده) در همین پنجره یک ذخیرهٔ ساده
/// (فقط نام، بدون رمز فعلی) می‌فرستد. قبلاً کوکیِ تازه با مهرِ «دیتابیس» صادر می‌شد و کوکیِ باطل‌شده تا
/// پایان عمرش معتبر می‌ماند؛ حالا مهرِ همان کوکی حفظ می‌شود و با تمام شدن کش رد می‌شود. فقط درخواستی که
/// خودش رمز (یا نقش/وضعیت) را عوض کرده مهر تازه‌ای را که خودش نوشته در کوکی می‌گذارد.
/// </summary>
public sealed class CookieReissueTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task ProfileNameOnlySave_KeepsTheCookiesOwnStamp_SoARevokedCookieIsNotLaundered()
    {
        var (tenantId, _) = await factory.GetDemoTenantAsync();
        var user = await factory.CreateUserAsync(tenantId, UserRole.Operator);

        var victim = factory.NewSession();
        await victim.LoginAsync(user.Email, user.Password);
        var attacker = victim.Clone();

        // کوکی یک بار اعتبارسنجی می‌شود → وضعیت با مهر قدیمی در کش این نمونه
        HttpAssert.Ok(await attacker.GetAsync("/account/profile"));

        await factory.RotateStampBehindTheAppsBackAsync(user.Id);

        // در پنجرهٔ کش، کوکی قدیمی هنوز پذیرفته می‌شود و فقط نام را ذخیره می‌کند
        var stolenCookie = attacker.AuthCookie;
        using (var save = await attacker.PostFormAsync("/account/profile", "/account/profile",
                   [new("FullName", "Renamed by stale cookie")]))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }
        Assert.NotEqual(stolenCookie, attacker.AuthCookie); // کوکی واقعاً بازنویسی شد

        // کش منقضی می‌شود؛ کوکیِ بازنویسی‌شده همان مهر قدیمی را دارد و رد می‌شود
        factory.ExpireCachedSecurityState(user.Id);
        HttpAssert.RedirectsToLogin(await attacker.GetAsync("/dashboard"));
    }

    [PostgresFact]
    public async Task ProfilePasswordChange_KeepsThisDevice_AndEndsTheOtherSessions()
    {
        var (tenantId, _) = await factory.GetDemoTenantAsync();
        var user = await factory.CreateUserAsync(tenantId, UserRole.Operator);

        var device = factory.NewSession();
        await device.LoginAsync(user.Email, user.Password);
        var otherDevice = device.Clone();
        HttpAssert.Ok(await otherDevice.GetAsync("/dashboard"));

        const string newPassword = "it-Web-Changed-1";
        using (var save = await device.PostFormAsync("/account/profile", "/account/profile",
               [
                   new("FullName", "IT Operator"),
                   new("CurrentPassword", user.Password),
                   new("NewPassword", newPassword),
                   new("ConfirmPassword", newPassword)
               ]))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
            Assert.Equal("/Account/Profile", HttpAssert.LocationPath(save), StringComparer.OrdinalIgnoreCase);
        }

        HttpAssert.Ok(await device.GetAsync("/dashboard"));
        HttpAssert.RedirectsToLogin(await otherDevice.GetAsync("/dashboard"));
    }

    [PostgresFact]
    public async Task AdminSelfEdit_NameOnly_KeepsTheCookiesOwnStamp_SoARevokedCookieIsNotLaundered()
    {
        var (tenantId, _) = await factory.GetDemoTenantAsync();
        var admin = await factory.CreateUserAsync(tenantId, UserRole.TenantAdmin);

        var victim = factory.NewSession();
        await victim.LoginAsync(admin.Email, admin.Password);
        var attacker = victim.Clone();
        HttpAssert.Ok(await attacker.GetAsync("/users"));

        await factory.RotateStampBehindTheAppsBackAsync(admin.Id);

        using (var save = await attacker.PostFormAsync($"/users/edit/{admin.Id}", "/users/save",
                   SelfEditFields(admin, "Self renamed", password: "")))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        factory.ExpireCachedSecurityState(admin.Id);
        HttpAssert.RedirectsToLogin(await attacker.GetAsync("/dashboard"));
    }

    [PostgresFact]
    public async Task AdminSelfPasswordReset_KeepsThisDevice_AndEndsTheOtherSessions()
    {
        var (tenantId, _) = await factory.GetDemoTenantAsync();
        var admin = await factory.CreateUserAsync(tenantId, UserRole.TenantAdmin);

        var device = factory.NewSession();
        await device.LoginAsync(admin.Email, admin.Password);
        var otherDevice = device.Clone();
        HttpAssert.Ok(await otherDevice.GetAsync("/dashboard"));

        using (var save = await device.PostFormAsync($"/users/edit/{admin.Id}", "/users/save",
                   SelfEditFields(admin, "IT TenantAdmin", password: "it-Web-Reset-22")))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        HttpAssert.Ok(await device.GetAsync("/dashboard"));
        HttpAssert.RedirectsToLogin(await otherDevice.GetAsync("/dashboard"));
    }

    private static KeyValuePair<string, string>[] SelfEditFields(SeededUser admin, string fullName, string password) =>
    [
        new("Id", admin.Id.ToString()),
        new("FullName", fullName),
        new("Email", admin.Email),
        new("Role", nameof(UserRole.TenantAdmin)),
        new("IsActive", "true"),
        new("Password", password)
    ];
}
