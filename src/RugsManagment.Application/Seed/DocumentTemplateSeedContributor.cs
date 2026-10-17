using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.Seed;
public sealed class DocumentTemplateSeedContributor(IDocumentTemplateRepository templates) : ITenantSeedContributor
{
    public int Order => 400;
    public async Task SeedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await templates.ListAsync(tenantId,cancellationToken);
        foreach (var seed in LayoutJson.Defaults().GroupBy(x => x.SeedKey).Select(g => g.OrderByDescending(x => x.Version).First()))
        {
            if (existing.Any(x => x.SeedKey == seed.SeedKey)) continue;
            var layout = seed.Parse(); var kind = Enum.Parse<DocumentKind>(layout.Kind);
            var item = new DocumentTemplate { TenantId = tenantId, Kind = kind, SeedKey = seed.SeedKey, LayoutJson = seed.Json,
                Name = kind switch { DocumentKind.ShipmentDeclaration => "صورت نگله‌بندی و اظهار", DocumentKind.RugList => "فهرست فرش‌ها", _ => "هزینهٔ محموله" },
                IsDefault = !existing.Any(x => x.Kind == kind && x.IsDefault), UsesMoneyFields = kind == DocumentKind.ShipmentCost || LayoutValidator.Validate(layout,DocumentFields.Catalogue()).UsesMoneyFields };
            await templates.AddAsync(item,cancellationToken);
            await templates.AddRevisionAsync(new DocumentTemplateRevision { TemplateId = item.Id, Revision = 1, LayoutJson = item.LayoutJson },cancellationToken);
        }
    }
}
