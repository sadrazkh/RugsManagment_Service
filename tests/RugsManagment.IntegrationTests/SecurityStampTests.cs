using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// مهر امنیتی (SecurityStamp) باید دقیقاً وقتی عوض شود که کوکی‌های قبلی دیگر نباید پذیرفته شوند:
/// تغییر نقش، تغییر فعال‌بودن و هر تغییر رمز (بازنشانی مدیر یا تغییر رمز خودِ کاربر).
/// ویرایش صرفِ نام نباید عوضش کند، وگرنه کاربر بی‌دلیل از همهٔ دستگاه‌ها بیرون می‌افتاد.
///
/// مقدار همیشه از دیتابیس (DbContext مستقل) خوانده می‌شود، چون SecurityStampValidator هم
/// همان مقدار ذخیره‌شده را با کوکی مقایسه می‌کند.
/// </summary>
public sealed class SecurityStampTests(TwoTenantFixture fixture) : IClassFixture<TwoTenantFixture>
{
    private const string InitialPassword = "first-Passw0rd";

    [PostgresFact]
    public async Task Stamp_RotatesOnRoleActiveAndPasswordChanges_ButNotOnNameOnlyEdits()
    {
        var a = fixture.A;
        var user = await CreateOperatorAsync();
        var stamp = await StampAsync(user.Id);
        Assert.Equal(32, stamp.Length);

        // فقط نام → مهر ثابت می‌ماند (و سرویس هم مهر تازه‌ای گزارش نمی‌کند)
        Assert.Null(await UpdateAsync(new UpdateUserRequest("Renamed operator", UserRole.Operator, true, null)));
        stamp = AssertUnchanged(stamp, await StampAsync(user.Id));

        // تغییر نقش — مهری که سرویس گزارش می‌کند همانی است که ذخیره شد (کنترلر همین را در کوکی مدیر می‌گذارد)
        var reported = await UpdateAsync(new UpdateUserRequest("Renamed operator", UserRole.TenantAdmin, true, null));
        stamp = AssertRotated(stamp, await StampAsync(user.Id));
        Assert.Equal(stamp, reported);

        // غیرفعال کردن، و دوباره فعال کردن
        await UpdateAsync(new UpdateUserRequest("Renamed operator", UserRole.TenantAdmin, false, null));
        stamp = AssertRotated(stamp, await StampAsync(user.Id));
        await UpdateAsync(new UpdateUserRequest("Renamed operator", UserRole.TenantAdmin, true, null));
        stamp = AssertRotated(stamp, await StampAsync(user.Id));

        // بازنشانی رمز توسط مدیر
        const string resetPassword = "reset-Passw0rd";
        await UpdateAsync(new UpdateUserRequest("Renamed operator", UserRole.TenantAdmin, true, resetPassword));
        stamp = AssertRotated(stamp, await StampAsync(user.Id));

        // تغییر رمز توسط خودِ کاربر (از پروفایل) — مهر برگشتی همانی است که ذخیره شد
        string returned;
        await using (var scope = AsUser(user.Id))
        {
            var profile = scope.ServiceProvider.GetRequiredService<IProfileService>();
            returned = await profile.ChangePasswordAsync(user.Id, resetPassword, "own-Passw0rd-2");
        }
        stamp = AssertRotated(stamp, await StampAsync(user.Id));
        Assert.Equal(stamp, returned);

        // تغییر نام از پروفایل → ثابت
        await using (var scope = AsUser(user.Id))
            await scope.ServiceProvider.GetRequiredService<IProfileService>().UpdateNameAsync(user.Id, "Self renamed");
        stamp = AssertUnchanged(stamp, await StampAsync(user.Id));

        // ورود همان مهرِ ذخیره‌شده را برمی‌گرداند (همانی که داخل کوکی می‌نشیند)
        await using (var scope = fixture.AsSystemAdmin())
        {
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var login = await auth.LoginAsync(new LoginRequest(user.Email, "own-Passw0rd-2"));
            Assert.Equal(stamp, login.SecurityStamp);
        }

        async Task<string?> UpdateAsync(UpdateUserRequest request)
        {
            await using var scope = fixture.As(a);
            var result = await scope.ServiceProvider.GetRequiredService<IUserManagementService>()
                .UpdateAsync(a.TenantId, user.Id, request);
            return result.RotatedSecurityStamp;
        }
    }

    [PostgresFact]
    public async Task WrongCurrentPassword_DoesNotRotateStamp()
    {
        var user = await CreateOperatorAsync();
        var stamp = await StampAsync(user.Id);

        await using (var scope = AsUser(user.Id))
        {
            var profile = scope.ServiceProvider.GetRequiredService<IProfileService>();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => profile.ChangePasswordAsync(user.Id, "not-the-password", "another-Passw0rd"));
        }

        AssertUnchanged(stamp, await StampAsync(user.Id));
    }

    [PostgresFact]
    public async Task OtherTenantsAdmin_CannotEditUser_AndStampIsUntouched()
    {
        var user = await CreateOperatorAsync();
        var stamp = await StampAsync(user.Id);

        await using (var scope = fixture.As(fixture.B))
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => users.UpdateAsync(
                fixture.B.TenantId, user.Id,
                new UpdateUserRequest("hijacked", UserRole.TenantAdmin, false, "hijack-Passw0rd")));
        }

        await using var db = fixture.CreateContext();
        var stored = await db.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(stamp, stored.SecurityStamp);
        Assert.Equal(UserRole.Operator, stored.Role);
        Assert.True(stored.IsActive);
        Assert.Equal("IT operator", stored.FullName);
    }

    private async Task<UserDto> CreateOperatorAsync()
    {
        await using var scope = fixture.As(fixture.A);
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var email = $"operator-{Guid.NewGuid():N}@it.test";
        return await users.CreateAsync(fixture.A.TenantId,
            new CreateUserRequest("IT operator", email, InitialPassword, UserRole.Operator));
    }

    /// <summary>یک «درخواست» از طرف خودِ کاربر (برای مسیرهای پروفایل).</summary>
    private AsyncServiceScope AsUser(Guid userId)
    {
        var scope = fixture.As(fixture.A);
        scope.ServiceProvider.GetRequiredService<FakeCurrentUser>().UserId = userId;
        return scope;
    }

    private async Task<string> StampAsync(Guid userId)
    {
        await using var db = fixture.CreateContext();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync();
    }

    private static string AssertRotated(string previous, string current)
    {
        Assert.NotEqual(previous, current);
        Assert.False(string.IsNullOrWhiteSpace(current));
        return current;
    }

    private static string AssertUnchanged(string previous, string current)
    {
        Assert.Equal(previous, current);
        return current;
    }
}
