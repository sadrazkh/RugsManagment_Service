using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

public sealed class CostEntryRepository(AppDbContext db) : ICostEntryRepository, ICostSplitRepository
{
    public async Task<IReadOnlyList<CostEntryTarget>> RugTargetsAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
        => await db.Set<CostEntryTarget>().AsNoTracking().Where(t => t.TenantId == tenantId && t.RugId == rugId && t.Entry.DeletedAt == null).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<CostEntryPin>> PinsTrackedAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
        => await db.Set<CostEntryPin>().Where(p => p.TenantId == tenantId && p.RugId == rugId && p.Entry.DeletedAt == null).ToListAsync(cancellationToken);
    private IQueryable<CostEntry> Entries(Guid tenant) => db.Set<CostEntry>().Where(e => e.TenantId == tenant);
    private IQueryable<Rug> Rugs(Guid tenant) => db.Rugs.IgnoreQueryFilters().Where(r => r.TenantId == tenant);
    public Task<CostEntry?> GetTrackedAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default)
        => Entries(tenantId).Include(e => e.Targets).Include(e => e.Pins).AsSplitQuery().FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);
    public async Task AddAsync(CostEntry entry, CancellationToken cancellationToken = default) => await db.Set<CostEntry>().AddAsync(entry, cancellationToken);
    public async Task AddTargetAsync(CostEntryTarget target, CancellationToken cancellationToken = default) => await db.Set<CostEntryTarget>().AddAsync(target, cancellationToken);
    public async Task AddPinAsync(CostEntryPin pin, CancellationToken cancellationToken = default) => await db.Set<CostEntryPin>().AddAsync(pin, cancellationToken);
    public void RemoveTarget(CostEntryTarget target) => db.Remove(target);
    public void RemovePin(CostEntryPin pin) => db.Remove(pin);
    public Task<CostCategory?> GetCategoryTrackedAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default)
        => db.Set<CostCategory>().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, cancellationToken);
    public async Task AddCategoryAsync(CostCategory category, CancellationToken cancellationToken = default) => await db.Set<CostCategory>().AddAsync(category, cancellationToken);
    public void RemoveCategory(CostCategory category) => db.Remove(category);
    public Task<Guid?> FindByClientKeyAsync(Guid tenantId, Guid clientKey, CancellationToken cancellationToken = default)
        => Entries(tenantId).Where(e => e.ClientKey == clientKey).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(cancellationToken);
    public Task<CostCategory?> GetCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default)
        => db.Set<CostCategory>().AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, cancellationToken);
    public async Task<IReadOnlyList<CostCategoryDto>> ListCategoriesAsync(Guid tenantId, CostGroup? group, bool includeInactive, CancellationToken cancellationToken = default)
        => await db.Set<CostCategory>().AsNoTracking().Where(c => c.TenantId == tenantId && (group == null || c.Group == group) && (includeInactive || c.IsActive))
            .OrderBy(c => c.Group).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new CostCategoryDto(c.Id, c.Name, c.Group, c.Icon, c.SortOrder, c.IsActive, c.DefaultBasis, c.DefaultRate,
                c.DefaultCurrency, c.DefaultMethod, c.DefaultAreaBasis, c.DefaultScope, c.ShowInIntake, c.StepTypeId, c.SeedKey, EF.Property<uint>(c, "xmin"))).ToListAsync(cancellationToken);
    public async Task<bool> CategoryUsedAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default)
        => await Entries(tenantId).AnyAsync(e => e.CategoryId == categoryId, cancellationToken)
            || await db.Set<RugPurchaseLine>().IgnoreQueryFilters().AnyAsync(l => l.TenantId == tenantId && l.CategoryId == categoryId, cancellationToken);
    public Task<bool> CategoryNameTakenAsync(Guid tenantId, CostGroup group, string name, Guid? exceptId, CancellationToken cancellationToken = default)
        => db.Set<CostCategory>().AnyAsync(c => c.TenantId == tenantId && c.Group == group && c.Name == name && c.Id != exceptId, cancellationToken);
    public async Task<int> MaxCategorySortAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await db.Set<CostCategory>().Where(c => c.TenantId == tenantId).Select(c => (int?)c.SortOrder).MaxAsync(cancellationToken) ?? 0;
    public Task<string?> ProviderNameAsync(Guid tenantId, Guid providerId, CancellationToken cancellationToken = default)
        => db.ServiceProviders.Where(p => p.TenantId == tenantId && p.Id == providerId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken);
    public Task<bool> StepTypeVisibleAsync(Guid tenantId, Guid stepTypeId, CancellationToken cancellationToken = default)
        => db.ProcessStepTypes.AnyAsync(t => t.Id == stepTypeId && (t.TenantId == null || t.TenantId == tenantId), cancellationToken);
    public Task<CostShipmentRef?> GetShipmentAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => db.Set<Shipment>().Where(s => s.TenantId == tenantId && s.Id == shipmentId).Select(s => new CostShipmentRef(s.Id, s.Number, s.Status, s.FinalizedAt)).FirstOrDefaultAsync(cancellationToken);
    public Task<CostBatchRef?> GetBatchAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default)
        => db.RugBatches.Where(b => b.TenantId == tenantId && b.Id == batchId).Select(b => new CostBatchRef(b.Id, b.Number, b.SellerName)).FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<CostBaleRef>> GetBalesAsync(Guid tenantId, IReadOnlyCollection<Guid> baleIds, CancellationToken cancellationToken = default)
        => await db.Set<Bale>().Where(b => b.TenantId == tenantId && baleIds.Contains(b.Id)).Select(b => new CostBaleRef(b.Id, b.ShipmentId, b.Number)).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<CostRugRef>> GetRugsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default)
        => await Refs(Rugs(tenantId).Where(r => rugIds.Contains(r.Id))).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Guid>> BatchMemberIdsAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default)
        => await Rugs(tenantId).Where(r => r.DeletedAt == null && r.BatchId == batchId).Select(r => r.Id).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<AllocationMember>> MembersAsync(Guid tenantId, CostScopeSpec scope, Guid? stepTypeId, DateTimeOffset createdAt, CancellationToken cancellationToken = default)
        => (await CostMembership.LoadAsync(db, tenantId, [scope], cancellationToken)).Where(r => r.Matches(scope)).Select(r => r.Member(stepTypeId, createdAt)).ToList();
    public async Task<IReadOnlyDictionary<Guid, string?>> SerialsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default)
        => await Rugs(tenantId).Where(r => rugIds.Contains(r.Id)).Select(r => new { r.Id, r.SerialNo }).ToDictionaryAsync(r => r.Id, r => r.SerialNo, cancellationToken);
    public async Task<CostEntryDetail?> GetDetailAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var row = await Entries(tenantId).AsNoTracking().Where(e => e.Id == entryId)
            .Select(e => new { Entry = e, Name = e.Category.Name, e.Category.Icon, e.Category.Group, Version = EF.Property<uint>(e, "xmin") }).FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var targets = await db.Set<CostEntryTarget>().AsNoTracking().Where(t => t.TenantId == tenantId && t.EntryId == entryId).ToListAsync(cancellationToken);
        var pins = await db.Set<CostEntryPin>().Where(p => p.TenantId == tenantId && p.EntryId == entryId).Select(p => new PinDto(p.RugId, p.Amount)).ToListAsync(cancellationToken);
        var result = await db.Set<CostEntryResult>().AsNoTracking().FirstOrDefaultAsync(r => r.TenantId == tenantId && r.EntryId == entryId, cancellationToken);
        var shares = await db.Set<CostAllocation>().IgnoreQueryFilters().AsNoTracking().Where(a => a.TenantId == tenantId && a.EntryId == entryId && a.Rug.TenantId == tenantId)
            .OrderBy(a => a.Rug.SerialSort == null).ThenBy(a => a.Rug.SerialSort).ThenBy(a => a.Rug.SerialNo)
            .Select(a => new CostShareRow(a.RugId, a.Rug.Sku, a.Rug.SerialNo, a.Rug.SerialSort, a.Rug.Quantity, a.Amount, a.WeightUnits, a.IsPinned)).ToListAsync(cancellationToken);
        var provider = row.Entry.ServiceProviderId is { } pid ? await ProviderNameAsync(tenantId, pid, cancellationToken) : null;
        if (row.Entry.IsLive)
        {
            var settings = await TenantCostSettingsAsync(tenantId, cancellationToken);
            var scope = new CostScopeSpec(row.Entry.Scope, row.Group, targets.Where(t => t.RugId != null).Select(t => t.RugId!.Value).ToArray(),
                targets.Where(t => t.BaleId != null).Select(t => t.BaleId!.Value).ToArray(), row.Entry.ShipmentId, row.Entry.BatchId);
            var members = await MembersAsync(tenantId, scope, null, row.Entry.CreatedAt, cancellationToken);
            row.Entry.Amount = CostAllocator.LiveTotal(row.Entry.Basis, row.Entry.Rate ?? 0m, row.Entry.Currency, row.Entry.FxRate, row.Entry.AreaBasis, members, settings.Currency, settings.MoneyScale).Original;
        }
        return new CostEntryDetail(row.Entry, row.Name, row.Icon, row.Group, provider, row.Version,
            targets.Where(t => t.RugId != null).Select(t => t.RugId!.Value).ToList(), targets.Where(t => t.BaleId != null).Select(t => t.BaleId!.Value).ToList(), pins, result, shares);
    }
    public async Task<PagedResult<CostEntryListRow>> ListAsync(Guid tenantId, CostEntryQuery query, CancellationToken cancellationToken = default)
    {
        query = query.Sanitized();
        var q = Entries(tenantId).AsNoTracking().Where(e => query.IncludeDeleted || e.DeletedAt == null);
        if (query.CategoryId is { } category) q = q.Where(e => e.CategoryId == category);
        if (query.From is { } from) q = q.Where(e => e.Date >= from);
        if (query.To is { } to) q = q.Where(e => e.Date <= to);
        var allocations = db.Set<CostAllocation>(); var results = db.Set<CostEntryResult>();
        if (query.RugId is { } rug) q = q.Where(e => e.Targets.Any(t => t.RugId == rug) || allocations.Any(a => a.EntryId == e.Id && a.RugId == rug));
        if (query.BatchId is { } batch) q = q.Where(e => e.BatchId == batch || allocations.Any(a => a.EntryId == e.Id && a.Rug.BatchId == batch));
        if (query.ShipmentId is { } ship) q = q.Where(e => e.ShipmentId == ship || e.Targets.Any(t => t.BaleId != null && db.Set<Bale>().Any(b => b.Id == t.BaleId && b.ShipmentId == ship)) || allocations.Any(a => a.EntryId == e.Id && db.Set<ShipmentLine>().Any(l => l.RugId == a.RugId && l.ShipmentId == ship && l.TenantId == tenantId)));
        if (query.State is { } state) q = q.Where(e => results.Any(r => r.EntryId == e.Id && r.State == state));
        if (query.NotFullyAllocated) q = q.Where(e => !results.Any(r => r.EntryId == e.Id && r.State == AllocationState.Allocated));
        var count = await q.CountAsync(cancellationToken);
        var rows = await q.OrderByDescending(e => e.Date).ThenByDescending(e => e.Number).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(e => new { Entry = e, Category = e.Category.Name, e.Category.Icon, e.Category.Group, Version = EF.Property<uint>(e, "xmin") }).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Entry.Id).ToArray();
        var resultRows = await results.AsNoTracking().Where(r => r.TenantId == tenantId && ids.Contains(r.EntryId)).ToDictionaryAsync(r => r.EntryId, cancellationToken);
        var targetRows = await db.Set<CostEntryTarget>().AsNoTracking().Where(t => t.TenantId == tenantId && ids.Contains(t.EntryId)).Select(t => new { t.EntryId, t.RugId, t.BaleId, Number = t.BaleId == null ? (int?)null : db.Set<Bale>().Where(b => b.Id == t.BaleId && b.TenantId == tenantId).Select(b => b.Number).FirstOrDefault() }).ToListAsync(cancellationToken);
        var shipIds = rows.Where(r => r.Entry.ShipmentId != null).Select(r => r.Entry.ShipmentId!.Value).ToArray();
        var batchIds = rows.Where(r => r.Entry.BatchId != null).Select(r => r.Entry.BatchId!.Value).ToArray();
        var ships = await db.Set<Shipment>().Where(s => s.TenantId == tenantId && shipIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Number, cancellationToken);
        var batches = await db.RugBatches.Where(b => b.TenantId == tenantId && batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Number, cancellationToken);
        var liveScopes = rows.Where(r => r.Entry.IsLive).ToDictionary(r => r.Entry.Id, r => new CostScopeSpec(r.Entry.Scope, r.Group,
            targetRows.Where(t => t.EntryId == r.Entry.Id && t.RugId != null).Select(t => t.RugId!.Value).ToArray(),
            targetRows.Where(t => t.EntryId == r.Entry.Id && t.BaleId != null).Select(t => t.BaleId!.Value).ToArray(), r.Entry.ShipmentId, r.Entry.BatchId));
        if (liveScopes.Count > 0)
        {
            var members = await CostMembership.LoadAsync(db, tenantId, liveScopes.Values.ToList(), cancellationToken);
            var settings = await TenantCostSettingsAsync(tenantId, cancellationToken);
            foreach (var row in rows.Where(r => r.Entry.IsLive))
            {
                var e = row.Entry;
                e.Amount = CostAllocator.LiveTotal(e.Basis, e.Rate ?? 0m, e.Currency, e.FxRate, e.AreaBasis,
                    members.Where(m => m.Matches(liveScopes[e.Id])).Select(m => m.Member(null, e.CreatedAt)), settings.Currency, settings.MoneyScale).Original;
            }
        }
        return new PagedResult<CostEntryListRow>(rows.Select(r => { var e = r.Entry; return new CostEntryListRow(e.Id, e.Number, e.Date, r.Category, r.Icon, r.Group, e.Description, e.Scope, e.Basis, e.Amount, e.Currency, e.BaseAmount, e.FrozenAt, e.DeletedAt != null, r.Version, e.ScopeNote,
            e.ShipmentId is { } sid && ships.TryGetValue(sid, out var sn) ? sn : null, e.BatchId is { } bid && batches.TryGetValue(bid, out var bn) ? bn : null,
            targetRows.Where(t => t.EntryId == e.Id && t.Number != null).Select(t => t.Number!.Value).Order().ToList(), targetRows.Count(t => t.EntryId == e.Id && t.RugId != null), resultRows.GetValueOrDefault(e.Id)); }).ToList(), count, query.Page, query.PageSize);
    }
    public async Task<IReadOnlyList<AllocationLineDto>> AllocationsOfRugAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
        => await db.Set<CostAllocation>().AsNoTracking().Where(a => a.TenantId == tenantId && a.RugId == rugId && a.Entry.DeletedAt == null)
            .OrderBy(a => a.Entry.Date).ThenBy(a => a.Entry.Number)
            .Select(a => new AllocationLineDto(a.EntryId, a.Entry.Number, a.Entry.Category.Name, a.Entry.Category.Group, a.Entry.Date, a.Amount, a.WeightUnits,
                db.Set<CostEntryResult>().Where(r => r.EntryId == a.EntryId).Select(r => r.TotalWeightUnits).FirstOrDefault(), a.Entry.Method, a.IsPinned,
                db.Set<CostEntryResult>().Where(r => r.EntryId == a.EntryId).Select(r => r.BaseAmount).FirstOrDefault())).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<CostRugRef>> InStockRugsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Refs(Rugs(tenantId).Where(r => r.DeletedAt == null && r.Sale == null && !db.Set<ShipmentLine>().Any(l => l.TenantId == tenantId && l.RugId == r.Id))).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<CostRugRef>> RugsBySerialAsync(Guid tenantId, string? exactText, int? from, int? to, CancellationToken cancellationToken = default)
        => await Refs(Rugs(tenantId).Where(r => r.DeletedAt == null && ((exactText != null && r.SerialNo == exactText) || (from != null && to != null && r.SerialFrom <= to && r.SerialTo >= from)))).ToListAsync(cancellationToken);
    public Task<int> CountNotFullyAllocatedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Entries(tenantId).CountAsync(e => e.DeletedAt == null && !db.Set<CostEntryResult>().Any(r => r.EntryId == e.Id && r.State == AllocationState.Allocated), cancellationToken);
    public async Task<ShipmentMoneyData?> ShipmentMoneyAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var s = await db.Set<Shipment>().AsNoTracking().Where(s => s.TenantId == tenantId && s.Id == shipmentId).Select(s => new { Shipment = s, Version = EF.Property<uint>(s, "xmin") }).FirstOrDefaultAsync(cancellationToken);
        if (s is null) return null;
        var lines = await db.Set<ShipmentLine>().AsNoTracking().Where(l => l.TenantId == tenantId && l.ShipmentId == shipmentId)
            .Select(l => new ShipmentMoneyLine(l.RugId, l.Rug.SerialNo, l.Rug.Sku, l.Rug.SerialSort, l.Rug.Quantity, l.Value == null ? 0m : l.Value.LineAreaM2, l.Value == null ? null : l.Value.LineUsd,
                l.Rug.CostTotal == null ? 0m : l.Rug.CostTotal.PurchaseCost, l.Rug.CostTotal == null ? 0m : l.Rug.CostTotal.StepCost, l.Rug.CostTotal == null ? 0m : l.Rug.CostTotal.ExpenseCost, l.Rug.CostTotal == null ? 0m : l.Rug.CostTotal.Total)).ToListAsync(cancellationToken);
        var unallocated = await ListAsync(tenantId, new CostEntryQuery { ShipmentId = shipmentId, NotFullyAllocated = true, PageSize = 1 }, cancellationToken);
        return new ShipmentMoneyData(s.Shipment.Id, s.Shipment.Number, s.Shipment.Status, s.Version, s.Shipment.RevenueAmount, s.Shipment.RevenueCurrency, s.Shipment.RevenueFx, lines, unallocated.TotalCount);
    }
    public async Task<CostTenantSettings> TenantCostSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var currency = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Currency).FirstOrDefaultAsync(cancellationToken);
        var prefs = await db.Set<TenantPreferences>().AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        return new CostTenantSettings(currency, prefs?.MoneyScale ?? 0, prefs?.BaleTerm ?? BaleTerm.Negleh);
    }
    private static IQueryable<CostRugRef> Refs(IQueryable<Rug> q)
        => q.AsNoTracking().OrderBy(r => r.SerialSort == null).ThenBy(r => r.SerialSort).ThenBy(r => r.SerialNo).Select(r => new CostRugRef(r.Id, r.Sku, r.SerialNo, r.SerialSort, r.Quantity, r.TotalAreaM2, r.DeletedAt != null));
}
