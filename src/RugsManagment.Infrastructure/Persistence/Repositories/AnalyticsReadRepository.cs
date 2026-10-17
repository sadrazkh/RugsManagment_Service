using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Analytics;
using RugsManagment.Domain.Common;
using RugsManagment.Infrastructure.Persistence.Ledger;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>تجمیع‌های تحلیل روی سرور؛ حجم دادهٔ برگشتی برابر سطرهای گزارش است، نه کل موجودیت‌های کارگاه.</summary>
public sealed class AnalyticsReadRepository(AppDbContext db) : IAnalyticsReadRepository
{
    public async Task<AgingReportDto> AgingAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            WITH active AS (
                SELECT r."Id", r."Sku", r."Title", t."NameFa", s."StartedAt", p."Name" AS provider,
                  floor(extract(epoch FROM (@now - COALESCE(s."StartedAt", r."UpdatedAt", r."CreatedAt")))/86400)::int AS days,
                  t."ExpectedDurationDays" AS expected
                FROM "Rugs" r
                JOIN LATERAL (SELECT * FROM "RugWorkflowSteps" s WHERE s."RugId"=r."Id" AND s."Status"=1
                    ORDER BY s."OrderIndex", s."Id" LIMIT 1) s ON true
                JOIN "ProcessStepTypes" t ON t."Id"=s."ProcessStepTypeId"
                LEFT JOIN "ServiceProviders" p ON p."Id"=s."ServiceProviderId" AND p."TenantId"=@tenant
                WHERE r."TenantId"=@tenant AND r."DeletedAt" IS NULL AND r."Status" NOT IN (3,4)
            ), classified AS (
                SELECT *, CASE WHEN expected > 0 THEN
                  CASE WHEN days >= expected*3 THEN 3 WHEN days >= expected*2 THEN 2 WHEN days > expected THEN 1 ELSE 0 END
                  ELSE CASE WHEN days>=30 THEN 3 WHEN days>=14 THEN 2 WHEN days>=7 THEN 1 ELSE 0 END END AS severity
                FROM active
            ) SELECT "Id", "Sku", "Title", "NameFa", days, "StartedAt", provider, severity
              FROM classified WHERE severity>0 ORDER BY days DESC, "Id"
            """;
        var items = await QueryAsync(sql, r => new AgingItemDto(r.GetGuid(0), r.GetString(1), Text(r, 2), r.GetString(3),
            r.GetInt32(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), Text(r, 6), (AgingSeverity)r.GetInt32(7)),
            ct, new NpgsqlParameter("tenant", tenantId), new NpgsqlParameter("now", DateTimeOffset.UtcNow));
        return new AgingReportDto(items, items.Count(i => i.Severity == AgingSeverity.Warning),
            items.Count(i => i.Severity == AgingSeverity.Serious), items.Count(i => i.Severity == AgingSeverity.Critical));
    }

    public async Task<AnalyticsReportDto> ReadAsync(Guid tenantId, int months, CancellationToken ct = default)
    {
        var aging = await AgingAsync(tenantId, ct);
        var stepSql = $$"""
            SELECT t."Id", t."NameFa", count(*) FILTER (WHERE s."Status"=2)::int,
              COALESCE(sum({{CostSql.StepEffectiveCost("s")}}) FILTER (WHERE s."Status"=2),0),
              avg(extract(epoch FROM (s."CompletedAt"-s."StartedAt"))/86400)
                FILTER (WHERE s."Status"=2 AND s."CompletedAt" IS NOT NULL AND s."StartedAt" IS NOT NULL),
              count(*) FILTER (WHERE s."Status"=1)::int
            FROM "RugWorkflowSteps" s JOIN "Rugs" r ON r."Id"=s."RugId"
              JOIN "ProcessStepTypes" t ON t."Id"=s."ProcessStepTypeId"
            WHERE r."TenantId"=@tenant AND r."DeletedAt" IS NULL
            GROUP BY t."Id",t."NameFa" ORDER BY 4 DESC, t."Id"
            """;
        var steps = await QueryAsync(stepSql, r => new StepBreakdownDto(r.GetGuid(0), r.GetString(1), r.GetInt32(2), r.GetDecimal(3),
            r.GetInt32(2) == 0 ? 0m : Num.Round(r.GetDecimal(3) / r.GetInt32(2), 0),
            r.IsDBNull(4) ? null : Math.Round((double)r.GetDecimal(4), 1), r.GetInt32(5)), ct, new NpgsqlParameter("tenant", tenantId));

        var cursor = JalaliCalendarHelper.StartOfCurrentJalaliMonthUtc();
        var buckets = Enumerable.Range(0, Math.Clamp(months, 3, 36)).Reverse().Select(i => (
            Start: JalaliCalendarHelper.AddJalaliMonths(cursor, -i), End: JalaliCalendarHelper.AddJalaliMonths(cursor, -i + 1))).ToList();
        var parameters = new List<NpgsqlParameter> { new("tenant", tenantId) };
        var valueRows = new List<string>();
        for (var i = 0; i < buckets.Count; i++)
        {
            valueRows.Add($"({i},@start{i},@end{i})");
            parameters.Add(new NpgsqlParameter($"start{i}", buckets[i].Start));
            parameters.Add(new NpgsqlParameter($"end{i}", buckets[i].End));
        }
        var trendSql = "WITH buckets(idx, start_at, end_at) AS (VALUES " + string.Join(",", valueRows) + ") " + """
            SELECT b.idx,
              (SELECT count(*)::int FROM "Rugs" r WHERE r."TenantId"=@tenant AND r."DeletedAt" IS NULL
                AND r."CreatedAt">=b.start_at AND r."CreatedAt"<b.end_at),
              count(s."Id")::int, COALESCE(sum(s."SalePrice"-s."Discount"),0),
              COALESCE(sum(s."SalePrice"-s."Discount"-COALESCE(s."CostAtSale",ct."Total",0)),0)
            FROM buckets b
            LEFT JOIN "RugSales" s ON s."TenantId"=@tenant AND s."SoldAt">=b.start_at AND s."SoldAt"<b.end_at
              AND EXISTS(SELECT 1 FROM "Rugs" r WHERE r."Id"=s."RugId" AND r."TenantId"=@tenant AND r."DeletedAt" IS NULL)
            LEFT JOIN "RugCostTotals" ct ON ct."RugId"=s."RugId" AND ct."TenantId"=@tenant
            GROUP BY b.idx,b.start_at,b.end_at ORDER BY b.idx
            """;
        var trend = await QueryAsync(trendSql, r =>
        {
            var bucket = buckets[r.GetInt32(0)];
            return new TrendPointDto(JalaliCalendarHelper.SortKey(bucket.Start), JalaliCalendarHelper.MonthLabel(bucket.Start),
                r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3), r.GetDecimal(4));
        }, ct, parameters.ToArray());
        return new AnalyticsReportDto(aging, steps, trend);
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, CancellationToken ct, params NpgsqlParameter[] parameters)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var parameter in parameters) command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var result = new List<T>();
            while (await reader.ReadAsync(ct)) result.Add(map(reader));
            return result;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    private static string? Text(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
