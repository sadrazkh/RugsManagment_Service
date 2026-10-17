using System.Linq.Expressions;
using System.Text.Json;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// جایگزین درون‌حافظهٔ <see cref="IRepository{T}"/> برای آزمون‌های بستهٔ فرش فاز ۲: فهرست، افزودن و حذف روی یک
/// دیکشنری. «ردیابی» ساده است: <see cref="GetByIdAsync"/> همان نمونهٔ ذخیره‌شده را می‌دهد (مثل EF که موجودیت ردیابی‌شده
/// را برمی‌گرداند)، پس تغییر روی آن همان تغییرِ «ذخیره‌شده» است.
/// </summary>
internal sealed class P2MemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    public Dictionary<Guid, T> Items { get; } = [];
    public List<T> Added { get; } = [];
    public List<T> Removed { get; } = [];

    public P2MemoryRepository(params T[] seed)
    {
        foreach (var item in seed) Items[item.Id] = item;
    }

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var compiled = predicate?.Compile() ?? (_ => true);
        IReadOnlyList<T> list = Items.Values.Where(compiled).ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        Items[entity.Id] = entity;
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity) => Items[entity.Id] = entity;

    public void Remove(T entity)
    {
        Items.Remove(entity.Id);
        Removed.Add(entity);
    }
}

/// <summary>
/// <see cref="ITrashAwareQueries"/> روی همان مخزن درون‌حافظه: در حافظه فیلتر سطل زباله‌ای نیست، پس همهٔ سطرها دیده
/// می‌شوند (همان که پیاده‌سازی EF با IgnoreQueryFilters می‌دهد).
/// </summary>
internal sealed class P2TrashAwareQueries(
    P2MemoryRepository<RugPurchaseLine>? lines = null, IReadOnlyCollection<RugWorkflowStep>? steps = null) : ITrashAwareQueries
{
    public Task<bool> IsStepTypeUsedAsync(Guid processStepTypeId, CancellationToken cancellationToken = default)
        => Task.FromResult(steps?.Any(s => s.ProcessStepTypeId == processStepTypeId) ?? false);

    public Task<IReadOnlyList<RugPurchaseLine>> TrackUnlinkedLegacyPurchaseLinesAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RugPurchaseLine> found = (lines?.Items.Values ?? Enumerable.Empty<RugPurchaseLine>())
            .Where(l => l.TenantId == tenantId && l.CategoryId == null && l.Label == RugPurchaseLine.LegacyPurchaseLabel)
            .ToList();
        return Task.FromResult(found);
    }
}

/// <summary>
/// پایهٔ جایگزین‌های <see cref="IRugRepository"/> در این آزمون‌ها: هر متد NotSupported است و آزمون فقط همان‌هایی را که لازم
/// دارد بازنویسی می‌کند (اگر کد چیز دیگری صدا بزند، آزمون با پیام روشن می‌شکند).
/// </summary>
internal abstract class NotSupportedRugRepository : IRugRepository
{
    public virtual Task<Rug?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<Rug>> ListAsync(Expression<Func<Rug, bool>>? predicate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task AddAsync(Rug entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual void Update(Rug entity) => throw new NotSupportedException();
    public virtual void Remove(Rug entity) => throw new NotSupportedException();
    public virtual Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<Rug>> ListByTenantAsync(Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<Application.DTOs.Common.PagedResult<Application.DTOs.Rugs.RugListItemDto>> SearchAsync(Guid tenantId, Application.DTOs.Rugs.RugQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<int?> GetMaxSerialAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<Application.DTOs.Rugs.RugBySerialDto?> FindBySerialAsync(Guid tenantId, SerialNumber serial, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<string>> ListDistinctValuesAsync(Guid tenantId, Application.DTOs.Rugs.RugValuesQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<Application.DTOs.Rugs.DeletedRugDto>> ListDeletedAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public virtual Task<Rug?> GetDeletedAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>دیدِ پول: Business یا نه.</summary>
internal sealed class P2Money(bool canSeeMoney) : IMoneyVisibility
{
    public static readonly P2Money Business = new(true);
    public static readonly P2Money Operator = new(false);

    public bool CanSeeMoney => canSeeMoney;
}

/// <summary>تاریخچه که فقط ثبت می‌کند.</summary>
internal sealed class P2AuditLog : IAuditLog
{
    public List<(AuditAction Action, Guid? EntityId, string Summary)> Entries { get; } = [];

    public void Record(AuditAction action, string entityType, Guid? entityId, string summary,
        string? entityLabel = null, object? details = null)
        => Entries.Add((action, entityId, summary));
}

/// <summary>کد انبار پشت‌سرهم بی دیتابیس.</summary>
internal sealed class P2SkuGenerator : IRugSkuGenerator
{
    private int _next = 100;

    public Task<IReadOnlyList<string>> NextAsync(Guid tenantId, int count = 1, CancellationToken cancellationToken = default)
    {
        var list = new List<string>(count);
        for (var i = 0; i < count; i++) list.Add(RugSkuGenerator.Format("RUG", "202610", _next++));
        return Task.FromResult<IReadOnlyList<string>>(list);
    }
}

/// <summary>داده و ابزار مشترک.</summary>
internal static class P2Data
{
    public static readonly Guid TenantId = Guid.Parse("0f9b3d2e-1111-4a5b-9c8d-000000000001");

    /// <summary>JSON یک مقدار (برای Values).</summary>
    public static JsonElement? Json(object? value)
        => value is null ? null : JsonSerializer.SerializeToElement(value);

    public static Dictionary<string, JsonElement?> Values(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => Json(p.Value), StringComparer.Ordinal);

    public static RugWriteContext Context(bool isNew, SerialMode mode = SerialMode.SuggestNext, short scale = 0)
        => new(TenantId, isNew, mode, scale, new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero), Guid.NewGuid());

    /// <summary>فرش ذخیره‌شده با اندازهٔ اول.</summary>
    public static Rug Rug(decimal length = 2.08m, decimal width = 2.10m, int quantity = 1, string? serial = null)
    {
        var rug = new Rug { TenantId = TenantId, Sku = "RUG-202610-0001" };
        rug.SetIntakeSize(length, width);
        rug.SetSerialAndQuantity(SerialNumber.Parse(serial), quantity);
        return rug;
    }

    /// <summary>سه نوع خرید پایه (همان seed).</summary>
    public static P2MemoryRepository<CostCategory> PurchaseCategories()
        => new([.. Seed()]);

    private static IEnumerable<CostCategory> Seed()
        => Application.Seed.CostCategorySeedContributor.Defaults(TenantId);
}
