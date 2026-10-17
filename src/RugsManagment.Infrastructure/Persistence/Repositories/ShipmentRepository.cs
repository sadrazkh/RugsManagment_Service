using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Ledger;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// محموله، نگله، خط و جدول ارزش گمرکی (پیاده‌سازی <see cref="IShipmentRepository"/>). بارگذاری‌های ردیابی‌شده با EF (تا
/// تغییرشان از پاس سازگاری بگذرد)، و خواندن‌های جمعی (فهرست، نگله‌ها، ردیف‌ها، جای فرش‌ها) با SQL خواندنی روی همان اتصال و
/// تراکنش DbContext — یک دستور برای هر فهرست، با فیلتر کارگاه و فیلتر حذف نرمِ فرش. این کلاس هیچ SQL نوشتنی ندارد.
/// </summary>
public sealed class ShipmentRepository : IShipmentRepository
{
    private readonly AppDbContext _db;

    /// <param name="tracking">فقط تا ردیاب «وصل‌شده بی بارگذاری» پیش از هر بارگذاری ساخته شده باشد (مثل RugRepository).</param>
    public ShipmentRepository(AppDbContext db, LedgerTracking tracking)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        _db = db;
    }

    // ═════════════════════════════════════════════════════════════════════
    // ردیابی‌شده
    // ═════════════════════════════════════════════════════════════════════

    public Task<Shipment?> GetTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => _db.Set<Shipment>().FirstOrDefaultAsync(s => s.Id == shipmentId && s.TenantId == tenantId, cancellationToken);

    public Task<Bale?> GetBaleTrackedAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default)
        => _db.Set<Bale>().FirstOrDefaultAsync(b => b.Id == baleId && b.TenantId == tenantId, cancellationToken);

    public Task<Bale?> FindBaleByNumberTrackedAsync(Guid tenantId, Guid shipmentId, int number, CancellationToken cancellationToken = default)
        => _db.Set<Bale>().FirstOrDefaultAsync(
            b => b.TenantId == tenantId && b.ShipmentId == shipmentId && b.Number == number, cancellationToken);

    public async Task<IReadOnlyList<Bale>> ListBalesTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => await _db.Set<Bale>()
            .Where(b => b.TenantId == tenantId && b.ShipmentId == shipmentId)
            .OrderBy(b => b.Number).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

    public Task<ShipmentLine?> GetLineTrackedAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken = default)
        => _db.Set<ShipmentLine>().FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId, cancellationToken);

    public Task<ShipmentLine?> GetLineOfRugTrackedAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
        => _db.Set<ShipmentLine>()
            .Include(l => l.Shipment)
            .Include(l => l.Bale)
            .FirstOrDefaultAsync(l => l.RugId == rugId && l.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<ShipmentLine>> ListLinesOfBaleTrackedAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default)
        => await _db.Set<ShipmentLine>()
            .Where(l => l.TenantId == tenantId && l.BaleId == baleId)
            .OrderBy(l => l.Seq).ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Rug>> ListLineRugsTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        // یک پرس‌وجوی تقسیم‌شده برای همه (فرش‌ها با فروش، و مراحل)، نه یکی برای هر فرش؛ شرط «خط در این محموله» زیرپرس‌وجوی
        // همبسته است تا فهرست شناسه‌ها یک رفت‌وبرگشت جدا نخواهد (D.4 «Performance»: قطعی ۳۰۰ خط زیر ۲۰ دستور)
        var lines = _db.Set<ShipmentLine>().Where(l => l.TenantId == tenantId && l.ShipmentId == shipmentId);
        return await _db.Rugs
            .Include(r => r.WorkflowSteps)
            .Include(r => r.Sale)
            .AsSplitQuery()
            .Where(r => r.TenantId == tenantId && lines.Any(l => l.RugId == r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeclaredValueRate>> ListRatesTrackedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _db.Set<DeclaredValueRate>()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.KeyNormalized)
            .ToListAsync(cancellationToken);

    public async Task AddShipmentAsync(Shipment shipment, CancellationToken cancellationToken = default)
        => await _db.Set<Shipment>().AddAsync(shipment, cancellationToken);

    public async Task AddBaleAsync(Bale bale, CancellationToken cancellationToken = default)
        => await _db.Set<Bale>().AddAsync(bale, cancellationToken);

    public async Task AddLineAsync(ShipmentLine line, CancellationToken cancellationToken = default)
        => await _db.Set<ShipmentLine>().AddAsync(line, cancellationToken);

    public async Task AddRateAsync(DeclaredValueRate rate, CancellationToken cancellationToken = default)
        => await _db.Set<DeclaredValueRate>().AddAsync(rate, cancellationToken);

    public void RemoveShipment(Shipment shipment) => _db.Set<Shipment>().Remove(shipment);

    public void RemoveBale(Bale bale) => _db.Set<Bale>().Remove(bale);

    public void RemoveLine(ShipmentLine line) => _db.Set<ShipmentLine>().Remove(line);

    public void RemoveRate(DeclaredValueRate rate) => _db.Set<DeclaredValueRate>().Remove(rate);

    // ═════════════════════════════════════════════════════════════════════
    // خواندن
    // ═════════════════════════════════════════════════════════════════════

    public async Task<int> MaxBaleNumberAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => await _db.Set<Bale>()
            .Where(b => b.TenantId == tenantId && b.ShipmentId == shipmentId)
            .MaxAsync(b => (int?)b.Number, cancellationToken) ?? 0;

    public async Task<int> MaxSeqAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default)
        => await _db.Set<ShipmentLine>()
            .Where(l => l.TenantId == tenantId && l.BaleId == baleId)
            .MaxAsync(l => (int?)l.Seq, cancellationToken) ?? 0;

    public async Task<ShipmentHeaderData?> GetHeaderAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => await _db.Set<Shipment>()
            .AsNoTracking()
            .Where(s => s.Id == shipmentId && s.TenantId == tenantId)
            .Select(s => new ShipmentHeaderData(
                s.Id, s.Number, s.Title, s.DocumentDate, s.Destination, s.BuyerName, s.DeclarationNo, s.CustomsOffice,
                s.Status, s.FinalizeCount, s.FinalizedAt, s.SnapshotHash, s.TargetBaleAreaM2, s.UsdRate, s.Notes,
                EF.Property<uint>(s, "xmin"), s.CreatedAt, s.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<ShipmentListData>> ListAsync(
        Guid tenantId, ShipmentStatus? status, string keyField, CancellationToken cancellationToken = default)
        => ListCoreAsync(tenantId, status, keyField, byActivity: false, cancellationToken);

    public Task<IReadOnlyList<ShipmentListData>> ListOpenForHomeAsync(
        Guid tenantId, string keyField, CancellationToken cancellationToken = default)
        => ListCoreAsync(tenantId, ShipmentStatus.Open, keyField, byActivity: true, cancellationToken);

    /// <summary>
    /// یک SQL برای همهٔ فهرست: جمع‌ها، نگله‌های خالی، فرش‌های بی‌اندازهٔ نهایی و مقدار کلیدِ خط‌های «ندارد» (C.6 «Performance»).
    /// </summary>
    private async Task<IReadOnlyList<ShipmentListData>> ListCoreAsync(
        Guid tenantId, ShipmentStatus? status, string keyField, bool byActivity, CancellationToken cancellationToken)
    {
        var sql = $"""
            WITH s AS (
                SELECT * FROM "Shipments" WHERE "TenantId" = @tenant {(status is null ? "" : "AND \"Status\" = @status")}),
            lx AS (
                SELECT l."ShipmentId" AS shipment_id, l."BaleId" AS bale_id, r."Quantity" AS q,
                       COALESCE(v."LineAreaM2", r."AreaM2" * r."Quantity") AS area, v."LineUsd" AS usd,
                       (r."FinalLength" IS NULL OR r."FinalWidth" IS NULL) AS no_final,
                       (v."LineId" IS NULL OR v."RateSource" = {(int)DeclaredRateSource.Missing}) AS missing,
                       {ShipmentSql.KeyValue(keyField, "r")} AS key_value,
                       COALESCE(l."UpdatedAt", l."CreatedAt") AS act
                FROM "ShipmentLines" l
                JOIN "Rugs" r ON r."Id" = l."RugId" AND r."DeletedAt" IS NULL
                LEFT JOIN "ShipmentLineValues" v ON v."LineId" = l."Id"
                WHERE l."TenantId" = @tenant AND l."ShipmentId" IN (SELECT "Id" FROM s)),
            bx AS (
                SELECT b."ShipmentId" AS shipment_id,
                       NOT EXISTS (SELECT 1 FROM lx WHERE lx.bale_id = b."Id") AS empty,
                       COALESCE(b."UpdatedAt", b."CreatedAt") AS act
                FROM "Bales" b
                WHERE b."TenantId" = @tenant AND b."ShipmentId" IN (SELECT "Id" FROM s))
            SELECT s."Id", s."Number", s."Title", s."DocumentDate", s."Status",
                   (SELECT count(*) FROM bx WHERE bx.shipment_id = s."Id")::int,
                   (SELECT count(*) FROM lx WHERE lx.shipment_id = s."Id")::int,
                   (SELECT COALESCE(sum(q), 0) FROM lx WHERE lx.shipment_id = s."Id")::int,
                   (SELECT COALESCE(sum(area), 0) FROM lx WHERE lx.shipment_id = s."Id"),
                   (SELECT COALESCE(sum(usd), 0) FROM lx WHERE lx.shipment_id = s."Id"),
                   (SELECT count(*) FROM bx WHERE bx.shipment_id = s."Id" AND bx.empty)::int,
                   (SELECT count(*) FROM lx WHERE lx.shipment_id = s."Id" AND lx.no_final)::int,
                   (SELECT array_agg(key_value) FROM lx WHERE lx.shipment_id = s."Id" AND lx.missing),
                   GREATEST(s."CreatedAt", s."UpdatedAt",
                            (SELECT max(act) FROM lx WHERE lx.shipment_id = s."Id"),
                            (SELECT max(act) FROM bx WHERE bx.shipment_id = s."Id")) AS last_activity
            FROM s
            ORDER BY {(byActivity ? "last_activity DESC, s.\"Number\" DESC" : "s.\"Number\" DESC")}
            """;

        var parameters = new List<NpgsqlParameter> { ShipmentSql.Uuid("tenant", tenantId) };
        if (status is { } st) parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Integer) { Value = (int)st });

        return await ShipmentSql.QueryAsync(_db, sql,
            r => new ShipmentListData(
                r.GetGuid(0), r.GetInt32(1), r.GetString(2), r.GetFieldValue<DateOnly>(3), (ShipmentStatus)r.GetInt32(4),
                r.GetInt32(5), r.GetInt32(6), r.GetInt32(7), r.GetDecimal(8), r.GetDecimal(9),
                r.GetInt32(10), r.GetInt32(11),
                r.IsDBNull(12) ? [] : r.GetFieldValue<string?[]>(12),
                r.IsDBNull(13) ? null : r.GetFieldValue<DateTimeOffset>(13)),
            cancellationToken, ShipmentSql.WithKey(keyField, [.. parameters]));
    }

    public async Task<IReadOnlyList<BaleData>> ListBalesAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT b."Id", b."Number", b.xmin, b."WeightKg", b."Notes",
                   count(x.line_id)::int,
                   COALESCE(sum(x.q), 0)::int,
                   COALESCE(sum(x.area), 0),
                   COALESCE(sum(x.usd), 0)
            FROM "Bales" b
            LEFT JOIN (
                SELECT l."Id" AS line_id, l."BaleId" AS bale_id, r."Quantity" AS q,
                       COALESCE(v."LineAreaM2", r."AreaM2" * r."Quantity") AS area, v."LineUsd" AS usd
                FROM "ShipmentLines" l
                JOIN "Rugs" r ON r."Id" = l."RugId" AND r."DeletedAt" IS NULL
                LEFT JOIN "ShipmentLineValues" v ON v."LineId" = l."Id"
                WHERE l."TenantId" = @tenant AND l."ShipmentId" = @shipment) x ON x.bale_id = b."Id"
            WHERE b."TenantId" = @tenant AND b."ShipmentId" = @shipment
            GROUP BY b."Id", b."Number", b.xmin, b."WeightKg", b."Notes"
            ORDER BY b."Number", b."Id"
            """;

        return await ShipmentSql.QueryAsync(_db, sql,
            r => new BaleData(r.GetGuid(0), r.GetInt32(1), r.GetFieldValue<uint>(2), ShipmentSql.NullableDecimal(r, 3),
                ShipmentSql.NullableString(r, 4), r.GetInt32(5), r.GetInt32(6), r.GetDecimal(7), r.GetDecimal(8)),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuid("shipment", shipmentId));
    }

    public async Task<IReadOnlyList<ShipmentRowData>> ListRowsAsync(
        Guid tenantId, Guid shipmentId, string keyField, Guid? baleId = null, IReadOnlyCollection<Guid>? lineIds = null,
        CancellationToken cancellationToken = default)
    {
        if (lineIds is { Count: 0 }) return [];

        var sql = $"""
            SELECT l."Id", l.xmin, l."Seq", l."UsdPerM2Override",
                   b."Id", b."Number",
                   r."Id", r.xmin, r."Sku", r."SerialNo", r."Quantity",
                   r."Title", r."Origin", r."Pattern", r."Material", r."KnotDensity", r."MetadataJson"::text,
                   r."IntakeLength", r."IntakeWidth", r."FinalLength", r."FinalWidth", r."AreaM2",
                   v."LineId" IS NOT NULL, v."UsdPerM2", v."RateSource", v."PieceUsd", v."LineAreaM2", v."LineUsd",
                   {ShipmentSql.KeyValue(keyField, "r")}
            FROM "ShipmentLines" l
            JOIN "Bales" b ON b."Id" = l."BaleId"
            JOIN "Rugs" r ON r."Id" = l."RugId" AND r."DeletedAt" IS NULL
            LEFT JOIN "ShipmentLineValues" v ON v."LineId" = l."Id"
            WHERE l."TenantId" = @tenant AND l."ShipmentId" = @shipment
                {(baleId is null ? "" : "AND l.\"BaleId\" = @bale")}
                {(lineIds is null ? "" : "AND l.\"Id\" = ANY(@lines)")}
            ORDER BY b."Number", l."Seq", l."Id"
            """;

        var parameters = new List<NpgsqlParameter> { ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuid("shipment", shipmentId) };
        if (baleId is { } bale) parameters.Add(ShipmentSql.Uuid("bale", bale));
        if (lineIds is not null) parameters.Add(ShipmentSql.Uuids("lines", lineIds));

        return await ShipmentSql.QueryAsync(_db, sql, ReadRow, cancellationToken, ShipmentSql.WithKey(keyField, [.. parameters]));
    }

    private static ShipmentRowData ReadRow(DbDataReader r) => new(
        r.GetGuid(0), r.GetFieldValue<uint>(1), r.GetInt32(2), ShipmentSql.NullableDecimal(r, 3),
        r.GetGuid(4), r.GetInt32(5),
        r.GetGuid(6), r.GetFieldValue<uint>(7), r.GetString(8), ShipmentSql.NullableString(r, 9), r.GetInt32(10),
        ShipmentSql.NullableString(r, 11), ShipmentSql.NullableString(r, 12), ShipmentSql.NullableString(r, 13),
        ShipmentSql.NullableString(r, 14), r.IsDBNull(15) ? null : r.GetInt32(15), ShipmentSql.NullableString(r, 16),
        r.GetDecimal(17), r.GetDecimal(18), ShipmentSql.NullableDecimal(r, 19), ShipmentSql.NullableDecimal(r, 20), r.GetDecimal(21),
        r.GetBoolean(22), ShipmentSql.NullableDecimal(r, 23),
        r.IsDBNull(24) ? DeclaredRateSource.Missing : (DeclaredRateSource)r.GetInt32(24),
        ShipmentSql.NullableDecimal(r, 25), r.IsDBNull(26) ? 0m : r.GetDecimal(26), ShipmentSql.NullableDecimal(r, 27),
        ShipmentSql.NullableString(r, 28));

    public async Task<RugLineState?> RugStateAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
        => await _db.Rugs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.Id == rugId && r.TenantId == tenantId)
            .Select(r => new RugLineState(r.Id, r.Sku, r.SerialNo, r.SerialFrom, r.SerialTo, r.Quantity,
                r.DeletedAt != null, r.Sale != null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RugPlacementData>> PlacementsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default)
    {
        if (rugIds.Count == 0) return [];
        const string sql = """
            SELECT l."RugId", l."Id", s."Id", s."Number", s."Status", b."Id", b."Number", v."UsdPerM2", v."LineUsd"
            FROM "ShipmentLines" l
            JOIN "Shipments" s ON s."Id" = l."ShipmentId"
            JOIN "Bales" b ON b."Id" = l."BaleId"
            LEFT JOIN "ShipmentLineValues" v ON v."LineId" = l."Id"
            WHERE l."TenantId" = @tenant AND l."RugId" = ANY(@rugs)
            """;

        return await ShipmentSql.QueryAsync(_db, sql,
            r => new RugPlacementData(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetInt32(3), (ShipmentStatus)r.GetInt32(4),
                r.GetGuid(5), r.GetInt32(6), ShipmentSql.NullableDecimal(r, 7), ShipmentSql.NullableDecimal(r, 8)),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuids("rugs", rugIds));
    }

    public async Task<IReadOnlyList<Guid>> TenantsOfRugsAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default)
    {
        if (rugIds.Count == 0) return [];
        var ids = rugIds.ToList();
        return await _db.Rugs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .Select(r => r.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeclaredValueRate>> ListRatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _db.Set<DeclaredValueRate>()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.KeyValue).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string?>> OpenLineKeyValuesAsync(Guid tenantId, string keyField, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {ShipmentSql.KeyValue(keyField, "r")}
            FROM "ShipmentLines" l
            JOIN "Shipments" s ON s."Id" = l."ShipmentId" AND s."Status" = {(int)ShipmentStatus.Open}
            JOIN "Rugs" r ON r."Id" = l."RugId" AND r."DeletedAt" IS NULL
            WHERE l."TenantId" = @tenant AND l."UsdPerM2Override" IS NULL
            """;

        return await ShipmentSql.QueryAsync(_db, sql, r => r.IsDBNull(0) ? null : r.GetString(0),
            cancellationToken, ShipmentSql.WithKey(keyField, ShipmentSql.Uuid("tenant", tenantId)));
    }

    public async Task<IReadOnlyList<CustomFieldInfo>> CustomFieldsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _db.CustomFieldDefinitions
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.IsActive && f.Scope == CustomFieldScope.Rug)
            .OrderBy(f => f.Key)
            .Select(f => new CustomFieldInfo(f.Key, f.Label, f.Exportable))
            .ToListAsync(cancellationToken);

    public async Task<(int Pieces, decimal AreaM2)> StockAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // همان قاعدهٔ «انبار» فاز سکو (وضعیت فروخته یا صادرشده بیرون است، حتی دادهٔ قدیمی‌ای که ردیف فروش ندارد) به‌اضافهٔ
        // جای C.10: فرشِ دارای فروش یا داخل هر نگله‌ای در انبار نیست
        var sql = $"""
            SELECT COALESCE(sum(r."Quantity"), 0)::int, COALESCE(sum(r."TotalAreaM2"), 0)
            FROM "Rugs" r
            WHERE r."TenantId" = @tenant AND r."DeletedAt" IS NULL
                AND r."Status" NOT IN ({(int)RugStatus.Sold}, {(int)RugStatus.Exported})
                AND NOT EXISTS (SELECT 1 FROM "RugSales" x WHERE x."RugId" = r."Id")
                AND NOT EXISTS (SELECT 1 FROM "ShipmentLines" l WHERE l."RugId" = r."Id")
            """;
        var rows = await ShipmentSql.QueryAsync(_db, sql, r => (r.GetInt32(0), r.GetDecimal(1)),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId));
        return rows.Count > 0 ? rows[0] : (0, 0m);
    }
}

/// <summary>
/// «آیا این کارگاه مبلغی ذخیره کرده؟» برای جدول‌های فاز محموله (D.5 #22): درآمد ثبت‌شدهٔ یک محموله. <c>PreferencesLockGuard</c>
/// و صفحهٔ ترجیحات همهٔ پرسش‌های ثبت‌شده را می‌پرسند.
/// </summary>
public sealed class ShipmentStoredMoneyProbe(AppDbContext db) : IStoredMoneyProbe
{
    public Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken)
        => db.Set<Shipment>().AsNoTracking().AnyAsync(s => s.TenantId == tenantId && s.RevenueAmount != null, cancellationToken);
}
