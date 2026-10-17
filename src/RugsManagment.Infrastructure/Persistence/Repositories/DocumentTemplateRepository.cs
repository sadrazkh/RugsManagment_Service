using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Documents;
using RugsManagment.Domain.Entities;
namespace RugsManagment.Infrastructure.Persistence.Repositories;
public sealed class DocumentTemplateRepository(AppDbContext db) : IDocumentTemplateRepository
{
    public async Task<IReadOnlyList<DocumentTemplate>> ListAsync(Guid tenantId, CancellationToken ct)
        => await db.Set<DocumentTemplate>().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<DocumentTemplate?> GetTrackedAsync(Guid tenantId, Guid id, CancellationToken ct)
        => db.Set<DocumentTemplate>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public uint Version(DocumentTemplate template) => db.Entry(template).Property<uint>("xmin").CurrentValue;
    public async Task AddAsync(DocumentTemplate template, CancellationToken ct) => await db.AddAsync(template, ct);
    public void Remove(DocumentTemplate template) => db.Remove(template);
    public async Task AddRevisionAsync(DocumentTemplateRevision revision, CancellationToken ct) => await db.AddAsync(revision, ct);
    public async Task<IReadOnlyList<RevisionDto>> RevisionsAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await db.Set<DocumentTemplateRevision>().Where(x => x.TemplateId == id && db.Set<DocumentTemplate>().Any(t => t.Id == id && t.TenantId == tenantId))
            .OrderByDescending(x => x.Revision).Select(x => new RevisionDto(x.Revision, x.SavedAt, x.SavedBy)).ToListAsync(ct);
    public Task<string?> RevisionAsync(Guid tenantId, Guid id, int revision, CancellationToken ct)
        => db.Set<DocumentTemplateRevision>().Where(x => x.TemplateId == id && x.Revision == revision && db.Set<DocumentTemplate>().Any(t => t.Id == id && t.TenantId == tenantId))
            .Select(x => x.LayoutJson).FirstOrDefaultAsync(ct);
    public async Task AddRunAsync(ExportRun run, CancellationToken ct) => await db.AddAsync(run, ct);
    public async Task<IReadOnlyList<ExportRunDto>> RunsAsync(Guid tenantId, Guid shipmentId, bool includeMoney, CancellationToken ct)
        => await (from x in db.Set<ExportRun>() where x.TenantId == tenantId && x.ShipmentId == shipmentId
            join t in db.Set<DocumentTemplate>().Where(t => t.TenantId == tenantId) on x.TemplateId equals t.Id into templates
            from t in templates.DefaultIfEmpty()
            where includeMoney || t != null && !t.UsesMoneyFields
            join u in db.Users.Where(u => u.TenantId == tenantId) on x.UserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby x.At descending
            select new ExportRunDto(x.Id, x.At, u == null ? "سیستم" : u.FullName, t == null ? "قالب حذف‌شده" : t.Name,
                x.TemplateRevision, x.Format, x.IsDraft, x.DataHash, x.FileSha256)).Take(100).ToListAsync(ct);
}
