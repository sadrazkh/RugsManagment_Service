using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Ledger;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// پیاده‌سازی <see cref="IIntakeGridRepository"/>: ردیف‌های جدول ورود و انبار، ورودی‌های (روز، فروشنده) و چیدمان ستون‌ها.
///
/// <para>ردیف‌ها با projection بی‌ردیابی خوانده می‌شوند (همان ستون‌های تولیدشدهٔ متراژ که هر SQL دیگری می‌بیند، و xmin به‌عنوان
/// نسخه)، و سطرهای خرید با یک پرس‌وجوی دوم برای همهٔ فرش‌ها — نه یک بارگذاری کامل گراف برای هر ردیف. بارگذاری برای نوشتن
/// ردیابی‌شده است (پاس سازگاری مقدار اصلی را لازم دارد) ولی فقط ورودی، فروش و سطرهای خرید را می‌آورد.</para>
///
/// <para>هیچ نوشتن مستقیمی این‌جا نیست: ورودی تازه و چیدمان فقط به DbContext اضافه می‌شوند و <c>IUnitOfWork</c> ذخیره
/// می‌کند (قاعدهٔ «هیچ نوشتنی از پاس رد نمی‌شود»، D.4).</para>
/// </summary>
public sealed class IntakeGridRepository : IIntakeGridRepository
{
    private const string VersionProperty = "xmin";

    private readonly AppDbContext _db;

    /// <param name="tracking">فقط تا ردیاب «وصل‌شده بی بارگذاری» پیش از هر بارگذاری فرش ساخته شده باشد (مثل RugRepository).</param>
    public IntakeGridRepository(AppDbContext db, LedgerTracking tracking)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        _db = db;
    }

    // ═════════════════════════════════════════════════════════════════════
    // ردیف‌ها
    // ═════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<GridRugSnapshot>> ListIntakeDayAsync(
        Guid tenantId, DateOnly day, Guid? batchId, CancellationToken cancellationToken = default)
    {
        var query = _db.Rugs.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Batch != null && r.Batch.ReceivedOn == day);
        if (batchId is { } id)
            query = query.Where(r => r.BatchId == id);

        var rows = await Project(query.OrderBy(r => r.CreatedAt).ThenBy(r => r.SerialSort).ThenBy(r => r.Id))
            .ToListAsync(cancellationToken);
        return await WithLinesAsync(tenantId, rows, cancellationToken);
    }

    public async Task<IReadOnlyList<GridRugSnapshot>> ListByIdsAsync(
        Guid tenantId, IReadOnlyList<Guid> rugIds, CancellationToken cancellationToken = default)
    {
        if (rugIds.Count == 0) return [];
        var ids = rugIds.Distinct().ToList();

        var rows = await Project(_db.Rugs.AsNoTracking().Where(r => r.TenantId == tenantId && ids.Contains(r.Id)))
            .ToListAsync(cancellationToken);
        var withLines = await WithLinesAsync(tenantId, rows, cancellationToken);

        var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return withLines.OrderBy(r => order[r.Id]).ToList();
    }

    /// <summary>ستون‌های یک ردیف بدون سطرهای خرید (آن‌ها با پرس‌وجوی دوم برای همهٔ فرش‌ها می‌آیند).</summary>
    private sealed record RugRow(
        Guid Id, uint Version, string Sku, string? SerialNo, int? SerialFrom, int? SerialTo, long? SerialSort, int Quantity,
        string? Title, string? Origin, string? Pattern, string? Material, int? KnotDensity,
        decimal IntakeLength, decimal IntakeWidth, decimal? FinalLength, decimal? FinalWidth,
        decimal AreaM2, decimal TotalAreaM2, decimal IntakeAreaM2, string? Notes, string? MetadataJson,
        Guid? BatchId, int? BatchNumber, string? SellerName, DateOnly? ReceivedOn, DateTimeOffset CreatedAt);

    /// <summary>متراژها ستون‌های تولیدشده‌اند (همان عدد هر SQL دیگر)؛ نسخه همان xmin.</summary>
    private static IQueryable<RugRow> Project(IQueryable<Rug> source) => source.Select(r => new RugRow(
        r.Id,
        EF.Property<uint>(r, VersionProperty),
        r.Sku,
        r.SerialNo,
        r.SerialFrom,
        r.SerialTo,
        r.SerialSort,
        r.Quantity,
        r.Title,
        r.Origin,
        r.Pattern,
        r.Material,
        r.KnotDensity,
        r.IntakeLength,
        r.IntakeWidth,
        r.FinalLength,
        r.FinalWidth,
        r.AreaM2,
        r.TotalAreaM2,
        r.IntakeAreaM2,
        r.Notes,
        r.MetadataJson,
        r.BatchId,
        r.Batch != null ? (int?)r.Batch.Number : null,
        r.Batch != null ? r.Batch.SellerName : null,
        r.Batch != null ? r.Batch.ReceivedOn : null,
        r.CreatedAt));

    private async Task<IReadOnlyList<GridRugSnapshot>> WithLinesAsync(
        Guid tenantId, List<RugRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(r => r.Id).ToList();

        var lines = await _db.Set<RugPurchaseLine>().AsNoTracking()
            .Where(l => l.TenantId == tenantId && ids.Contains(l.RugId))
            .OrderBy(l => l.SortOrder).ThenBy(l => l.CreatedAt).ThenBy(l => l.Id)
            .Select(l => new { l.RugId, l.Id, l.CategoryId, l.Basis, l.Rate, l.Amount, l.SortOrder })
            .ToListAsync(cancellationToken);
        var byRug = lines.ToLookup(
            l => l.RugId, l => new GridPurchaseLine(l.Id, l.CategoryId, l.Basis, l.Rate, l.Amount, l.SortOrder));

        return rows.Select(r => new GridRugSnapshot(
                r.Id, r.Version, r.Sku, r.SerialNo, r.SerialFrom, r.SerialTo, r.SerialSort, r.Quantity,
                r.Title, r.Origin, r.Pattern, r.Material, r.KnotDensity,
                r.IntakeLength, r.IntakeWidth, r.FinalLength, r.FinalWidth,
                r.AreaM2, r.TotalAreaM2, r.IntakeAreaM2, r.Notes, r.MetadataJson,
                r.BatchId, r.BatchNumber, r.SellerName, r.ReceivedOn, r.CreatedAt,
                byRug[r.Id].ToList()))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, (Guid RugId, bool Deleted)>> FindByClientRowIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> clientRowIds, CancellationToken cancellationToken = default)
    {
        if (clientRowIds.Count == 0) return new Dictionary<Guid, (Guid, bool)>();
        var ids = clientRowIds.Distinct().ToList();

        // سطل زباله هم: ردیفی که ساخته و بعد حذف شده دوباره ساخته نمی‌شود (ایندکس یکتای ClientRowId حذف‌شده‌ها را هم دارد)
        var rows = await _db.Rugs.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ClientRowId != null && ids.Contains(r.ClientRowId.Value))
            .Select(r => new { ClientRowId = r.ClientRowId!.Value, r.Id, Deleted = r.DeletedAt != null })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ClientRowId, r => (r.Id, r.Deleted));
    }

    public async Task<IReadOnlyList<Rug>> LoadForWriteAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default)
    {
        if (rugIds.Count == 0) return [];
        var ids = rugIds.Distinct().ToList();

        return await _db.Rugs
            .Include(r => r.Batch)
            .Include(r => r.Sale)
            .Include(r => r.PurchaseLines)
            .AsSplitQuery()
            .Where(r => r.TenantId == tenantId && ids.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, RugDeleteFacts>> DeleteFactsAsync(
        Guid tenantId, Guid? userId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default)
    {
        if (rugIds.Count == 0) return new Dictionary<Guid, RugDeleteFacts>();
        var ids = rugIds.Distinct().ToList();

        // سطل زباله هم (قاعدهٔ برگرداندن همین است). «مبلغ دارد»: سطر خرید، فروش، یا هر عددی در جمع بهای فرش (کارهای
        // کارگاه، سهم هزینه‌ها و خرید یکجا) — همان «no line, allocation, sale or step cost» در G.6
        var rows = await _db.Rugs.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.TenantId == tenantId && ids.Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.CreatedAt,
                Sold = r.Sale != null,
                HasLines = r.PurchaseLines.Any(),
                HasCost = r.CostTotal != null
                          && (r.CostTotal.PurchaseCost != 0 || r.CostTotal.StepCost != 0 || r.CostTotal.ExpenseCost != 0)
            })
            .ToListAsync(cancellationToken);

        var createdByUser = userId is { } user
            ? (await _db.AuditEntries.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.UserId == user && a.Action == AuditAction.Created
                            && a.EntityType == nameof(Rug) && a.EntityId != null && ids.Contains(a.EntityId.Value))
                .Select(a => a.EntityId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet()
            : [];

        return rows.ToDictionary(
            r => r.Id,
            r => new RugDeleteFacts(r.Id, r.CreatedAt, createdByUser.Contains(r.Id), r.HasLines || r.HasCost || r.Sold, r.Sold));
    }

    // ═════════════════════════════════════════════════════════════════════
    // ورودی‌ها
    // ═════════════════════════════════════════════════════════════════════

    public async Task<RugBatch?> FindBatchAsync(
        Guid tenantId, DateOnly day, string sellerKey, CancellationToken cancellationToken = default)
    {
        // ورودیِ تازهٔ همین درخواست (هنوز ذخیره‌نشده) اول؛ وگرنه ردیف دیتابیس (ردیابی‌شده تا ردیف تازه به آن وصل شود)
        var local = _db.RugBatches.Local.FirstOrDefault(b =>
            b.TenantId == tenantId && b.ReceivedOn == day && (b.SellerKey ?? string.Empty) == sellerKey);
        if (local is not null)
            return local;

        return await _db.RugBatches
            .Where(b => b.TenantId == tenantId && b.ReceivedOn == day && (b.SellerKey ?? string.Empty) == sellerKey)
            .OrderBy(b => b.Number)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RugBatch?> GetBatchAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default)
        => _db.RugBatches.Local.FirstOrDefault(b => b.Id == batchId && b.TenantId == tenantId)
           ?? await _db.RugBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<IntakeBatchRow>> ListBatchesAsync(
        Guid tenantId, DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default)
    {
        var batches = await _db.RugBatches.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.ReceivedOn != null && b.ReceivedOn >= fromDay && b.ReceivedOn <= toDay)
            .OrderByDescending(b => b.ReceivedOn).ThenByDescending(b => b.Number)
            .ToListAsync(cancellationToken);
        return await WithTotalsAsync(tenantId, batches, cancellationToken);
    }

    public async Task<IReadOnlyList<IntakeBatchRow>> SummarizeBatchesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken = default)
    {
        if (batchIds.Count == 0) return [];
        var ids = batchIds.Distinct().ToList();
        var batches = await _db.RugBatches.AsNoTracking()
            .Where(b => b.TenantId == tenantId && ids.Contains(b.Id))
            .OrderByDescending(b => b.ReceivedOn).ThenByDescending(b => b.Number)
            .ToListAsync(cancellationToken);
        return await WithTotalsAsync(tenantId, batches, cancellationToken);
    }

    private async Task<IReadOnlyList<IntakeBatchRow>> WithTotalsAsync(
        Guid tenantId, List<RugBatch> batches, CancellationToken cancellationToken)
    {
        if (batches.Count == 0) return [];
        var ids = batches.Select(b => b.Id).ToList();

        // فرش‌های فعال (فیلتر سراسری سطل زباله)؛ جمع‌ها در SQL و numeric
        var totals = await _db.Rugs.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.BatchId != null && ids.Contains(r.BatchId.Value))
            .GroupBy(r => r.BatchId!.Value)
            .Select(g => new { BatchId = g.Key, Rows = g.Count(), Pieces = g.Sum(r => r.Quantity), Area = g.Sum(r => r.TotalAreaM2) })
            .ToListAsync(cancellationToken);
        var byBatch = totals.ToDictionary(t => t.BatchId);

        return batches.Select(b => byBatch.TryGetValue(b.Id, out var t)
                ? new IntakeBatchRow(b, t.Rows, t.Pieces, t.Area)
                : new IntakeBatchRow(b, 0, 0, 0m))
            .ToList();
    }

    // ═════════════════════════════════════════════════════════════════════
    // چیدمان ستون‌ها
    // ═════════════════════════════════════════════════════════════════════

    public async Task<VersionedGridLayout?> GetLayoutAsync(
        Guid tenantId, string gridId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<TenantGridLayout>().AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.GridId == gridId)
            .Select(l => new { Layout = l, Version = EF.Property<uint>(l, VersionProperty) })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new VersionedGridLayout(row.Layout, row.Version);
    }

    public async Task<TenantGridLayout?> GetLayoutForUpdateAsync(
        Guid tenantId, string gridId, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Set<TenantGridLayout>()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.GridId == gridId, cancellationToken);
        if (entity is null) return null;

        // نسخه‌ای که کاربر دیده بود مسلح می‌شود: UPDATE/DELETE با WHERE xmin = expected؛ اگر کس دیگری در این فاصله
        // ذخیره کرده باشد هیچ ردیفی عوض نمی‌شود و ذخیره ۴۰۹ می‌دهد
        _db.Entry(entity).Property<uint>(VersionProperty).OriginalValue = expectedVersion;
        return entity;
    }

    public async Task AddLayoutAsync(TenantGridLayout layout, CancellationToken cancellationToken = default)
        => await _db.Set<TenantGridLayout>().AddAsync(layout, cancellationToken);

    public void RemoveLayout(TenantGridLayout layout) => _db.Set<TenantGridLayout>().Remove(layout);

    public uint LayoutVersion(TenantGridLayout layout)
        => _db.Entry(layout).Property<uint>(VersionProperty).CurrentValue;
}
