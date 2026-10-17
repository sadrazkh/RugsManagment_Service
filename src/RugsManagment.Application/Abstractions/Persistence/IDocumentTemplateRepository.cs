using RugsManagment.Domain.Entities;
using RugsManagment.Application.DTOs.Documents;
namespace RugsManagment.Application.Abstractions.Persistence;
public interface IDocumentTemplateRepository
{
    Task<IReadOnlyList<DocumentTemplate>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<DocumentTemplate?> GetTrackedAsync(Guid tenantId, Guid id, CancellationToken ct);
    uint Version(DocumentTemplate template);
    Task AddAsync(DocumentTemplate template, CancellationToken ct);
    void Remove(DocumentTemplate template);
    Task AddRevisionAsync(DocumentTemplateRevision revision, CancellationToken ct);
    Task<IReadOnlyList<RevisionDto>> RevisionsAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<string?> RevisionAsync(Guid tenantId, Guid id, int revision, CancellationToken ct);
    Task AddRunAsync(ExportRun run, CancellationToken ct);
    Task<IReadOnlyList<ExportRunDto>> RunsAsync(Guid tenantId, Guid shipmentId, bool includeMoney, CancellationToken ct);
}
