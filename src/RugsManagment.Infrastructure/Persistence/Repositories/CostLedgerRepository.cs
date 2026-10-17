using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Application.Features.Costs.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>فقط جدول‌های مشتق‌شده را در همان تراکنش پاس می‌نویسد؛ نسخهٔ ثبت هزینه و فرش دست‌نخورده می‌ماند.</summary>
public sealed class CostLedgerRepository(AppDbContext db) : ICostLedgerRepository, ICostSourceProvider, IStoredMoneyProbe
{
    public async Task<IReadOnlyList<Guid>> FindEntriesAsync(Guid tenantId, EntryCriteria c, CancellationToken cancellationToken)
    {
        if (c.IsEmpty) return [];
        // سهم‌های قبلی هم عضو شمرده می‌شوند تا خروج از دامنه، سهم قدیمی را پاک کند.
        const string membership = """
            SELECT r."Id" FROM "Rugs" r
            LEFT JOIN "ShipmentLines" l ON l."RugId" = r."Id" AND l."TenantId" = @tenant
            WHERE r."TenantId" = @tenant AND (
                EXISTS(SELECT 1 FROM "CostEntryTargets" t WHERE t."EntryId" = e."Id" AND t."TenantId" = @tenant
                    AND (t."RugId" = r."Id" OR (e."Scope" = 1 AND t."BaleId" = l."BaleId")))
                OR (e."Scope" = 2 AND e."ShipmentId" = l."ShipmentId")
                OR (e."Scope" = 3 AND cat."Group" = 1 AND e."BatchId" = r."BatchId")
                OR EXISTS(SELECT 1 FROM "CostAllocations" a WHERE a."EntryId" = e."Id" AND a."RugId" = r."Id" AND a."TenantId" = @tenant))
            """;
        var sql = $"""
            SELECT e."Id" FROM "CostEntries" e JOIN "CostCategories" cat ON cat."Id" = e."CategoryId" AND cat."TenantId" = @tenant
            WHERE e."TenantId" = @tenant AND (
                e."Id" = ANY(@entries) OR e."CategoryId" = ANY(@categories)
                OR e."BatchId" = ANY(@batches) OR e."ShipmentId" = ANY(@ships)
                OR EXISTS(SELECT 1 FROM "CostEntryTargets" t LEFT JOIN "Bales" b ON b."Id" = t."BaleId"
                    WHERE t."EntryId" = e."Id" AND t."TenantId" = @tenant AND (t."BaleId" = ANY(@bales) OR b."ShipmentId" = ANY(@ships)))
                OR EXISTS(SELECT 1 FROM ({membership}) m WHERE m."Id" = ANY(@members)
                    OR (cat."Group" = 0 AND m."Id" = ANY(@purchase))
                    OR (cat."StepTypeId" IS NOT NULL AND m."Id" = ANY(@steps))
                    OR (e."Method" = 3 AND m."Id" = ANY(@declared))
                    OR (e."Method" = 4 AND e."DeletedAt" IS NULL AND m."Id" = ANY(@cost))))
            ORDER BY e."Id"
            """;
        return await ShipmentSql.QueryAsync(db, sql, r => r.GetGuid(0), cancellationToken,
            ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuids("entries", c.EntryIds), ShipmentSql.Uuids("categories", c.Categories),
            ShipmentSql.Uuids("batches", c.Batches), ShipmentSql.Uuids("ships", c.Shipments), ShipmentSql.Uuids("bales", c.Bales),
            ShipmentSql.Uuids("members", c.MemberRugs), ShipmentSql.Uuids("purchase", c.PurchaseGroupRugs), ShipmentSql.Uuids("steps", c.StepRugs),
            ShipmentSql.Uuids("declared", c.DeclaredRugs), ShipmentSql.Uuids("cost", c.PurchaseCostRugs));
    }

    public async Task<IReadOnlyList<EntryInput>> LoadAsync(Guid tenantId, IReadOnlyCollection<Guid>? entryIds, CancellationToken cancellationToken)
    {
        if (entryIds is { Count: 0 }) return [];
        var q = db.Set<CostEntry>().AsNoTracking().Where(e => e.TenantId == tenantId);
        if (entryIds != null) q = q.Where(e => entryIds.Contains(e.Id));
        var entries = await q.Include(e => e.Category).Include(e => e.Targets).Include(e => e.Pins).AsSplitQuery().ToListAsync(cancellationToken);
        if (entries.Count == 0) return [];
        var ids = entries.Select(e => e.Id).ToArray();
        var stored = (await db.Set<CostAllocation>().IgnoreQueryFilters().AsNoTracking().Where(a => a.TenantId == tenantId && ids.Contains(a.EntryId)).ToListAsync(cancellationToken)).ToLookup(a => a.EntryId);
        var results = await db.Set<CostEntryResult>().AsNoTracking().Where(r => r.TenantId == tenantId && ids.Contains(r.EntryId)).ToDictionaryAsync(r => r.EntryId, cancellationToken);
        var scopes = entries.ToDictionary(e => e.Id, ScopeOf);
        var members = await CostMembership.LoadAsync(db, tenantId, scopes.Values.ToList(), cancellationToken);
        var pinIds = entries.SelectMany(e => e.Pins).Select(p => p.RugId).Distinct().ToArray();
        var serials = await db.Rugs.IgnoreQueryFilters().Where(r => r.TenantId == tenantId && pinIds.Contains(r.Id)).Select(r => new { r.Id, r.SerialNo }).ToDictionaryAsync(r => r.Id, r => r.SerialNo, cancellationToken);
        var shipIds = entries.Where(e => e.ShipmentId != null).Select(e => e.ShipmentId!.Value).Distinct().ToArray();
        var finalized = await db.Set<Shipment>().AsNoTracking().Where(s => s.TenantId == tenantId && shipIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.FinalizedAt, cancellationToken);
        return entries.Select(e =>
        {
            var s = scopes[e.Id];
            var deleted = (e.Scope == CostScope.Shipment && e.ShipmentId == null)
                || (e.Scope == CostScope.IntakeBatch && e.Category.Group == CostGroup.Expense && e.BatchId == null)
                || (e.Scope == CostScope.Bale && s.BaleIds.Count == 0);
            var late = e.ShipmentId is { } ship && finalized.TryGetValue(ship, out var f) && f is { } at && at < e.CreatedAt;
            var result = results.GetValueOrDefault(e.Id);
            return new EntryInput(e.Id, e.Number, e.DeletedAt != null, e.Method,
                new AllocationEntry(e.Category.Group, e.Basis, e.Rate, e.Amount, e.Currency, e.FxRate, e.BaseAmount, e.IsFrozen, e.AreaBasis, e.Method, deleted, late),
                members.Where(m => m.Matches(s)).Select(m => m.Member(e.Category.StepTypeId, e.CreatedAt)).ToList(),
                e.Pins.Select(p => new AllocationPin(p.RugId, p.Amount, serials.GetValueOrDefault(p.RugId))).ToList(),
                stored[e.Id].Select(a => new StoredAllocation(a.RugId, a.Amount, a.WeightUnits, a.IsPinned)).ToList(),
                result == null ? null : new StoredResult(result.Lines, result.Pieces, result.AreaM2, result.TotalWeightUnits, result.BaseAmount, result.Allocated, result.Unallocated, result.State, CostWarningJson.Parse(result.WarningsJson)));
        }).ToList();
    }

    internal static CostScopeSpec ScopeOf(CostEntry e) => new(e.Scope, e.Category.Group,
        e.Targets.Where(t => t.RugId != null).Select(t => t.RugId!.Value).ToArray(),
        e.Targets.Where(t => t.BaleId != null).Select(t => t.BaleId!.Value).ToArray(), e.ShipmentId, e.BatchId);

    public async Task<int> WriteAsync(Guid tenantId, IReadOnlyList<EntryWrite> writes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (writes.Count == 0) return 0;
        const string sql = """
            WITH removed AS (
                DELETE FROM "CostAllocations" a USING jsonb_to_recordset(@removed) AS d(entry uuid, rug uuid)
                WHERE a."TenantId" = @tenant AND a."EntryId" = d.entry AND a."RugId" = d.rug RETURNING 1),
            shares AS (
                INSERT INTO "CostAllocations" ("EntryId", "RugId", "TenantId", "Amount", "WeightUnits", "IsPinned")
                SELECT x.entry, x.rug, @tenant, x.amount, x.weight, x.pinned
                FROM jsonb_to_recordset(@shares) AS x(entry uuid, rug uuid, amount numeric, weight bigint, pinned boolean)
                JOIN "CostEntries" e ON e."Id" = x.entry AND e."TenantId" = @tenant
                JOIN "Rugs" r ON r."Id" = x.rug AND r."TenantId" = @tenant
                ON CONFLICT ("EntryId", "RugId") DO UPDATE SET "Amount" = EXCLUDED."Amount", "WeightUnits" = EXCLUDED."WeightUnits", "IsPinned" = EXCLUDED."IsPinned"
                RETURNING 1),
            results AS (
                INSERT INTO "CostEntryResults" ("EntryId", "TenantId", "Lines", "Pieces", "AreaM2", "TotalWeightUnits", "BaseAmount", "Allocated", "Unallocated", "State", "WarningsJson", "ComputedAt")
                SELECT x.entry, @tenant, x.lines, x.pieces, x.area, x.weight, x.base, x.allocated, x.unallocated, x.state, x.warnings, @now
                FROM jsonb_to_recordset(@results) AS x(entry uuid, lines int, pieces int, area numeric, weight bigint, base numeric, allocated numeric, unallocated numeric, state int, warnings jsonb)
                JOIN "CostEntries" e ON e."Id" = x.entry AND e."TenantId" = @tenant
                ON CONFLICT ("EntryId") DO UPDATE SET "Lines" = EXCLUDED."Lines", "Pieces" = EXCLUDED."Pieces", "AreaM2" = EXCLUDED."AreaM2",
                    "TotalWeightUnits" = EXCLUDED."TotalWeightUnits", "BaseAmount" = EXCLUDED."BaseAmount", "Allocated" = EXCLUDED."Allocated",
                    "Unallocated" = EXCLUDED."Unallocated", "State" = EXCLUDED."State", "WarningsJson" = EXCLUDED."WarningsJson", "ComputedAt" = EXCLUDED."ComputedAt"
                RETURNING 1)
            SELECT ((SELECT count(*) FROM removed) + (SELECT count(*) FROM shares) + (SELECT count(*) FROM results))::int
            """;
        var removed = writes.SelectMany(w => w.RemovedRugs.Select(r => new { entry = w.EntryId, rug = r }));
        var shares = writes.SelectMany(w => w.Upserts.Select(s => new { entry = w.EntryId, rug = s.RugId, amount = s.Amount, weight = s.WeightUnits, pinned = s.IsPinned }));
        var results = writes.Where(w => w.Result != null).Select(w => new { entry = w.EntryId, lines = w.Result!.Lines, pieces = w.Result.Pieces, area = w.Result.AreaM2,
            weight = w.Result.TotalWeightUnits, @base = w.Result.BaseAmount, allocated = w.Result.Allocated, unallocated = w.Result.Unallocated, state = (int)w.Result.State,
            warnings = JsonSerializer.Deserialize<JsonElement>(CostWarningJson.Serialize(w.Result.Warnings)) });
        // دستور نوشتن عمداً در LedgerRepository می‌ماند (فهرست سفید آزمون معماری).
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = ShipmentSql.CreateCommand(db, sql, [ShipmentSql.Uuid("tenant", tenantId),
                new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now.ToUniversalTime() }, Json("removed", removed), Json("shares", shares), Json("results", results)]);
            return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    private static NpgsqlParameter Json<T>(string name, T value) => new(name, NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(value) };

    public async Task<IReadOnlyList<Guid>> EntriesLosingTargetsAsync(Guid tenantId, IReadOnlyCollection<Guid> shipmentIds, IReadOnlyCollection<Guid> baleIds, IReadOnlyCollection<Guid> batchIds, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
        => await db.Set<CostEntry>().Where(e => e.TenantId == tenantId && ((e.ShipmentId != null && shipmentIds.Contains(e.ShipmentId.Value))
            || (e.BatchId != null && batchIds.Contains(e.BatchId.Value))
            || e.Targets.Any(t => (t.RugId != null && rugIds.Contains(t.RugId.Value)) || (t.BaleId != null && (baleIds.Contains(t.BaleId.Value) || db.Set<Bale>().Any(b => b.Id == t.BaleId && b.TenantId == tenantId && shipmentIds.Contains(b.ShipmentId)))))))
            .Select(e => e.Id).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Guid>> AllocatedRugsAsync(Guid tenantId, IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken)
        => await db.Set<CostAllocation>().IgnoreQueryFilters().Where(a => a.TenantId == tenantId && entryIds.Contains(a.EntryId)).Select(a => a.RugId).Distinct().ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<CategoryUse>> CategoryUsesAsync(Guid tenantId, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
        => await db.Set<CostCategory>().Where(c => c.TenantId == tenantId && categoryIds.Contains(c.Id)).Select(c => new CategoryUse(c.Id, c.Group,
            db.Set<CostEntry>().Any(e => e.TenantId == tenantId && e.CategoryId == c.Id) || db.Set<RugPurchaseLine>().IgnoreQueryFilters().Any(l => l.TenantId == tenantId && l.CategoryId == c.Id))).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Guid>> RevenueRugsAsync(Guid tenantId, IReadOnlyCollection<Guid> revenueShipments, IReadOnlyCollection<Guid> lineUsdChangedRugs, CancellationToken cancellationToken)
    {
        var current = await db.Set<ShipmentLine>().Where(l => l.TenantId == tenantId && (revenueShipments.Contains(l.ShipmentId)
            || (l.Shipment.RevenueAmount != null && db.Set<ShipmentLine>().Any(other => other.TenantId == tenantId && other.ShipmentId == l.ShipmentId && lineUsdChangedRugs.Contains(other.RugId)))))
            .Select(l => l.RugId).ToListAsync(cancellationToken);
        return current.Union(lineUsdChangedRugs).ToList();
    }
    public Task<bool> AnyEntriesAsync(Guid tenantId, CancellationToken cancellationToken)
        => db.Set<CostEntry>().AnyAsync(e => e.TenantId == tenantId, cancellationToken);
    public Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken) => AnyEntriesAsync(tenantId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, RugCostSources>> GetAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return new Dictionary<Guid, RugCostSources>();
        // مرحله‌های ۵۰۰ و ۱۰۰۰ هر دو این منبع را می‌خوانند. یک رفت‌وبرگشت برای سهم‌ها و تمام هم‌محموله‌ای‌های درآمدی؛
        // خواندن ترجیحات در همان شاخهٔ درآمد است و تقسیم دقیق LR همچنان فقط در CostAllocator می‌ماند.
        const string sql = """
            SELECT 0 AS kind, a."RugId", c."Group", sum(a."Amount") AS amount,
                NULL::uuid AS shipment, NULL::bigint AS serial_sort, NULL::numeric AS usd,
                NULL::numeric AS revenue_amount, NULL::integer AS currency, NULL::numeric AS fx,
                0 AS tenant_currency, 0 AS scale
            FROM "CostAllocations" a
            JOIN "CostEntries" e ON e."Id" = a."EntryId" AND e."TenantId" = @tenant AND e."DeletedAt" IS NULL
            JOIN "CostCategories" c ON c."Id" = e."CategoryId" AND c."TenantId" = @tenant
            JOIN "Rugs" r ON r."Id" = a."RugId" AND r."TenantId" = @tenant AND r."DeletedAt" IS NULL
            WHERE a."TenantId" = @tenant AND a."RugId" = ANY(@rugs)
            GROUP BY a."RugId", c."Group"
            UNION ALL
            SELECT 1, l."RugId", 0, 0::numeric, l."ShipmentId", r."SerialSort", v."LineUsd",
                s."RevenueAmount", s."RevenueCurrency", s."RevenueFx", t."Currency", COALESCE(p."MoneyScale", 0)::integer
            FROM "Shipments" s
            JOIN "ShipmentLines" l ON l."ShipmentId" = s."Id" AND l."TenantId" = @tenant
            JOIN "Rugs" r ON r."Id" = l."RugId" AND r."TenantId" = @tenant AND r."DeletedAt" IS NULL
            JOIN "Tenants" t ON t."Id" = @tenant
            LEFT JOIN "TenantPreferences" p ON p."TenantId" = @tenant
            LEFT JOIN "ShipmentLineValues" v ON v."LineId" = l."Id" AND v."TenantId" = @tenant
            WHERE s."TenantId" = @tenant AND s."RevenueAmount" IS NOT NULL
                AND s."Id" IN (SELECT x."ShipmentId" FROM "ShipmentLines" x WHERE x."TenantId" = @tenant AND x."RugId" = ANY(@rugs))
            """;
        var rows = await ShipmentSql.QueryAsync(db, sql, r => new SourceRow(r.GetInt32(0), r.GetGuid(1), (CostGroup)r.GetInt32(2),
            r.GetDecimal(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.IsDBNull(5) ? null : r.GetInt64(5), ShipmentSql.NullableDecimal(r, 6),
            ShipmentSql.NullableDecimal(r, 7), r.IsDBNull(8) ? null : (CurrencyUnit)r.GetInt32(8), ShipmentSql.NullableDecimal(r, 9),
            (CurrencyUnit)r.GetInt32(10), r.GetInt32(11)), cancellationToken, ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuids("rugs", rugIds));
        var allocations = rows.Where(r => r.Kind == 0).ToDictionary(r => (r.RugId, r.Group), r => r.Amount);
        var requested = rugIds.ToHashSet();
        var revenue = new Dictionary<Guid, decimal>();
        foreach (var group in rows.Where(r => r.Kind == 1).GroupBy(r => r.ShipmentId))
        {
            var head = group.First();
            var total = CostAllocator.RevenueBase(head.RevenueAmount, head.RevenueCurrency, head.RevenueFx, head.TenantCurrency, head.MoneyScale);
            if (total is null) continue;
            foreach (var share in CostAllocator.RevenueShares(total.Value, head.MoneyScale, group.Select(l => new RevenueLine(l.RugId, l.Usd, l.SerialSort)).ToList()))
                if (requested.Contains(share.Key)) revenue[share.Key] = share.Value;
        }
        return rugIds.ToDictionary(id => id, id => new RugCostSources(
            allocations.GetValueOrDefault((id, CostGroup.Purchase)), allocations.GetValueOrDefault((id, CostGroup.Expense)), revenue.TryGetValue(id, out var rev) ? rev : null));
    }

    private sealed record SourceRow(int Kind, Guid RugId, CostGroup Group, decimal Amount, Guid? ShipmentId, long? SerialSort,
        decimal? Usd, decimal? RevenueAmount, CurrencyUnit? RevenueCurrency, decimal? RevenueFx, CurrencyUnit TenantCurrency, int MoneyScale);
}
