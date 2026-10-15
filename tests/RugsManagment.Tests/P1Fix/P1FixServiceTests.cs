using System.Linq.Expressions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests.P1Fix;

/// <summary>
/// یافته‌های بازبینی فاز ۱ در سرویس‌ها (بی دیتابیس): قفل «آخرین تنظیم‌کننده» و قاعدهٔ رمز در ساخت کارگاه.
/// نسخهٔ هم‌زمانِ واقعی ترمز در <c>IntegrationTests/P1Fix/LastAdminRaceTests</c> است.
/// </summary>
public class P1FixServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ── قفل تنظیم‌کننده‌ها ────────────────────────────────────────────────

    [Theory]
    [InlineData(UserRole.Merchant, true)]   // پایین آوردن نقش
    [InlineData(UserRole.TenantAdmin, false)] // غیرفعال کردن
    public async Task AdminReducingEdit_TakesTheTenantAdminLock_InsideOneTransaction(UserRole role, bool active)
    {
        var (service, users, unitOfWork, _, other) = TwoAdmins();

        await service.UpdateAsync(TenantId, other.Id, new UpdateUserRequest(other.FullName, role, active, null));

        Assert.Equal(new[] { TenantId }, users.AdminLocks);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Saves);
    }

    [Fact]
    public async Task EditThatKeepsAnActiveAdmin_NeedsNoLock()
    {
        var (service, users, unitOfWork, _, other) = TwoAdmins();

        await service.UpdateAsync(TenantId, other.Id, new UpdateUserRequest("نام تازه", UserRole.TenantAdmin, true, null));

        Assert.Empty(users.AdminLocks);
        Assert.Equal(0, unitOfWork.Transactions);
        Assert.Equal("نام تازه", other.FullName);
    }

    [Fact]
    public async Task LastAdminGuard_StillRuns_UnderTheLock()
    {
        var (service, users, unitOfWork, admin, other) = TwoAdmins();
        other.IsActive = false; // فقط یک تنظیم‌کنندهٔ فعال مانده

        var ex = await Assert.ThrowsAsync<Domain.Common.DomainConflictException>(() =>
            service.UpdateAsync(TenantId, admin.Id, new UpdateUserRequest(admin.FullName, UserRole.Operator, true, null)));

        Assert.Equal(Messages.LastAdmin.Text, ex.Message);
        Assert.Equal(new[] { TenantId }, users.AdminLocks); // شمارش بعد از گرفتن قفل بود
        Assert.Equal(0, unitOfWork.Saves);
    }

    private static (UserManagementService Service, FakeUserRepository Users, FakeUnitOfWork UnitOfWork, User Admin, User Other) TwoAdmins()
    {
        var users = new FakeUserRepository();
        var admin = NewUser(UserRole.TenantAdmin);
        var other = NewUser(UserRole.TenantAdmin);
        users.Users.AddRange([admin, other]);
        var unitOfWork = new FakeUnitOfWork();
        // کاربر جاری کسی جز این دو (تا ترمز «نقش خودتان» درگیر نشود)
        var service = new UserManagementService(users, new FakeAuditLog(), unitOfWork, new FakeCurrentUser(Guid.NewGuid(), TenantId));
        return (service, users, unitOfWork, admin, other);
    }

    private static User NewUser(UserRole role) => new()
    {
        TenantId = TenantId,
        Email = $"{Guid.NewGuid():N}@t.test",
        FullName = role.ToString(),
        Role = role,
        IsActive = true,
        PasswordHash = "x"
    };

    // ── رمز اولین تنظیم‌کنندهٔ کارگاه ─────────────────────────────────────

    [Theory]
    [InlineData("abc123")]
    [InlineData("1234567")]
    [InlineData("        ")]
    public async Task CreateTenant_WithAShortAdminPassword_IsRejected_BeforeAnyWrite(string password)
    {
        var (service, tenants, users, unitOfWork) = Tenants();

        var ex = await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(Request(password)));

        Assert.Equal(PasswordPolicy.TooShortMessage, ex.Message);
        Assert.True(ex.Errors.ContainsKey("adminPassword"));
        Assert.Empty(tenants.Added);
        Assert.Empty(users.Users);
        Assert.Equal(0, unitOfWork.Saves);
    }

    [Fact]
    public async Task CreateTenant_WithAPolicyPassword_CreatesTheTenantAdmin()
    {
        var (service, tenants, users, _) = Tenants();

        await service.CreateAsync(Request("Passw0rd"));

        Assert.Single(tenants.Added);
        var admin = Assert.Single(users.Users);
        Assert.Equal(UserRole.TenantAdmin, admin.Role);
        Assert.True(AuthService.VerifyPassword("Passw0rd", admin.PasswordHash));
    }

    private static CreateTenantRequest Request(string password)
        => new("کارگاه آزمون", $"t-{Guid.NewGuid():N}"[..12], "admin@t.test", password, "مدیر", null, null);

    private static (TenantManagementService Service, FakeTenantRepository Tenants, FakeUserRepository Users, FakeUnitOfWork UnitOfWork) Tenants()
    {
        var tenants = new FakeTenantRepository();
        var users = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        return (new TenantManagementService(tenants, users, unitOfWork, new NoSeed()), tenants, users, unitOfWork);
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public List<Tenant> Added { get; } = [];

        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult(Added.FirstOrDefault(t => t.Slug == slug));

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Added.FirstOrDefault(t => t.Id == id));

        public Task<IReadOnlyList<Tenant>> ListAsync(Expression<Func<Tenant, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(Added.ToList());

        public Task AddAsync(Tenant entity, CancellationToken cancellationToken = default)
        {
            Added.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(Tenant entity) { }

        public void Remove(Tenant entity) => Added.Remove(entity);
    }

    private sealed class NoSeed : ITenantSeedRunner
    {
        public Task SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
