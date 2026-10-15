using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL پاس سازگاری برای محموله (پیاده‌سازی <see cref="IShipmentLedgerRepository"/>). نامش با «LedgerRepository» تمام می‌شود
/// چون در فهرست سفید «نوشتن خام» آزمون معماری است (D.4 «Bypass rule»)؛ تنها نوشتنش <see cref="WriteValuesAsync"/> در جدول
/// مشتق‌شدهٔ <c>ShipmentLineValues</c> است و هرگز به <c>Rugs</c>، <c>ShipmentLines</c> یا xmin آن‌ها نمی‌رسد. همهٔ دستورها روی
/// همان اتصال و تراکنش DbContext‌اند و کارگاه را پارامتر می‌گیرند.
/// </summary>
public sealed class ShipmentLedgerRepository(AppDbContext db) : IShipmentLedgerRepository
{
    public async Task<IReadOnlyList<LinePlacement>> LinesAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? rugIds, IReadOnlyCollection<Guid>? baleIds, IReadOnlyCollection<Guid>? shipmentIds,
        CancellationToken cancellationToken)
    {
        if ((rugIds?.Count ?? 0) + (baleIds?.Count ?? 0) + (shipmentIds?.Count ?? 0) == 0)
            return [];

        const string sql = """
            SELECT l."Id", l."RugId", s."Id", s."Number", s."Status", b."Id", b."Number", l."UsdPerM2Override" IS NOT NULL
            FROM "ShipmentLines" l
            JOIN "Shipments" s ON s."Id" = l."ShipmentId"
            JOIN "Bales" b ON b."Id" = l."BaleId"
            WHERE l."TenantId" = @tenant
                AND (l."RugId" = ANY(@rugs) OR l."BaleId" = ANY(@bales) OR l."ShipmentId" = ANY(@shipments))
            ORDER BY l."Id"
            """;

        return await ShipmentSql.QueryAsync(db, sql,
            r => new LinePlacement(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetInt32(3), (ShipmentStatus)r.GetInt32(4),
                r.GetGuid(5), r.GetInt32(6), r.GetBoolean(7)),
            cancellationToken,
            ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuids("rugs", rugIds ?? []),
            ShipmentSql.Uuids("bales", baleIds ?? []), ShipmentSql.Uuids("shipments", shipmentIds ?? []));
    }

    public async Task<IReadOnlyList<OpenLineKey>> OpenLinesWithoutOverrideAsync(
        Guid tenantId, string keyField, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT l."Id", l."ShipmentId", l."RugId", {ShipmentSql.KeyValue(keyField, "r")}
            FROM "ShipmentLines" l
            JOIN "Shipments" s ON s."Id" = l."ShipmentId" AND s."Status" = {(int)ShipmentStatus.Open}
            JOIN "Rugs" r ON r."Id" = l."RugId"
            WHERE l."TenantId" = @tenant AND l."UsdPerM2Override" IS NULL
            """;

        return await ShipmentSql.QueryAsync(db, sql,
            r => new OpenLineKey(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.IsDBNull(3) ? null : r.GetString(3)),
            cancellationToken, ShipmentSql.WithKey(keyField, ShipmentSql.Uuid("tenant", tenantId)));
    }

    public async Task<IReadOnlyList<ShipmentState>> ShipmentStatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken)
    {
        if (shipmentIds.Count == 0) return [];
        const string sql = """
            SELECT "Id", "Number", "Status" FROM "Shipments" WHERE "TenantId" = @tenant AND "Id" = ANY(@ids)
            """;
        return await ShipmentSql.QueryAsync(db, sql,
            r => new ShipmentState(r.GetGuid(0), r.GetInt32(1), (ShipmentStatus)r.GetInt32(2)),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuids("ids", shipmentIds));
    }

    public async Task<IReadOnlyList<BaleState>> BaleStatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> baleIds, CancellationToken cancellationToken)
    {
        if (baleIds.Count == 0) return [];
        const string sql = """
            SELECT "Id", "ShipmentId", "Number" FROM "Bales" WHERE "TenantId" = @tenant AND "Id" = ANY(@ids)
            """;
        return await ShipmentSql.QueryAsync(db, sql,
            r => new BaleState(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2)),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuids("ids", baleIds));
    }

    public async Task<IReadOnlyList<RugLockedValues>> RugValuesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
    {
        if (rugIds.Count == 0) return [];
        const string sql = """
            SELECT r."Id", EXISTS (SELECT 1 FROM "RugSales" x WHERE x."RugId" = r."Id"),
                   r."IntakeLength", r."IntakeWidth", r."FinalLength", r."FinalWidth", r."LengthMeters", r."WidthMeters",
                   r."Quantity", r."SerialNo", r."SerialFrom", r."SerialTo", r."SerialSort",
                   r."Title", r."Origin", r."Pattern", r."Material", r."KnotDensity",
                   r."Status", r."DeletedAt", r."WorkflowTemplateId", r."CurrentStepIndex", r."MetadataJson"::text
            FROM "Rugs" r
            WHERE r."TenantId" = @tenant AND r."Id" = ANY(@ids)
            """;

        return await ShipmentSql.QueryAsync(db, sql, r => new RugLockedValues(r.GetGuid(0), r.GetBoolean(1),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [nameof(Rug.IntakeLength)] = r.GetDecimal(2),
                    [nameof(Rug.IntakeWidth)] = r.GetDecimal(3),
                    [nameof(Rug.FinalLength)] = ShipmentSql.NullableDecimal(r, 4),
                    [nameof(Rug.FinalWidth)] = ShipmentSql.NullableDecimal(r, 5),
                    [nameof(Rug.LengthMeters)] = r.GetDecimal(6),
                    [nameof(Rug.WidthMeters)] = r.GetDecimal(7),
                    [nameof(Rug.Quantity)] = r.GetInt32(8),
                    [nameof(Rug.SerialNo)] = ShipmentSql.NullableString(r, 9),
                    [nameof(Rug.SerialFrom)] = r.IsDBNull(10) ? null : r.GetInt32(10),
                    [nameof(Rug.SerialTo)] = r.IsDBNull(11) ? null : r.GetInt32(11),
                    [nameof(Rug.SerialSort)] = r.IsDBNull(12) ? null : r.GetInt64(12),
                    [nameof(Rug.Title)] = ShipmentSql.NullableString(r, 13),
                    [nameof(Rug.Origin)] = ShipmentSql.NullableString(r, 14),
                    [nameof(Rug.Pattern)] = ShipmentSql.NullableString(r, 15),
                    [nameof(Rug.Material)] = ShipmentSql.NullableString(r, 16),
                    [nameof(Rug.KnotDensity)] = r.IsDBNull(17) ? null : r.GetInt32(17),
                    [nameof(Rug.Status)] = (RugStatus)r.GetInt32(18),
                    [nameof(Rug.DeletedAt)] = r.IsDBNull(19) ? null : r.GetFieldValue<DateTimeOffset>(19),
                    [nameof(Rug.WorkflowTemplateId)] = r.IsDBNull(20) ? null : r.GetGuid(20),
                    [nameof(Rug.CurrentStepIndex)] = r.GetInt32(21),
                    [nameof(Rug.MetadataJson)] = ShipmentSql.NullableString(r, 22)
                }),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId), ShipmentSql.Uuids("ids", rugIds));
    }

    public async Task<IReadOnlyList<string>> ExportableCustomKeysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT "Key" FROM "CustomFieldDefinitions"
            WHERE "TenantId" = @tenant AND "IsActive" AND "Exportable" AND "Scope" = 0
            ORDER BY "Key"
            """;
        return await ShipmentSql.QueryAsync(db, sql, r => r.GetString(0), cancellationToken, ShipmentSql.Uuid("tenant", tenantId));
    }

    public async Task<ShipmentLedgerSettings> SettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT "DeclaredValueKeyField", "DeclaredValueFallbackUsd", "BaleTerm" FROM "TenantPreferences" WHERE "TenantId" = @tenant
            """;
        var rows = await ShipmentSql.QueryAsync(db, sql,
            r => new ShipmentLedgerSettings(
                string.IsNullOrWhiteSpace(r.GetString(0)) ? TenantPreferences.DefaultDeclaredValueKeyField : r.GetString(0),
                ShipmentSql.NullableDecimal(r, 1),
                (BaleTerm)r.GetInt32(2)),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId));
        return rows.Count > 0
            ? rows[0]
            : new ShipmentLedgerSettings(TenantPreferences.DefaultDeclaredValueKeyField, null, BaleTerm.Negleh);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> RatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT "KeyNormalized", "UsdPerM2" FROM "DeclaredValueRates" WHERE "TenantId" = @tenant
            """;
        var rows = await ShipmentSql.QueryAsync(db, sql, r => (Key: r.GetString(0), Rate: r.GetDecimal(1)),
            cancellationToken, ShipmentSql.Uuid("tenant", tenantId));
        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (key, rate) in rows)
            rates[key] = rate;
        return rates;
    }

    public async Task<(ShipmentLedgerSettings Settings, IReadOnlyDictionary<string, decimal> Rates)> ValuationSourcesAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        // دو SELECT در یک دستور (دو مجموعهٔ نتیجه): یک رفت‌وبرگشت به‌جای دو
        const string sql = """
            SELECT "DeclaredValueKeyField", "DeclaredValueFallbackUsd", "BaleTerm" FROM "TenantPreferences" WHERE "TenantId" = @tenant;
            SELECT "KeyNormalized", "UsdPerM2" FROM "DeclaredValueRates" WHERE "TenantId" = @tenant;
            """;

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = ShipmentSql.CreateCommand(db, sql, [ShipmentSql.Uuid("tenant", tenantId)]);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var settings = new ShipmentLedgerSettings(TenantPreferences.DefaultDeclaredValueKeyField, null, BaleTerm.Negleh);
            if (await reader.ReadAsync(cancellationToken))
            {
                settings = new ShipmentLedgerSettings(
                    string.IsNullOrWhiteSpace(reader.GetString(0)) ? TenantPreferences.DefaultDeclaredValueKeyField : reader.GetString(0),
                    ShipmentSql.NullableDecimal(reader, 1),
                    (BaleTerm)reader.GetInt32(2));
            }

            await reader.NextResultAsync(cancellationToken);
            var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            while (await reader.ReadAsync(cancellationToken))
                rates[reader.GetString(0)] = reader.GetDecimal(1);
            return (settings, rates);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<LineValueInput>> LineInputsAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? lineIds, string keyField, CancellationToken cancellationToken)
    {
        if (lineIds is { Count: 0 }) return [];

        var sql = $"""
            SELECT l."Id", l."ShipmentId", l."RugId", s."Status", r."AreaM2", r."Quantity", l."UsdPerM2Override",
                   {ShipmentSql.KeyValue(keyField, "r")},
                   v."LineId" IS NOT NULL, v."ShipmentId", v."UsdPerM2", v."RateSource", v."PieceUsd", v."LineAreaM2", v."LineUsd"
            FROM "ShipmentLines" l
            JOIN "Shipments" s ON s."Id" = l."ShipmentId"
            JOIN "Rugs" r ON r."Id" = l."RugId"
            LEFT JOIN "ShipmentLineValues" v ON v."LineId" = l."Id"
            WHERE l."TenantId" = @tenant {(lineIds is null ? "" : "AND l.\"Id\" = ANY(@lines)")}
            ORDER BY l."Id"
            """;

        var parameters = new List<NpgsqlParameter> { ShipmentSql.Uuid("tenant", tenantId) };
        if (lineIds is not null) parameters.Add(ShipmentSql.Uuids("lines", lineIds));

        return await ShipmentSql.QueryAsync(db, sql,
            r => new LineValueInput(
                r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), (ShipmentStatus)r.GetInt32(3), r.GetDecimal(4), r.GetInt32(5),
                ShipmentSql.NullableDecimal(r, 6), ShipmentSql.NullableString(r, 7),
                r.GetBoolean(8), r.IsDBNull(9) ? null : r.GetGuid(9), ShipmentSql.NullableDecimal(r, 10),
                r.IsDBNull(11) ? DeclaredRateSource.Missing : (DeclaredRateSource)r.GetInt32(11),
                ShipmentSql.NullableDecimal(r, 12), r.IsDBNull(13) ? 0m : r.GetDecimal(13), ShipmentSql.NullableDecimal(r, 14)),
            cancellationToken, ShipmentSql.WithKey(keyField, [.. parameters]));
    }

    public async Task<int> WriteValuesAsync(
        Guid tenantId, IReadOnlyList<LineValueRow> rows, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return 0;

        const string sql = """
            INSERT INTO "ShipmentLineValues"
                ("LineId", "TenantId", "ShipmentId", "UsdPerM2", "RateSource", "PieceUsd", "LineAreaM2", "LineUsd", "ComputedAt")
            SELECT s.line_id, @tenant, s.shipment_id, s.usd, s.src, s.piece, s.area, s.line_usd, @now
            FROM unnest(@ids, @ships, @usd, @src, @piece, @area, @lineusd)
                AS s(line_id, shipment_id, usd, src, piece, area, line_usd)
            ON CONFLICT ("LineId") DO UPDATE SET
                "TenantId" = EXCLUDED."TenantId",
                "ShipmentId" = EXCLUDED."ShipmentId",
                "UsdPerM2" = EXCLUDED."UsdPerM2",
                "RateSource" = EXCLUDED."RateSource",
                "PieceUsd" = EXCLUDED."PieceUsd",
                "LineAreaM2" = EXCLUDED."LineAreaM2",
                "LineUsd" = EXCLUDED."LineUsd",
                "ComputedAt" = EXCLUDED."ComputedAt"
            """;

        return await ExecuteAsync(sql, cancellationToken,
            ShipmentSql.Uuid("tenant", tenantId),
            new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now.ToUniversalTime() },
            ShipmentSql.Uuids("ids", rows.Select(r => r.LineId)),
            ShipmentSql.Uuids("ships", rows.Select(r => r.ShipmentId)),
            ShipmentSql.Decimals("usd", rows.Select(r => r.UsdPerM2)),
            new NpgsqlParameter("src", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = rows.Select(r => (int)r.RateSource).ToArray() },
            ShipmentSql.Decimals("piece", rows.Select(r => r.PieceUsd)),
            ShipmentSql.Decimals("area", rows.Select(r => (decimal?)r.LineAreaM2)),
            ShipmentSql.Decimals("lineusd", rows.Select(r => r.LineUsd)));
    }

    /// <summary>
    /// تنها نوشتن خام محموله، همین‌جا (نه در کمک مشترک) تا آزمون معماری «نوشتن فقط در *LedgerRepository» آن را مجاز ببیند.
    /// روی همان اتصال و تراکنش DbContext.
    /// </summary>
    private async Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = ShipmentSql.CreateCommand(db, sql, parameters);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}

/// <summary>
/// تکه‌های SQL مشترک محموله: مقدار «فیلد کلید» ارزش گمرکی از ستون‌های فرش (فهرست سفید ستون‌ها؛ کلید فیلد سفارشی فقط
/// پارامتر است، هرگز در متن SQL) و کمک‌های اجرای دستور روی اتصال و تراکنش DbContext.
/// </summary>
internal static class ShipmentSql
{
    /// <summary>نام پارامتر کلید فیلد سفارشی.</summary>
    public const string KeyParameter = "keyname";

    /// <summary>
    /// عبارت SQL مقدار فیلد کلید روی فرشِ با نام مستعار <paramref name="alias"/>: <c>rug.origin</c> ← «Origin»، … ،
    /// <c>rug.custom.{k}</c> ← <c>MetadataJson -&gt;&gt; @keyname</c>؛ کلید ناشناخته ← NULL (نرخ «ندارد» مگر پیش‌فرض).
    /// همان قاعدهٔ <see cref="DeclaredValueMath.KeyValueOf"/> در C#.
    /// </summary>
    public static string KeyValue(string keyField, string alias) => keyField switch
    {
        RugFieldKeys.Origin => $"{alias}.\"Origin\"",
        RugFieldKeys.Pattern => $"{alias}.\"Pattern\"",
        RugFieldKeys.Material => $"{alias}.\"Material\"",
        RugFieldKeys.Title => $"{alias}.\"Title\"",
        _ when DeclaredValueMath.CustomKeyOf(keyField) is not null
            => $"(CASE WHEN jsonb_typeof({alias}.\"MetadataJson\") = 'object' THEN {alias}.\"MetadataJson\" ->> @{KeyParameter} END)",
        _ => "NULL::text"
    };

    /// <summary>پارامترها به‌اضافهٔ کلید فیلد سفارشی اگر عبارت آن را لازم دارد.</summary>
    public static NpgsqlParameter[] WithKey(string keyField, params NpgsqlParameter[] parameters)
        => DeclaredValueMath.CustomKeyOf(keyField) is { } custom
            ? [.. parameters, new NpgsqlParameter(KeyParameter, NpgsqlDbType.Text) { Value = custom }]
            : parameters;

    public static NpgsqlParameter Uuid(string name, Guid value) => new(name, NpgsqlDbType.Uuid) { Value = value };

    public static NpgsqlParameter Uuids(string name, IEnumerable<Guid> values)
        => new(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = values.ToArray() };

    public static NpgsqlParameter Decimals(string name, IEnumerable<decimal?> values)
        => new(name, NpgsqlDbType.Array | NpgsqlDbType.Numeric) { Value = values.ToArray() };

    public static string? NullableString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static decimal? NullableDecimal(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    /// <summary>پرس‌وجوی خواندنی روی همان اتصال و تراکنش DbContext.</summary>
    public static async Task<List<T>> QueryAsync<T>(
        AppDbContext db, string sql, Func<DbDataReader, T> map, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = CreateCommand(db, sql, parameters);
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

    public static NpgsqlCommand CreateCommand(AppDbContext db, string sql, NpgsqlParameter[] parameters)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
        command.Parameters.AddRange(parameters);
        return command;
    }
}
