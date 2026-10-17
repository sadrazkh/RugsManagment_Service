using RugsManagment.Application.Features.Documents.Data;
namespace RugsManagment.Application.Abstractions.Persistence;
public sealed record DocumentSource(DocumentData Data, string DataHash);
public interface IDocumentDataRepository
{
    Task<DocumentSource> ShipmentAsync(Guid tenantId, Guid shipmentId, bool includeMoney, CancellationToken ct);
    Task<DocumentSource> RugsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, bool includeMoney, CancellationToken ct);
}
