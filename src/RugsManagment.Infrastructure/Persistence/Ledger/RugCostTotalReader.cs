using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// پیاده‌سازی <see cref="IRugCostTotalReader"/>: حافظهٔ درون‌درخواستیِ ردیف‌های <c>RugCostTotal</c> (بی‌ردیابی) و رقم اعشار
/// هر کارگاه. ردیف مشتق‌شده هرگز ردیابی نمی‌شود (D.4 گام ۸)، پس عددی که این‌جا هست همان است که دیتابیس دارد یا پاس همین
/// درخواست تازه نوشته. فیلتر سطل زباله عمداً نادیده گرفته می‌شود: شناسهٔ فرش معلوم است و صفحه‌ها خودشان تصمیم می‌گیرند.
/// </summary>
public sealed class RugCostTotalReader(AppDbContext db) : IRugCostTotalReader
{
    private readonly Dictionary<Guid, RugCostTotalSnapshot?> _totals = [];
    private readonly HashSet<Guid> _fullTenants = [];
    private readonly Dictionary<Guid, int> _scales = [];

    public RugCostTotalSnapshot? Find(Guid tenantId, Guid rugId)
    {
        if (_totals.TryGetValue(rugId, out var cached))
            return cached;
        if (_fullTenants.Contains(tenantId))
            return null;

        // همگام: فقط وقتی بارگذاری‌ها از پیش نگذاشته‌اند (مثلاً فرشی که همین الان در حافظه ساخته شده)
        var row = Source().Where(t => t.RugId == rugId).Select(Projection).FirstOrDefault();
        _totals[rugId] = row;
        return row;
    }

    public async Task PreloadAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default)
    {
        var missing = rugIds.Where(id => !_totals.ContainsKey(id)).Distinct().ToList();
        if (missing.Count == 0)
            return;

        var rows = await Source().Where(t => missing.Contains(t.RugId)).Select(Projection).ToListAsync(cancellationToken);
        var byId = rows.ToDictionary(r => r.RugId);
        foreach (var id in missing)
            _totals[id] = byId.GetValueOrDefault(id);
    }

    public async Task PreloadTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_fullTenants.Contains(tenantId))
            return;

        var rows = await Source()
            .Where(t => t.TenantId == tenantId)
            .Select(Projection)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
            _totals[row.RugId] = row;
        _fullTenants.Add(tenantId);
    }

    public int MoneyScale(Guid tenantId)
    {
        if (_scales.TryGetValue(tenantId, out var scale))
            return scale;

        scale = db.Set<TenantPreferences>()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => (int?)p.MoneyScale)
            .FirstOrDefault() ?? 0;
        _scales[tenantId] = scale;
        return scale;
    }

    public async Task<int> MoneyScaleAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_scales.TryGetValue(tenantId, out var scale))
            return scale;

        scale = await db.Set<TenantPreferences>()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => (int?)p.MoneyScale)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
        _scales[tenantId] = scale;
        return scale;
    }

    public void Invalidate(IEnumerable<Guid> rugIds)
    {
        foreach (var id in rugIds)
            _totals.Remove(id);
        // فرشی که از «همهٔ کارگاه» برداشته شد دیگر از آن فهرست نیست
        _fullTenants.Clear();
    }

    public void Clear()
    {
        _totals.Clear();
        _fullTenants.Clear();
        _scales.Clear();
    }

    /// <summary>
    /// فیلتر همیشه پیش از projection: EF عضوِ record ساخته‌شده در Select را (سازندهٔ موقعیتی) به ستون برنمی‌گرداند و
    /// <c>Where</c> بعد از آن ترجمه نمی‌شود.
    /// </summary>
    private IQueryable<RugCostTotal> Source()
        => db.Set<RugCostTotal>().IgnoreQueryFilters().AsNoTracking();

    private static readonly System.Linq.Expressions.Expression<Func<RugCostTotal, RugCostTotalSnapshot>> Projection =
        t => new RugCostTotalSnapshot(
            t.RugId, t.PurchaseCost, t.StepCost, t.ExpenseCost, t.Total, t.CostPerM2, t.CostPerPiece,
            t.ExportRevenueShare, t.LedgerVersion);
}
