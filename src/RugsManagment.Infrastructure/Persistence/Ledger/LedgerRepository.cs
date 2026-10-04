using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// SQL پاس سازگاری (پیاده‌سازی <see cref="ILedgerRepository"/>): همه مجموعه‌ای، روی همان اتصال و تراکنش DbContext، با
/// فرمول‌های <see cref="CostSql"/>. این کلاس (با بقیهٔ <c>Persistence/Ledger</c>) در فهرست سفید آزمون معماری «هیچ نوشتنی از
/// پاس رد نمی‌شود» است؛ همهٔ نوشتن‌هایش در ستون‌ها و جدول‌های مشتق‌شده است و هرگز به <c>Rugs</c> (و xmin آن) نمی‌رسد.
/// هر نوشتن «diff-write» است: فقط ردیفی که مقدارش واقعاً عوض می‌شود نوشته می‌شود.
/// </summary>
public sealed class LedgerRepository(AppDbContext db) : ILedgerRepository
{
    // ── تنظیم‌ها ─────────────────────────────────────────────────────────

    public async Task<LedgerTenantSettings> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT t."Currency", p."MoneyScale", p."SerialStart"
            FROM "Tenants" t LEFT JOIN "TenantPreferences" p ON p."TenantId" = t."Id"
            WHERE t."Id" = @tenant
            """;

        var rows = await QueryAsync(sql, r => new LedgerTenantSettings(
                r.IsDBNull(1) ? 0 : r.GetInt16(1),
                (CurrencyUnit)r.GetInt32(0),
                r.IsDBNull(2) ? null : r.GetInt32(2)),
            cancellationToken, Uuid("tenant", tenantId));

        // کارگاهی که در همین ذخیره ساخته می‌شود هنوز ردیفی ندارد: پیش‌فرض‌ها
        return rows.Count > 0 ? rows[0] : new LedgerTenantSettings(0, CurrencyUnit.Toman, null);
    }

    public async Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken cancellationToken)
        => await QueryAsync("""SELECT "Id" FROM "Tenants" ORDER BY "CreatedAt", "Id" """, r => r.GetGuid(0), cancellationToken);

    // ── شماره‌ها ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SerialConflict>> FindSerialOverlapsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return [];

        // عددی با عددی: دو بازه اشتراک دارند؛ متنی با متنی: همان متن. ایندکس جزئی (TenantId, SerialFrom, SerialTo) کار را
        // ارزان نگه می‌دارد.
        const string sql = """
            SELECT a."Id", a."SerialNo", b."Id", b."SerialNo"
            FROM "Rugs" a
            JOIN "Rugs" b ON b."TenantId" = a."TenantId" AND b."Id" <> a."Id"
                AND b."DeletedAt" IS NULL AND b."SerialNo" IS NOT NULL
                AND (
                    (a."SerialFrom" IS NOT NULL AND b."SerialFrom" IS NOT NULL
                        AND b."SerialFrom" <= a."SerialTo" AND a."SerialFrom" <= b."SerialTo")
                    OR (a."SerialFrom" IS NULL AND b."SerialFrom" IS NULL AND b."SerialNo" = a."SerialNo"))
            WHERE a."TenantId" = @tenant AND a."Id" = ANY(@rugs)
                AND a."DeletedAt" IS NULL AND a."SerialNo" IS NOT NULL
            ORDER BY a."SerialSort" NULLS LAST, a."Id", b."SerialSort" NULLS LAST, b."Id"
            LIMIT 20
            """;

        return await QueryAsync(sql,
            r => new SerialConflict(r.GetGuid(0), r.GetString(1), r.GetGuid(2), r.GetString(3)),
            cancellationToken, Uuid("tenant", tenantId), Uuids("rugs", rugIds));
    }

    public async Task<int?> MaxSerialAsync(
        Guid tenantId, IReadOnlyCollection<Guid> excludingRugIds, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MAX(COALESCE("SerialTo", "SerialFrom"))
            FROM "Rugs"
            WHERE "TenantId" = @tenant AND "DeletedAt" IS NULL AND "SerialFrom" IS NOT NULL
                AND NOT ("Id" = ANY(@excluded))
            """;

        var rows = await QueryAsync(sql, r => r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
            cancellationToken, Uuid("tenant", tenantId), Uuids("excluded", excludingRugIds));
        return rows.Count > 0 ? rows[0] : null;
    }

    // ── سطرهای خرید و جمع ────────────────────────────────────────────────

    public async Task<int> RecomputePurchaseLinesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, int moneyScale, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return 0;

        var sql = "WITH " + CostSql.ExpectedLineAmountsCtes("@tenant", "@rugs", "@scale") + """

            UPDATE "RugPurchaseLines" l SET "Amount" = e.amount
            FROM pl_expected e
            WHERE l."Id" = e."Id" AND l."Amount" IS DISTINCT FROM e.amount
            """;

        return await ExecuteAsync(sql, cancellationToken,
            Uuid("tenant", tenantId), Uuids("rugs", rugIds), Int("scale", moneyScale));
    }

    public async Task<IReadOnlyList<Guid>> RollupPurchaseCostAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources,
        DateTimeOffset now, long ledgerVersion, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return [];

        // ردیفی که نیست با بقیهٔ ستون‌های صفر ساخته می‌شود؛ مرحلهٔ ۱۰۰۰ در همین پاس کاملش می‌کند
        var sql = $"""
            WITH src AS (SELECT * FROM unnest(@src_ids, @src_pa, @src_ea, @src_rev) AS s("RugId", pa, ea, rev)),
            calc AS (
                SELECT r."Id" AS rug_id, r."TenantId" AS tenant_id,
                       {CostSql.PurchaseCost(CostSql.PurchaseLinesOfRug("r.\"Id\""), "src.pa")} AS purchase
                FROM "Rugs" r LEFT JOIN src ON src."RugId" = r."Id"
                WHERE r."TenantId" = @tenant AND r."Id" = ANY(@rugs)),
            changed AS (
                SELECT c.* FROM calc c LEFT JOIN "RugCostTotals" t ON t."RugId" = c.rug_id
                WHERE t."RugId" IS NULL OR t."PurchaseCost" IS DISTINCT FROM c.purchase),
            up AS (
                INSERT INTO "RugCostTotals" ("RugId", "TenantId", "PurchaseCost", "StepCost", "ExpenseCost", "Total",
                                             "CostPerM2", "CostPerPiece", "ExportRevenueShare", "ComputedAt", "LedgerVersion")
                SELECT rug_id, tenant_id, purchase, 0, 0, 0, NULL, 0, NULL, @now, @version FROM changed
                ON CONFLICT ("RugId") DO UPDATE SET "PurchaseCost" = EXCLUDED."PurchaseCost"
                RETURNING "RugId")
            SELECT "RugId" FROM up
            """;

        var parameters = new List<NpgsqlParameter>
        {
            Uuid("tenant", tenantId), Uuids("rugs", rugIds),
            new("now", NpgsqlDbType.TimestampTz) { Value = now.ToUniversalTime() },
            new("version", NpgsqlDbType.Bigint) { Value = ledgerVersion }
        };
        parameters.AddRange(SourceParameters(rugIds, sources));

        return await QueryAsync(sql, r => r.GetGuid(0), cancellationToken, [.. parameters]);
    }

    public async Task<int> UpsertCostTotalsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources,
        int moneyScale, DateTimeOffset now, long ledgerVersion, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return 0;

        var sql = $"""
            WITH src AS (SELECT * FROM unnest(@src_ids, @src_pa, @src_ea, @src_rev) AS s("RugId", pa, ea, rev)),
            calc AS (
                SELECT r."Id" AS rug_id, r."TenantId" AS tenant_id,
                       {CostSql.PurchaseCost(CostSql.PurchaseLinesOfRug("r.\"Id\""), "src.pa")} AS purchase,
                       {CostSql.StepCostOfRug("r.\"Id\"")} AS step,
                       {CostSql.ExpenseCost("src.ea")} AS expense,
                       src.rev AS rev, r."TotalAreaM2" AS area, r."Quantity" AS qty
                FROM "Rugs" r LEFT JOIN src ON src."RugId" = r."Id"
                WHERE r."TenantId" = @tenant AND r."Id" = ANY(@rugs)),
            fin AS (SELECT c.*, {CostSql.Total("c.purchase", "c.step", "c.expense")} AS total FROM calc c)
            INSERT INTO "RugCostTotals" ("RugId", "TenantId", "PurchaseCost", "StepCost", "ExpenseCost", "Total",
                                         "CostPerM2", "CostPerPiece", "ExportRevenueShare", "ComputedAt", "LedgerVersion")
            SELECT f.rug_id, f.tenant_id, f.purchase, f.step, f.expense, f.total,
                   {CostSql.CostPerM2("f.total", "f.area", "@scale")},
                   {CostSql.CostPerPiece("f.total", "f.qty", "@scale")},
                   f.rev, @now, @version
            FROM fin f
            ON CONFLICT ("RugId") DO UPDATE SET
                "TenantId" = EXCLUDED."TenantId",
                "PurchaseCost" = EXCLUDED."PurchaseCost",
                "StepCost" = EXCLUDED."StepCost",
                "ExpenseCost" = EXCLUDED."ExpenseCost",
                "Total" = EXCLUDED."Total",
                "CostPerM2" = EXCLUDED."CostPerM2",
                "CostPerPiece" = EXCLUDED."CostPerPiece",
                "ExportRevenueShare" = EXCLUDED."ExportRevenueShare",
                "ComputedAt" = EXCLUDED."ComputedAt",
                "LedgerVersion" = EXCLUDED."LedgerVersion"
            WHERE ("RugCostTotals"."TenantId", "RugCostTotals"."PurchaseCost", "RugCostTotals"."StepCost",
                   "RugCostTotals"."ExpenseCost", "RugCostTotals"."Total", "RugCostTotals"."CostPerM2",
                   "RugCostTotals"."CostPerPiece", "RugCostTotals"."ExportRevenueShare", "RugCostTotals"."LedgerVersion")
                IS DISTINCT FROM
                  (EXCLUDED."TenantId", EXCLUDED."PurchaseCost", EXCLUDED."StepCost", EXCLUDED."ExpenseCost",
                   EXCLUDED."Total", EXCLUDED."CostPerM2", EXCLUDED."CostPerPiece", EXCLUDED."ExportRevenueShare",
                   EXCLUDED."LedgerVersion")
            """;

        var parameters = new List<NpgsqlParameter>
        {
            Uuid("tenant", tenantId), Uuids("rugs", rugIds), Int("scale", moneyScale),
            new("now", NpgsqlDbType.TimestampTz) { Value = now.ToUniversalTime() },
            new("version", NpgsqlDbType.Bigint) { Value = ledgerVersion }
        };
        parameters.AddRange(SourceParameters(rugIds, sources));

        return await ExecuteAsync(sql, cancellationToken, [.. parameters]);
    }

    public async Task<int> SnapshotCostAtSaleAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return 0;

        const string sql = """
            UPDATE "RugSales" s SET "CostAtSale" = t."Total", "CostAtSaleAt" = @now
            FROM "RugCostTotals" t
            WHERE t."RugId" = s."RugId" AND s."TenantId" = @tenant AND s."RugId" = ANY(@rugs)
            """;

        return await ExecuteAsync(sql, cancellationToken,
            Uuid("tenant", tenantId), Uuids("rugs", rugIds),
            new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now.ToUniversalTime() });
    }

    // ── بازسازی و سنجش ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<Guid>> ListRugIdsAsync(Guid tenantId, int? sample, CancellationToken cancellationToken)
    {
        if (sample is int n)
        {
            return await QueryAsync("""SELECT "Id" FROM "Rugs" WHERE "TenantId" = @tenant ORDER BY random() LIMIT @n""",
                r => r.GetGuid(0), cancellationToken, Uuid("tenant", tenantId), Int("n", Math.Max(0, n)));
        }

        return await QueryAsync("""SELECT "Id" FROM "Rugs" WHERE "TenantId" = @tenant ORDER BY "CreatedAt", "Id" """,
            r => r.GetGuid(0), cancellationToken, Uuid("tenant", tenantId));
    }

    public async Task<LedgerHealthCounts> GetHealthCountsAsync(
        Guid tenantId, long ledgerVersion, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT (SELECT count(*) FROM "Rugs" WHERE "TenantId" = @tenant)::int,
                   (SELECT count(*) FROM "RugCostTotals" WHERE "TenantId" = @tenant)::int,
                   (SELECT count(*) FROM "RugCostTotals" WHERE "TenantId" = @tenant AND "LedgerVersion" <> @version)::int
            """;

        var rows = await QueryAsync(sql, r => new LedgerHealthCounts(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)),
            cancellationToken, Uuid("tenant", tenantId), new NpgsqlParameter("version", NpgsqlDbType.Bigint) { Value = ledgerVersion });
        return rows[0];
    }

    public async Task<IReadOnlyList<LedgerMismatchRow>> VerifyAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources,
        int moneyScale, long ledgerVersion, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return [];
        var mismatches = new List<LedgerMismatchRow>();

        // ۱) مبلغ هر سطر خرید با قاعدهٔ دسته
        var linesSql = "WITH " + CostSql.ExpectedLineAmountsCtes("@tenant", "@rugs", "@scale") + """

            SELECT r."Id", r."Sku", r."SerialNo", l."Label", l."Amount", e.amount
            FROM pl_expected e
            JOIN "RugPurchaseLines" l ON l."Id" = e."Id"
            JOIN "Rugs" r ON r."Id" = l."RugId"
            WHERE l."Amount" IS DISTINCT FROM e.amount
            ORDER BY r."CreatedAt", r."Id", l."SortOrder"
            """;
        mismatches.AddRange(await QueryAsync(linesSql,
            r => new LedgerMismatchRow(r.GetGuid(0), r.GetString(1), NullableString(r, 2), CostMismatchCheck.PurchaseLine,
                r.GetDecimal(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetString(3)),
            cancellationToken, Uuid("tenant", tenantId), Uuids("rugs", rugIds), Int("scale", moneyScale)));

        // ۲) جمع: از مبلغ «درست» سطرها (نه مبلغ ذخیره‌شده)، با همان فرمول مرحلهٔ ۱۰۰۰
        var totalsSql = "WITH " + CostSql.ExpectedLineAmountsCtes("@tenant", "@rugs", "@scale") + $"""
            ,
            src AS (SELECT * FROM unnest(@src_ids, @src_pa, @src_ea, @src_rev) AS s("RugId", pa, ea, rev)),
            calc AS (
                SELECT r."Id" AS rug_id, r."Sku" AS sku, r."SerialNo" AS serial, r."CreatedAt" AS created,
                       {CostSql.PurchaseCost(CostSql.ExpectedPurchaseLinesOfRug("r.\"Id\""), "src.pa")} AS purchase,
                       {CostSql.StepCostOfRug("r.\"Id\"")} AS step,
                       {CostSql.ExpenseCost("src.ea")} AS expense,
                       src.rev AS rev, r."TotalAreaM2" AS area, r."Quantity" AS qty
                FROM "Rugs" r LEFT JOIN src ON src."RugId" = r."Id"
                WHERE r."TenantId" = @tenant AND r."Id" = ANY(@rugs)),
            fin AS (SELECT c.*, {CostSql.Total("c.purchase", "c.step", "c.expense")} AS total FROM calc c)
            SELECT f.rug_id, f.sku, f.serial,
                   f.purchase, f.step, f.expense, f.total,
                   {CostSql.CostPerM2("f.total", "f.area", "@scale")} AS per_m2,
                   {CostSql.CostPerPiece("f.total", "f.qty", "@scale")} AS per_piece,
                   f.rev,
                   t."RugId" IS NOT NULL, t."PurchaseCost", t."StepCost", t."ExpenseCost", t."Total",
                   t."CostPerM2", t."CostPerPiece", t."ExportRevenueShare", t."LedgerVersion"
            FROM fin f LEFT JOIN "RugCostTotals" t ON t."RugId" = f.rug_id
            ORDER BY f.created, f.rug_id
            """;

        var totalParameters = new List<NpgsqlParameter>
        {
            Uuid("tenant", tenantId), Uuids("rugs", rugIds), Int("scale", moneyScale)
        };
        totalParameters.AddRange(SourceParameters(rugIds, sources));

        var totals = await QueryAsync(totalsSql, ReadExpectedTotal, cancellationToken, [.. totalParameters]);
        foreach (var row in totals)
            mismatches.AddRange(CompareTotals(row, ledgerVersion));

        // ۳) جفت مؤثر = COALESCE(نهایی، اول) (B.4)
        const string sizeSql = """
            SELECT r."Id", r."Sku", r."SerialNo", r."LengthMeters", r."WidthMeters",
                   COALESCE(r."FinalLength", r."IntakeLength"), COALESCE(r."FinalWidth", r."IntakeWidth")
            FROM "Rugs" r
            WHERE r."TenantId" = @tenant AND r."Id" = ANY(@rugs)
                AND (r."LengthMeters" <> COALESCE(r."FinalLength", r."IntakeLength")
                     OR r."WidthMeters" <> COALESCE(r."FinalWidth", r."IntakeWidth"))
            ORDER BY r."CreatedAt", r."Id"
            """;
        var sizes = await QueryAsync(sizeSql,
            r => (Id: r.GetGuid(0), Sku: r.GetString(1), Serial: NullableString(r, 2),
                Length: r.GetDecimal(3), Width: r.GetDecimal(4), ExpectedLength: r.GetDecimal(5), ExpectedWidth: r.GetDecimal(6)),
            cancellationToken, Uuid("tenant", tenantId), Uuids("rugs", rugIds));
        foreach (var s in sizes)
        {
            if (s.Length != s.ExpectedLength)
                mismatches.Add(new LedgerMismatchRow(s.Id, s.Sku, s.Serial, CostMismatchCheck.EffectiveSize, s.Length, s.ExpectedLength, "طول"));
            if (s.Width != s.ExpectedWidth)
                mismatches.Add(new LedgerMismatchRow(s.Id, s.Sku, s.Serial, CostMismatchCheck.EffectiveSize, s.Width, s.ExpectedWidth, "عرض"));
        }

        return mismatches;
    }

    // ═════════════════════════════════════════════════════════════════════
    // جزئیات
    // ═════════════════════════════════════════════════════════════════════

    private sealed record ExpectedTotal(
        Guid RugId, string Sku, string? Serial,
        decimal Purchase, decimal Step, decimal Expense, decimal Total, decimal? PerM2, decimal PerPiece, decimal? Revenue,
        bool HasRow, decimal? StoredPurchase, decimal? StoredStep, decimal? StoredExpense, decimal? StoredTotal,
        decimal? StoredPerM2, decimal? StoredPerPiece, decimal? StoredRevenue, long? StoredVersion);

    private static ExpectedTotal ReadExpectedTotal(DbDataReader r) => new(
        r.GetGuid(0), r.GetString(1), NullableString(r, 2),
        r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), NullableDecimal(r, 7), r.GetDecimal(8), NullableDecimal(r, 9),
        r.GetBoolean(10), NullableDecimal(r, 11), NullableDecimal(r, 12), NullableDecimal(r, 13), NullableDecimal(r, 14),
        NullableDecimal(r, 15), NullableDecimal(r, 16), NullableDecimal(r, 17), r.IsDBNull(18) ? null : r.GetInt64(18));

    private static IEnumerable<LedgerMismatchRow> CompareTotals(ExpectedTotal row, long ledgerVersion)
    {
        if (!row.HasRow)
        {
            yield return new LedgerMismatchRow(row.RugId, row.Sku, row.Serial, CostMismatchCheck.MissingTotal, null, row.Total, null);
            yield break;
        }

        if (row.StoredVersion != ledgerVersion)
        {
            yield return new LedgerMismatchRow(row.RugId, row.Sku, row.Serial, CostMismatchCheck.StaleVersion,
                row.StoredVersion, ledgerVersion, null);
        }

        // نام فارسی ستون در «توضیح»؛ صفحهٔ سلامت حساب‌ها همین را نشان می‌دهد
        foreach (var (label, stored, expected) in new (string, decimal?, decimal?)[]
                 {
                     ("خرید", row.StoredPurchase, row.Purchase),
                     ("مراحل", row.StoredStep, row.Step),
                     ("هزینه‌ها", row.StoredExpense, row.Expense),
                     ("بهای تمام‌شده", row.StoredTotal, row.Total),
                     ("هر متر مربع", row.StoredPerM2, row.PerM2),
                     ("هر تخته", row.StoredPerPiece, row.PerPiece),
                     ("سهم درآمد صادرات", row.StoredRevenue, row.Revenue)
                 })
        {
            if (stored != expected)
                yield return new LedgerMismatchRow(row.RugId, row.Sku, row.Serial, CostMismatchCheck.Total, stored, expected, label);
        }
    }

    /// <summary>سهم‌های ثبت هزینه به‌صورت چهار آرایهٔ هم‌طول برای <c>unnest</c> (فقط فرش‌هایی که سهم دارند).</summary>
    private static IEnumerable<NpgsqlParameter> SourceParameters(
        IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources)
    {
        var withSources = rugIds.Where(sources.ContainsKey).ToList();
        yield return new NpgsqlParameter("src_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = withSources.ToArray() };
        yield return new NpgsqlParameter("src_pa", NpgsqlDbType.Array | NpgsqlDbType.Numeric)
            { Value = withSources.Select(id => sources[id].PurchaseAllocations).ToArray() };
        yield return new NpgsqlParameter("src_ea", NpgsqlDbType.Array | NpgsqlDbType.Numeric)
            { Value = withSources.Select(id => sources[id].ExpenseAllocations).ToArray() };
        yield return new NpgsqlParameter("src_rev", NpgsqlDbType.Array | NpgsqlDbType.Numeric)
            { Value = withSources.Select(id => sources[id].ExportRevenueShare).ToArray() };
    }

    private static NpgsqlParameter Uuid(string name, Guid value) => new(name, NpgsqlDbType.Uuid) { Value = value };

    private static NpgsqlParameter Uuids(string name, IEnumerable<Guid> values)
        => new(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = values.ToArray() };

    private static NpgsqlParameter Int(string name, int value) => new(name, NpgsqlDbType.Integer) { Value = value };

    private static string? NullableString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static decimal? NullableDecimal(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    /// <summary>دستور روی همان اتصال و تراکنش DbContext (EF اتصال را با شمارش باز و بسته می‌کند).</summary>
    private async Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = CreateCommand(sql, parameters);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<List<T>> QueryAsync<T>(
        string sql, Func<DbDataReader, T> map, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = CreateCommand(sql, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
                rows.Add(map(reader));
            return rows;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private NpgsqlCommand CreateCommand(string sql, NpgsqlParameter[] parameters)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
        command.Parameters.AddRange(parameters);
        return command;
    }
}
