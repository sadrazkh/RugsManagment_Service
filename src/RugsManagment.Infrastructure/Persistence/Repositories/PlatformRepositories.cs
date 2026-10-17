using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// ردیف ترجیحات کارگاه. نسخه همان ستون سیستمی xmin است که EF به‌صورت shadow property می‌شناسد.
/// </summary>
public sealed class TenantPreferencesRepository(AppDbContext db, IEnumerable<IStoredMoneyProbe> moneyProbes)
    : ITenantPreferencesRepository
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
    /// همان پرسش‌های <see cref="IStoredMoneyProbe"/> که <c>PreferencesLockGuard</c> پاس سازگاری می‌پرسد (D.5 #22).
    ///
    /// چرا واگذاری و نه کوئری جدا: صفحهٔ ترجیحات («قفل است») و پاس (۴۰۹) باید یک جواب بدهند. از فاز ۲ قیمت خرید سطر خرید
    /// است و فازهای محموله و هزینه‌ها پرسش خودشان را به‌صورت probe تازه اضافه می‌کنند؛ کوئری جداگانه این‌جا یا ستون منسوخ
    /// <c>Rug.PurchaseCost</c> را می‌پرسید یا جدول‌های بعدی را جا می‌انداخت. سطل زباله را خودِ probeها می‌شمرند.
    /// </summary>
    public async Task<bool> HasStoredMoneyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var probe in moneyProbes)
        {
            if (await probe.AnyAsync(tenantId, cancellationToken))
                return true;
        }

        return false;
    }
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
            return new PagedResult<AuditEntry>([], 0, 1, pageSize);

        // صفحهٔ بعد از آخر همان صفحهٔ آخر است. حساب در long: «?page=80000000» در int سرریز می‌شد، OFFSET منفی به
        // پستگرس می‌رسید (2201X) و صفحه ۵۰۰ می‌داد. بعد از بریدن، OFFSET از total کمتر است و در int جا می‌شود.
        var lastPage = (int)(((long)total + pageSize - 1) / pageSize);
        page = Math.Min(page, lastPage);
        var offset = (int)((long)(page - 1) * pageSize);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Skip(offset)
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
/// «انبار» پیش‌فرض (تا فاز محموله منبع خودش را جایگزین کند): فرش‌های فعال (نه در سطل زباله) که نه فروخته شده‌اند و نه
/// صادر. تخته‌ها Σ تعداد است و متراژ Σ ستون تولیدشدهٔ <c>TotalAreaM2</c> (<c>round(L×W, 2) × Quantity</c>، قاعدهٔ دسته
/// D.3)؛ پیش‌تر هر ردیف یک تخته و مساحت یک تخته شمرده می‌شد و دستهٔ ۵۰تایی ۰٫۴۰×۰٫۴۰ به‌جای «۵۰ تخته، ۸ متر» «۱ تخته، ۰٫۱۶
/// متر» در کارت خانه می‌آمد.
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
                Pieces = g.Sum(r => r.Quantity),
                Area = g.Sum(r => r.TotalAreaM2)
            })
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? HomeStock.Empty : new HomeStock(rows[0].Pieces, rows[0].Area);
    }
}
