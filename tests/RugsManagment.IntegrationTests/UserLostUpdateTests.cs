using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// نوشتنِ ردیف User نباید تغییرِ هم‌زمانِ دیگری را بی‌صدا برگرداند.
///
/// User توکن هم‌زمانی ندارد؛ پس اگر مسیری کل ردیف را بنویسد (DbSet.Update روی موجودیتِ tracked همهٔ
/// ستون‌ها را Modified می‌کند)، هر چیزی که بین خواندن و ذخیرهٔ آن مسیر commit شده باشد — غیرفعال‌سازی،
/// تنزل نقش، بازنشانی رمز و چرخش مهر — با مقدارِ کهنهٔ همان مسیر بازنویسی می‌شود. هر تست دقیقاً همین
/// ترتیب را می‌سازد: مسیر A کاربر را می‌خواند، تغییرِ B در درخواستی جدا commit می‌شود، بعد A ذخیره می‌کند.
/// </summary>
public sealed class UserLostUpdateTests(TwoTenantFixture fixture) : IClassFixture<TwoTenantFixture>
{
    private const string Password = "lost-Upd8-first";

    [PostgresFact]
    public async Task Login_DoesNotUndo_AConcurrentAdminDisable()
    {
        var user = await CreateOperatorAsync();
        var stampBefore = await StampAsync(user.Id);

        await using var login = fixture.AsSystemAdmin();
        var auth = login.ServiceProvider.GetRequiredService<IAuthService>();
        var result = await auth.LoginAsync(new LoginRequest(user.Email, Password)); // خواندن + PBKDF2، هنوز ذخیره نشده

        await AdminUpdateAsync(user, new UpdateUserRequest(user.FullName, UserRole.Operator, false, null));

        // AccountController.Login بعد از LoginAsync همین را صدا می‌زند (برای LastLoginAt)
        await login.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var stored = await LoadAsync(user.Id);
        Assert.False(stored.IsActive);
        Assert.NotEqual(stampBefore, stored.SecurityStamp);
        Assert.Equal(stampBefore, result.SecurityStamp); // کوکیِ همین ورود مهر قدیمی را دارد و رد خواهد شد
        Assert.NotNull(stored.LastLoginAt);
    }

    [PostgresFact]
    public async Task ProfileNameSave_DoesNotUndo_AConcurrentAdminPasswordReset()
    {
        var user = await CreateOperatorAsync();
        const string resetPassword = "lost-Upd8-reset";

        await using var scope = fixture.As(fixture.A);
        var profile = ProfileWithInterleave(scope, () =>
            AdminUpdateAsync(user, new UpdateUserRequest(user.FullName, UserRole.TenantAdmin, true, resetPassword)));

        await profile.UpdateNameAsync(user.Id, "Self renamed");

        var stored = await LoadAsync(user.Id);
        Assert.Equal("Self renamed", stored.FullName);
        Assert.Equal(UserRole.TenantAdmin, stored.Role);
        Assert.True(AuthService.VerifyPassword(resetPassword, stored.PasswordHash));
        Assert.Equal(await StampAfterAdminAsync(user.Id), stored.SecurityStamp);
    }

    [PostgresFact]
    public async Task ProfilePasswordChange_DoesNotUndo_AConcurrentAdminDisable()
    {
        var user = await CreateOperatorAsync();

        await using var scope = fixture.As(fixture.A);
        var profile = ProfileWithInterleave(scope, () =>
            AdminUpdateAsync(user, new UpdateUserRequest(user.FullName, UserRole.Operator, false, null)));

        var newStamp = await profile.ChangePasswordAsync(user.Id, Password, "lost-Upd8-mine");

        var stored = await LoadAsync(user.Id);
        Assert.False(stored.IsActive); // غیرفعال‌سازی مدیر ماند
        Assert.True(AuthService.VerifyPassword("lost-Upd8-mine", stored.PasswordHash));
        Assert.Equal(newStamp, stored.SecurityStamp);
    }

    [PostgresFact]
    public async Task AdminNameEdit_DoesNotUndo_AConcurrentOwnPasswordChange()
    {
        var user = await CreateOperatorAsync();
        string? userStamp = null;

        await using var scope = fixture.As(fixture.A);
        var users = new InterleavingUserRepository(scope.ServiceProvider.GetRequiredService<IUserRepository>(), async () =>
        {
            await using var own = fixture.As(fixture.A);
            userStamp = await own.ServiceProvider.GetRequiredService<IProfileService>()
                .ChangePasswordAsync(user.Id, Password, "lost-Upd8-own");
        });
        var service = new UserManagementService(
            users,
            scope.ServiceProvider.GetRequiredService<IAuditLog>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

        var result = await service.UpdateAsync(fixture.A.TenantId, user.Id,
            new UpdateUserRequest("Renamed by admin", UserRole.Operator, true, null));
        Assert.Null(result.RotatedSecurityStamp); // ویرایش نامِ تنها مهر نمی‌چرخاند

        var stored = await LoadAsync(user.Id);
        Assert.Equal("Renamed by admin", stored.FullName);
        Assert.True(AuthService.VerifyPassword("lost-Upd8-own", stored.PasswordHash));
        Assert.Equal(userStamp, stored.SecurityStamp);
    }

    // ── کمکی‌ها ──────────────────────────────────────────────────

    /// <summary>ProfileService واقعی روی مخزنی که بلافاصله بعد از خواندن کاربر، کار دیگری را commit می‌کند.</summary>
    private static ProfileService ProfileWithInterleave(AsyncServiceScope scope, Func<Task> afterLoad)
        => new(
            new InterleavingUserRepository(scope.ServiceProvider.GetRequiredService<IUserRepository>(), afterLoad),
            scope.ServiceProvider.GetRequiredService<IAuditLog>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

    private async Task AdminUpdateAsync(UserDto user, UpdateUserRequest request)
    {
        await using var admin = fixture.As(fixture.A);
        await admin.ServiceProvider.GetRequiredService<IUserManagementService>()
            .UpdateAsync(fixture.A.TenantId, user.Id, request);
    }

    private async Task<string> StampAfterAdminAsync(Guid userId) => (await LoadAsync(userId)).SecurityStamp;

    private async Task<UserDto> CreateOperatorAsync()
    {
        await using var scope = fixture.As(fixture.A);
        return await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateAsync(
            fixture.A.TenantId,
            new CreateUserRequest("Lost update operator", $"lost-{Guid.NewGuid():N}@it.test", Password, UserRole.Operator));
    }

    private async Task<User> LoadAsync(Guid userId)
    {
        await using var db = fixture.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    private async Task<string> StampAsync(Guid userId) => (await LoadAsync(userId)).SecurityStamp;

    /// <summary>
    /// همان مخزن واقعی؛ فقط یک بار، درست بعد از اولین خواندن کاربر، <c>afterLoad</c> را اجرا می‌کند —
    /// یعنی تغییرِ هم‌زمان دقیقاً بین خواندن و ذخیرهٔ سرویس commit می‌شود.
    /// </summary>
    private sealed class InterleavingUserRepository(IUserRepository inner, Func<Task> afterLoad) : IUserRepository
    {
        private bool _fired;

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var user = await inner.GetByIdAsync(id, cancellationToken);
            await FireOnceAsync();
            return user;
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var user = await inner.GetByEmailAsync(email, cancellationToken);
            await FireOnceAsync();
            return user;
        }

        public Task<IReadOnlyList<User>> ListAsync(
            Expression<Func<User, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => inner.ListAsync(predicate, cancellationToken);

        public Task AddAsync(User entity, CancellationToken cancellationToken = default)
            => inner.AddAsync(entity, cancellationToken);

        public void Update(User entity) => inner.Update(entity);

        public void Remove(User entity) => inner.Remove(entity);

        public Task LockTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => inner.LockTenantAdminsAsync(tenantId, cancellationToken);

        private async Task FireOnceAsync()
        {
            if (_fired) return;
            _fired = true;
            await afterLoad();
        }
    }
}
