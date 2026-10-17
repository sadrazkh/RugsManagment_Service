using RugsManagment.Application.Features.Costs;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Ledger;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>عضویتِ همهٔ ثبت‌های یک پاس در یک پرس‌وجو خوانده می‌شود تا تعداد فرش‌ها تعداد دستورها را زیاد نکند.</summary>
internal sealed record CostMembership(
    Guid Id, Guid? BatchId, Guid? ShipmentId, Guid? BaleId, string? Serial, long? SerialSort, int Quantity,
    decimal IntakeArea, decimal EffectiveArea, decimal? LineUsd, decimal PurchaseCost, bool HasPurchaseLines,
    Guid[] CostlyStepTypes, DateTimeOffset? FinalizedAt)
{
    public bool Matches(CostScopeSpec s) => s.Scope switch
    {
        CostScope.Selection => s.RugIds.Contains(Id),
        CostScope.Bale => BaleId is { } b && s.BaleIds.Contains(b),
        CostScope.Shipment => s.ShipmentId != null && ShipmentId == s.ShipmentId,
        CostScope.IntakeBatch when s.Group == CostGroup.Purchase => s.RugIds.Contains(Id),
        CostScope.IntakeBatch => s.BatchId != null && BatchId == s.BatchId,
        _ => false
    };
    public AllocationMember Member(Guid? stepType, DateTimeOffset createdAt)
        => new(Id, Serial, SerialSort, Quantity, IntakeArea, EffectiveArea, LineUsd, PurchaseCost, HasPurchaseLines,
            stepType is { } step && CostlyStepTypes.Contains(step), FinalizedAt is { } finalized && finalized < createdAt);

    public static async Task<IReadOnlyList<CostMembership>> LoadAsync(AppDbContext db, Guid tenant, IReadOnlyList<CostScopeSpec> scopes, CancellationToken ct)
    {
        if (scopes.Count == 0) return [];
        var sql = $"""
            SELECT r."Id", r."BatchId", l."ShipmentId", l."BaleId", COALESCE(r."SerialNo", r."Sku"), r."SerialSort",
                r."Quantity", r."IntakeAreaM2", r."AreaM2", v."LineUsd", COALESCE(t."PurchaseCost", 0),
                EXISTS(SELECT 1 FROM "RugPurchaseLines" p WHERE p."TenantId" = @tenant AND p."RugId" = r."Id" AND p."Amount" <> 0),
                ARRAY(SELECT st."ProcessStepTypeId" FROM "RugWorkflowSteps" st WHERE st."RugId" = r."Id" AND {CostSql.StepCounts("st")} AND {CostSql.StepEffectiveCost("st")} <> 0),
                s."FinalizedAt"
            FROM "Rugs" r
            LEFT JOIN "ShipmentLines" l ON l."RugId" = r."Id" AND l."TenantId" = @tenant
            LEFT JOIN "ShipmentLineValues" v ON v."LineId" = l."Id" AND v."TenantId" = @tenant
            LEFT JOIN "Shipments" s ON s."Id" = l."ShipmentId" AND s."TenantId" = @tenant
            LEFT JOIN "RugCostTotals" t ON t."RugId" = r."Id" AND t."TenantId" = @tenant
            WHERE r."TenantId" = @tenant AND r."DeletedAt" IS NULL
                AND (r."Id" = ANY(@rugs) OR l."BaleId" = ANY(@bales) OR l."ShipmentId" = ANY(@ships) OR r."BatchId" = ANY(@batches))
            """;
        return await ShipmentSql.QueryAsync(db, sql, r => new CostMembership(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1),
            r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetInt64(5),
            r.GetInt32(6), r.GetDecimal(7), r.GetDecimal(8), ShipmentSql.NullableDecimal(r, 9), r.GetDecimal(10), r.GetBoolean(11),
            r.GetFieldValue<Guid[]>(12), r.IsDBNull(13) ? null : r.GetFieldValue<DateTimeOffset>(13)), ct,
            ShipmentSql.Uuid("tenant", tenant), ShipmentSql.Uuids("rugs", scopes.SelectMany(s => s.RugIds).Distinct()),
            ShipmentSql.Uuids("bales", scopes.SelectMany(s => s.BaleIds).Distinct()),
            ShipmentSql.Uuids("ships", scopes.Where(s => s.Scope == CostScope.Shipment && s.ShipmentId != null).Select(s => s.ShipmentId!.Value).Distinct()),
            ShipmentSql.Uuids("batches", scopes.Where(s => s.Scope == CostScope.IntakeBatch && s.Group == CostGroup.Expense && s.BatchId != null).Select(s => s.BatchId!.Value).Distinct()));
    }
}
