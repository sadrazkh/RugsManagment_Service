using System.Linq.Expressions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>جایگزین‌های درون‌حافظه‌ای برای آزمون سرویس‌ها بدون دیتابیس.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }
    public int Resets { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.FromResult(1);
    }

    public void Reset() => Resets++;

    /// <summary>تراکنش‌های درون‌حافظه همان اجرای مستقیم‌اند؛ شمارش برای آزمون‌هایی که «یک تراکنش» را می‌سنجند.</summary>
    public int Transactions { get; private set; }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        Transactions++;
        await work(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        Transactions++;
        return await work(cancellationToken);
    }
}

internal sealed class FakeAuditLog : IAuditLog
{
    public List<(AuditAction Action, string EntityType, Guid? EntityId, string Summary, object? Details)> Records { get; } = [];

    public void Record(AuditAction action, string entityType, Guid? entityId, string summary,
        string? entityLabel = null, object? details = null)
        => Records.Add((action, entityType, entityId, summary, details));
}

internal sealed class FakeCurrentUser(Guid? userId = null, Guid? tenantId = null) : ICurrentUser
{
    public Guid? UserId { get; set; } = userId;
    public string? DisplayName { get; set; } = "Test";
    public Guid? TenantId { get; set; } = tenantId;
}

/// <summary>کاربران در حافظه؛ GetById همان شیء را برمی‌گرداند (مثل موجودیت ردیابی‌شده).</summary>
internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

    public Task<IReadOnlyList<User>> ListAsync(Expression<Func<User, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<User>>(predicate is null ? Users.ToList() : Users.Where(predicate.Compile()).ToList());

    public Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        Users.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(User entity) { }

    public void Remove(User entity) => Users.Remove(entity);

    /// <summary>هر کارگاهی که قفل تنظیم‌کننده‌هایش گرفته شد، به ترتیب.</summary>
    public List<Guid> AdminLocks { get; } = [];

    public Task LockTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        AdminLocks.Add(tenantId);
        return Task.CompletedTask;
    }
}

/// <summary>یک ردیف ترجیحات در حافظه با نسخه‌ای که هر ذخیره یکی بالا می‌برد (شبیه xmin).</summary>
internal sealed class FakeTenantPreferencesRepository : ITenantPreferencesRepository
{
    public TenantPreferences? Row { get; set; }
    public uint Version { get; set; } = 7;
    public bool HasMoney { get; set; }
    public uint? ExpectedVersion { get; private set; }
    public int Adds { get; private set; }

    public Task<VersionedTenantPreferences?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(Row is { } row && row.TenantId == tenantId ? new VersionedTenantPreferences(row, Version) : null);

    public Task<TenantPreferences?> GetForUpdateAsync(Guid tenantId, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        ExpectedVersion = expectedVersion;
        return Task.FromResult(Row is { } row && row.TenantId == tenantId ? row : null);
    }

    public uint CurrentVersion(TenantPreferences tracked) => Version;

    public Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(Row?.TenantId == tenantId);

    public Task AddAsync(TenantPreferences preferences, CancellationToken cancellationToken = default)
    {
        Row = preferences;
        Adds++;
        return Task.CompletedTask;
    }

    public Task<bool> HasStoredMoneyAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(HasMoney);
}

/// <summary>فقط آنچه آزمون‌های پول در RugManagementService لازم دارند؛ بقیه پرتاب می‌کنند.</summary>
internal sealed class FakeRugRepository : IRugRepository
{
    public Dictionary<Guid, Rug> Rugs { get; } = [];
    public RugQuery? LastQuery { get; private set; }
    public PagedResult<RugListItemDto> Page { get; set; } = PagedResult<RugListItemDto>.Empty(25);

    public Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(Rugs.TryGetValue(id, out var rug) && rug.TenantId == tenantId ? rug : null);

    public Task<PagedResult<RugListItemDto>> SearchAsync(Guid tenantId, RugQuery query, CancellationToken cancellationToken = default)
    {
        LastQuery = query;
        return Task.FromResult(Page);
    }

    public void Update(Rug entity) { }

    public Task<IReadOnlyList<Rug>> ListByTenantAsync(Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Rug>>(Rugs.Values.Where(r => r.TenantId == tenantId).ToList());

    public Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Rug?> GetDeletedAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<int?> GetMaxSerialAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RugBySerialDto?> FindBySerialAsync(Guid tenantId, Domain.Common.SerialNumber serial, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<string>> ListDistinctValuesAsync(Guid tenantId, RugValuesQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Rug?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Rug>> ListAsync(Expression<Func<Rug, bool>>? predicate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AddAsync(Rug entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void Remove(Rug entity) => throw new NotSupportedException();
}

/// <summary>کاتالوگ خالی مرحله‌ها برای ساختن WorkflowEngine واقعی.</summary>
internal sealed class EmptyStepTypeLookup : Application.Services.IProcessStepTypeLookup
{
    public Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken ct = default)
        => throw new KeyNotFoundException();
}
