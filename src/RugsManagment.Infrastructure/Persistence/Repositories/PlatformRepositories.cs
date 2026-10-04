using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// ردیف ترجیحات کارگاه. نسخه همان ستون سیستمی xmin است که EF به‌صورت shadow property می‌شناسد.
/// </summary>
public sealed class TenantPreferencesRepository(AppDbContext db) : ITenantPreferencesRepository
{
    private const string VersionProperty = "xmin";

    private DbSet<TenantPreferences> Set => db.Set<TenantPreferences>();

    public async Task<VersionedTenantPreferences?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var row = await Set
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new { Preferences = p, Version = EF.Property<uint>(p, VersionProperty) })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new VersionedTenantPreferences(row.Preferences, row.Version);
    }

    /// <summary>
    /// الگوی استاندارد هم‌زمانی برای دادهٔ جداشده: مقدار «اصلی» توکن هم‌زمانی همان نسخه‌ای می‌شود که کاربر
    /// دیده بود، پس UPDATE با <c>WHERE xmin = expected</c> اجرا می‌شود و اگر کس دیگری در این فاصله ذخیره کرده
    /// باشد هیچ ردیفی عوض نمی‌شود و EF خطای هم‌زمانی می‌دهد.
    /// </summary>
    public async Task<TenantPreferences?> GetForUpdateAsync(
        Guid tenantId, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var entity = await Set.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (entity is null) return null;

        db.Entry(entity).Property<uint>(VersionProperty).OriginalValue = expectedVersion;
        return entity;
    }

    public uint CurrentVersion(TenantPreferences tracked)
        => db.Entry(tracked).Property<uint>(VersionProperty).CurrentValue;

    /// <summary>ردیفی که در همین UnitOfWork اضافه و هنوز ذخیره نشده هم «هست» حساب می‌شود.</summary>
    public async Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Set.Local.Any(p => p.TenantId == tenantId)
           || await Set.AsNoTracking().AnyAsync(p => p.TenantId == tenantId, cancellationToken);

    public async Task AddAsync(TenantPreferences preferences, CancellationToken cancellationToken = default)
        => await Set.AddAsync(preferences, cancellationToken);

    /// <summary>
    /// یک رفت‌وبرگشت با چند EXISTS (هر کدام با اولین ردیف می‌ایستد). فرش‌ها و مراحلِ سطل زباله هم شمرده
    /// می‌شوند، چون با بازگردانی دوباره زنده‌اند؛ برای همین کل کوئری بی‌فیلتر است (IgnoreQueryFilters در EF
    /// برای همهٔ کوئری اعمال می‌شود).
    /// </summary>
    public async Task<bool> HasStoredMoneyAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(_ =>
                db.Rugs.Any(r => r.TenantId == tenantId
                                 && (r.PurchaseCost != null || r.TargetSalePrice != null))
                || db.RugWorkflowSteps.Any(s => s.Rug.TenantId == tenantId
                                                && ((s.CalculatedCost != null && s.CalculatedCost != 0)
                                                    || s.ManualCostOverride != null
                                                    || (s.Adjustment != null && s.Adjustment != 0)))
                || db.RugSales.Any(s => s.TenantId == tenantId)
                || db.ProviderPayments.Any(p => p.TenantId == tenantId)
                || db.ServiceProviderRates.Any(r => r.ServiceProvider.TenantId == tenantId)
                || db.ProcessStepTypes.Any(t => t.TenantId == tenantId && t.DefaultUnitRate > 0))
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>تاریخچهٔ فعالیت با شمارش و صفحه‌بندی در SQL.</summary>
public sealed class AuditEntryRepository(AppDbContext db) : IAuditEntryRepository
{
    public async Task<PagedResult<AuditEntry>> PageAsync(
        Guid tenantId,
        AuditAction? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var query = db.AuditEntries.AsNoTracking().Where(e => e.TenantId == tenantId);

        if (action is AuditAction a)
            query = query.Where(e => e.Action == a);

        // timestamptz در پستگرس فقط offset صفر می‌پذیرد
        if (from is DateTimeOffset start)
        {
            var utc = start.ToUniversalTime();
            query = query.Where(e => e.CreatedAt >= utc);
        }

        if (to is DateTimeOffset end)
        {
            var utc = end.ToUniversalTime();
            query = query.Where(e => e.CreatedAt <= utc);
        }

        var total = await query.CountAsync(cancellationToken);
        if (total == 0)
            return new PagedResult<AuditEntry>([], 0, page, pageSize);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEntry>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<AuditEntry>> ListForEntityAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken cancellationToken = default)
        => await db.AuditEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EntityType == entityType && e.EntityId == entityId)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditEntry>> ListRecentAsync(
        Guid tenantId,
        int take,
        IReadOnlyCollection<AuditAction> excludedActions,
        CancellationToken cancellationToken = default)
    {
        var excluded = excludedActions.ToList();
        var query = db.AuditEntries.AsNoTracking().Where(e => e.TenantId == tenantId);
        if (excluded.Count > 0)
            query = query.Where(e => !excluded.Contains(e.Action));

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// «انبار» این فاز: فرش‌های فعال (نه در سطل زباله) که نه فروخته شده‌اند و نه صادر. متراژ با قاعدهٔ دسته در
/// خود SQL: <c>round(round(L,2) × round(W,2), 2)</c> برای هر فرش — همان <c>AreaMath.PieceArea</c>؛ round
/// پستگرس روی numeric نصفه را دور از صفر گرد می‌کند، مثل <c>Num.Round</c>.
/// </summary>
public sealed class RugHomeStockSource(AppDbContext db) : IHomeStockSource
{
    public async Task<HomeStock> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // GroupBy ثابت حداکثر یک ردیف می‌دهد؛ ToList به‌جای FirstOrDefault تا EF برای «First بدون OrderBy»
        // (هشدار 10103) در هر بار باز شدن خانه لاگ نکند
        var rows = await db.Rugs
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Status != RugStatus.Sold && r.Status != RugStatus.Exported)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Pieces = g.Count(),
                Area = g.Sum(r => Math.Round(Math.Round(r.LengthMeters, 2) * Math.Round(r.WidthMeters, 2), 2))
            })
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? HomeStock.Empty : new HomeStock(rows[0].Pieces, rows[0].Area);
    }
}
