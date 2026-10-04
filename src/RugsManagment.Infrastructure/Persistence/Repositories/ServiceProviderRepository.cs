using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Providers;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Ledger;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// طرف‌های خدمات (قالیشوی، رفوگر، …) و صورت‌حسابشان.
///
/// <para><b>از فاز ۲:</b> دو جای قدیمیِ فرمول «هزینهٔ مؤثر مرحله» (جاهای ۵ و ۶ در جدول D.4: مانده‌ها و ردیف‌های کار)
/// فرمول خودشان را ندارند و همان <see cref="CostSql.StepEffectiveCost"/> را به کار می‌برند؛ فاز هزینه‌ها رسیدهای ثبت
/// هزینه را با union به همین کوئری‌ها اضافه می‌کند. هر مرحله از راه فرشش به کارگاه بسته است (E17): مانده‌ها قبلاً فقط
/// با شناسهٔ طرف فیلتر می‌شدند و مرحله‌ای که شناسهٔ طرفِ کارگاه دیگری داشت به حساب او می‌رفت. مرحلهٔ فرشِ سطل زباله
/// شمرده نمی‌شود (همان فیلتر حذف نرم).</para>
/// </summary>
public class ServiceProviderRepository(AppDbContext db) : Repository<ServiceProvider>(db), IServiceProviderRepository
{
    public async Task<IReadOnlyList<ServiceProvider>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceProvider>> ListAllWithRatesAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders.AsNoTracking()
            .Include(p => p.Rates).ThenInclude(r => r.ProcessStepType)
            .Where(p => p.TenantId == tenantId)
            // فعال‌ها اول، بعد بر اساس نام
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<ServiceProvider?> GetWithRatesAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders
            .Include(p => p.Rates).ThenInclude(r => r.ProcessStepType)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);

    /// <summary>
    /// مانده‌حساب همهٔ طرف‌ها در یک کوئری: کار انجام‌شده و در جریان (هزینهٔ مؤثر هر مرحله با کف صفر)، پرداخت‌ها و تاریخ
    /// آخرین پرداخت.
    /// </summary>
    public async Task<IReadOnlyList<ProviderBalanceDto>> ListBalancesAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var steps = $"""
            FROM "RugWorkflowSteps" s JOIN "Rugs" r ON r."Id" = s."RugId"
            WHERE s."ServiceProviderId" = p."Id" AND r."TenantId" = @tenant AND r."DeletedAt" IS NULL
            """;

        var sql = $"""
            SELECT p."Id" AS "ProviderId", p."Name" AS "ProviderName", p."IsActive" AS "IsActive", p."Phone" AS "Phone",
                   COALESCE((SELECT SUM({CostSql.StepEffectiveCost("s")}) {steps} AND {CostSql.StepCompleted("s")}), 0) AS "CompletedWorkTotal",
                   COALESCE((SELECT SUM({CostSql.StepEffectiveCost("s")}) {steps} AND {CostSql.StepInProgress("s")}), 0) AS "InProgressTotal",
                   COALESCE((SELECT SUM(x."Amount") FROM "ProviderPayments" x WHERE x."ServiceProviderId" = p."Id"), 0) AS "PaidTotal",
                   (SELECT count(*) {steps} AND {CostSql.StepCompleted("s")})::int AS "CompletedStepCount",
                   (SELECT count(*) {steps} AND {CostSql.StepInProgress("s")})::int AS "InProgressStepCount",
                   (SELECT max(x."PaidAt") FROM "ProviderPayments" x WHERE x."ServiceProviderId" = p."Id") AS "LastPaymentAt"
            FROM "ServiceProviders" p
            WHERE p."TenantId" = @tenant
            ORDER BY p."IsActive" DESC, p."Name", p."Id"
            """;

        var rows = await Db.Database
            .SqlQueryRaw<BalanceRow>(sql, new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProviderBalanceDto(
                r.ProviderId, r.ProviderName, r.IsActive, r.Phone, r.CompletedWorkTotal, r.InProgressTotal, r.PaidTotal,
                r.CompletedStepCount, r.InProgressStepCount, r.LastPaymentAt))
            .ToList();
    }

    /// <summary>کارهای انجام‌شده و در جریان یک طرف — ردیف‌های صورت‌حساب؛ تازه‌ترین اول، کار بی‌تاریخ بالای فهرست.</summary>
    public async Task<IReadOnlyList<ProviderWorkItemDto>> ListWorkAsync(
        Guid tenantId, Guid providerId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT s."Id" AS "StepId", s."RugId" AS "RugId", r."Sku" AS "RugSku", r."Title" AS "RugTitle",
                   t."NameFa" AS "StepNameFa", s."CompletedAt" AS "CompletedAt", s."Status" AS "Status",
                   {CostSql.StepEffectiveCost("s")} AS "Cost"
            FROM "RugWorkflowSteps" s
            JOIN "Rugs" r ON r."Id" = s."RugId"
            JOIN "ProcessStepTypes" t ON t."Id" = s."ProcessStepTypeId"
            WHERE s."ServiceProviderId" = @provider AND r."TenantId" = @tenant AND r."DeletedAt" IS NULL
                AND {CostSql.StepCounts("s")}
            ORDER BY COALESCE(s."CompletedAt", s."StartedAt") DESC, s."Id"
            """;

        var rows = await Db.Database
            .SqlQueryRaw<WorkRow>(sql,
                new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId },
                new NpgsqlParameter("provider", NpgsqlDbType.Uuid) { Value = providerId })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProviderWorkItemDto(
                r.StepId, r.RugId, r.RugSku, r.RugTitle, r.StepNameFa, r.CompletedAt, (WorkflowStepStatus)r.Status, r.Cost))
            .ToList();
    }

    public async Task<bool> HasWorkHistoryAsync(Guid providerId, CancellationToken cancellationToken = default)
        => await Db.RugWorkflowSteps.IgnoreQueryFilters().AnyAsync(s => s.ServiceProviderId == providerId, cancellationToken)
           || await Db.ProviderPayments.AnyAsync(p => p.ServiceProviderId == providerId, cancellationToken);

    /// <summary>ردیف خامِ کوئری مانده‌ها (نام ستون‌ها = نام ویژگی‌ها).</summary>
    private sealed class BalanceRow
    {
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Phone { get; set; }
        public decimal CompletedWorkTotal { get; set; }
        public decimal InProgressTotal { get; set; }
        public decimal PaidTotal { get; set; }
        public int CompletedStepCount { get; set; }
        public int InProgressStepCount { get; set; }
        public DateTimeOffset? LastPaymentAt { get; set; }
    }

    /// <summary>ردیف خامِ کوئری کارها.</summary>
    private sealed class WorkRow
    {
        public Guid StepId { get; set; }
        public Guid RugId { get; set; }
        public string RugSku { get; set; } = string.Empty;
        public string? RugTitle { get; set; }
        public string StepNameFa { get; set; } = string.Empty;
        public DateTimeOffset? CompletedAt { get; set; }
        public int Status { get; set; }
        public decimal Cost { get; set; }
    }
}
