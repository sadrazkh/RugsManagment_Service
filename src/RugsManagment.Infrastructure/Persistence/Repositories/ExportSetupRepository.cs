using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Infrastructure.Persistence.Repositories;
public sealed class ExportSetupRepository(AppDbContext db) : IExportSetupRepository
{
    public Task<bool> HasGridAsync(Guid tenantId, string gridId, CancellationToken ct)
        => db.Set<TenantGridLayout>().AnyAsync(x => x.TenantId == tenantId && x.GridId == gridId,ct);
    public async Task<ExportSetupState> ReadAsync(Guid tenantId, string keyField, CancellationToken ct)
    {
        var preset = await db.AuditEntries.AnyAsync(x => x.TenantId == tenantId && x.Action == AuditAction.PresetApplied,ct);
        var serials = await db.Rugs.AnyAsync(x => x.TenantId == tenantId && x.SerialNo != null,ct);
        var rates = await db.Set<DeclaredValueRate>().Where(x => x.TenantId == tenantId).Select(x => x.KeyNormalized).ToListAsync(ct);
        var rugs = await db.Rugs.AsNoTracking().Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.Sale == null &&
            (!db.Set<ShipmentLine>().Any(l => l.TenantId == tenantId && l.RugId == x.Id) || db.Set<ShipmentLine>().Any(l => l.TenantId == tenantId && l.RugId == x.Id && l.Shipment.Status == ShipmentStatus.Open)))
            .Select(x => new { x.Origin, x.Pattern, x.Material, x.MetadataJson }).ToListAsync(ct);
        var values = rugs.Select(x => keyField switch { "rug.origin" => x.Origin, "rug.pattern" => x.Pattern, "rug.material" => x.Material,
            _ => keyField.StartsWith("rug.custom.",StringComparison.Ordinal) ? DeclaredValueMath.MetadataValue(x.MetadataJson,keyField[11..]) : null });
        var missing = values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Where(x => !rates.Contains(DeclaredValueMath.KeyNormalize(x!))).Cast<string>().Order().ToArray();
        var merchant = await db.Users.AnyAsync(x => x.TenantId == tenantId && x.IsActive && x.Role == UserRole.Merchant && x.UiMode == UiMode.Simple && x.TextScale >= TextScale.Large,ct);
        var exports = await db.Set<ExportRun>().AnyAsync(x => x.TenantId == tenantId,ct);
        var labels = await db.LabelTemplates.Where(x => x.TenantId == tenantId).Select(x => x.ElementsJson).ToListAsync(ct);
        return new ExportSetupState(preset,serials,rates.Count > 0,missing,merchant,exports,labels.Any(x => x != null && x.Contains("rug.serial",StringComparison.Ordinal) && x.Contains("qr",StringComparison.OrdinalIgnoreCase)));
    }
}
