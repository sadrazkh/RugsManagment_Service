using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.P1Fix;

/// <summary>
/// یافتهٔ بازبینی فاز ۱: ترمز «آخرین تنظیم‌کننده» خواندن‌سپس‌نوشتن بی‌قفل بود. دو تنظیم‌کننده که هم‌زمان نقش هم را پایین
/// می‌آوردند هر کدام دیگری را هنوز فعال می‌دیدند و کارگاه بی تنظیم‌کننده می‌ماند.
///
/// آزمون همان ترتیب را قطعی می‌سازد: هر درخواست بعد از شمردن تنظیم‌کننده‌های دیگر پشت یک «دروازه» می‌ایستد تا درخواست دوم
/// هم به همان‌جا برسد (حداکثر چند ثانیه). بی‌قفل، هر دو از شمارش رد می‌شوند و هر دو ذخیره می‌کنند؛ با قفل، درخواست دوم پیش
/// از خواندن پشت قفل می‌ماند، دروازه با مهلت باز می‌شود، اولی ذخیره می‌کند و دومی آخرین تنظیم‌کننده را می‌بیند (۴۰۹).
/// </summary>
public sealed class LastAdminRaceTests(TwoTenantFixture fixture) : IClassFixture<TwoTenantFixture>
{
    private const string Password = "race-Passw0rd-1";

    [PostgresFact]
    public async Task TwoAdminsDemotingEachOther_LeaveExactlyOneActiveAdmin()
    {
        var tenant = fixture.B;
        var second = await CreateAdminAsync(tenant);
        var gate = new Gate(parties: 2, timeout: TimeSpan.FromSeconds(3));

        // اولی (مدیر اصلی) دومی را بازرگان می‌کند؛ دومی هم‌زمان اولی را
        var first = Task.Run(() => DemoteAsync(tenant, actor: tenant.AdminUserId, target: second.Id, gate));
        var other = Task.Run(() => DemoteAsync(tenant, actor: second.Id, target: tenant.AdminUserId, gate));
        var outcomes = await Task.WhenAll(first, other);

        Assert.Equal(1, outcomes.Count(o => o is null));
        var conflict = Assert.Single(outcomes, o => o is not null);
        var domain = Assert.IsType<DomainConflictException>(conflict);
        Assert.Equal(Messages.LastAdmin.Text, domain.Message);

        await using var db = fixture.CreateContext();
        var activeAdmins = await db.Users.AsNoTracking()
            .CountAsync(u => u.TenantId == tenant.TenantId && u.Role == UserRole.TenantAdmin && u.IsActive);
        Assert.Equal(1, activeAdmins);
    }

    [PostgresFact]
    public async Task TwoAdminsDeactivatingEachOther_LeaveExactlyOneActiveAdmin()
    {
        var tenant = fixture.A;
        var second = await CreateAdminAsync(tenant);
        var gate = new Gate(parties: 2, timeout: TimeSpan.FromSeconds(3));

        var first = Task.Run(() => DeactivateAsync(tenant, actor: tenant.AdminUserId, target: second.Id, gate));
        var other = Task.Run(() => DeactivateAsync(tenant, actor: second.Id, target: tenant.AdminUserId, gate));
        var outcomes = await Task.WhenAll(first, other);

        Assert.Equal(1, outcomes.Count(o => o is null));
        Assert.IsType<DomainConflictException>(Assert.Single(outcomes, o => o is not null));

        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Users.AsNoTracking()
            .CountAsync(u => u.TenantId == tenant.TenantId && u.Role == UserRole.TenantAdmin && u.IsActive));
    }

    [PostgresFact]
    public async Task NonAdminEdits_StillWork_WithTheLock()
    {
        // ویرایش عادی (انباردار → بازرگان، غیرفعال کردن انباردار) همان رفتار قبل را دارد
        var tenant = fixture.A;
        await using var scope = fixture.As(tenant);
        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var op = await service.CreateAsync(tenant.TenantId,
            new CreateUserRequest("Race operator", $"race-op-{Guid.NewGuid():N}@it.test", Password, UserRole.Operator));

        var updated = await service.UpdateAsync(tenant.TenantId, op.Id,
            new UpdateUserRequest("Race operator", UserRole.Merchant, false, null));
        Assert.False(updated.User.IsActive);
        Assert.NotNull(updated.RotatedSecurityStamp);
    }

    // ── کمکی‌ها ────────────────────────────────────────────────────────────

    private Task<Exception?> DemoteAsync(TenantSeed tenant, Guid actor, Guid target, Gate gate)
        => RunAsync(tenant, actor, target, new UpdateUserRequest("Race admin", UserRole.Merchant, true, null), gate);

    private Task<Exception?> DeactivateAsync(TenantSeed tenant, Guid actor, Guid target, Gate gate)
        => RunAsync(tenant, actor, target, new UpdateUserRequest("Race admin", UserRole.TenantAdmin, false, null), gate);

    /// <summary>یک «درخواست» مستقل (scope و DbContext جدا) از طرف <paramref name="actor"/>؛ خطا برگردانده می‌شود، پرتاب نه.</summary>
    private async Task<Exception?> RunAsync(TenantSeed tenant, Guid actor, Guid target, UpdateUserRequest request, Gate gate)
    {
        await using var scope = fixture.As(tenant);
        var current = new FakeCurrentUser { TenantId = tenant.TenantId, UserId = actor, DisplayName = "Race admin" };
        var service = new UserManagementService(
            new GatedUserRepository(scope.ServiceProvider.GetRequiredService<IUserRepository>(), gate),
            scope.ServiceProvider.GetRequiredService<IAuditLog>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            current);

        try
        {
            await service.UpdateAsync(tenant.TenantId, target, request);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private async Task<User> CreateAdminAsync(TenantSeed tenant)
    {
        await using var scope = fixture.As(tenant);
        var dto = await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateAsync(tenant.TenantId,
            new CreateUserRequest("Race admin 2", $"race-{Guid.NewGuid():N}@it.test", Password, UserRole.TenantAdmin));

        await using var db = fixture.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == dto.Id);
    }

    /// <summary>
    /// همان مخزن واقعی؛ بعد از هر شمارش تنظیم‌کننده‌ها (<see cref="IRepository{T}.ListAsync"/>) پشت دروازه می‌ایستد.
    /// </summary>
    private sealed class GatedUserRepository(IUserRepository inner, Gate gate) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => inner.GetByIdAsync(id, cancellationToken);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => inner.GetByEmailAsync(email, cancellationToken);

        public async Task<IReadOnlyList<User>> ListAsync(
            Expression<Func<User, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var list = await inner.ListAsync(predicate, cancellationToken);
            await gate.ArriveAsync();
            return list;
        }

        public Task AddAsync(User entity, CancellationToken cancellationToken = default)
            => inner.AddAsync(entity, cancellationToken);

        public void Update(User entity) => inner.Update(entity);

        public void Remove(User entity) => inner.Remove(entity);

        public Task LockTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => inner.LockTenantAdminsAsync(tenantId, cancellationToken);
    }

    /// <summary>
    /// دروازهٔ چندنفره با مهلت: هر رسنده صبر می‌کند تا همه برسند، یا تا مهلت تمام شود (تا اگر دیگری پشت قفل است، بن‌بست
    /// نشود).
    /// </summary>
    private sealed class Gate(int parties, TimeSpan timeout)
    {
        private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= parties)
                _all.TrySetResult();
            await Task.WhenAny(_all.Task, Task.Delay(timeout));
        }
    }
}
