using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Application.DTOs.Import;
namespace RugsManagment.Application.Features.Import;
public interface IImportJobRepository
{
    Task<ImportJob?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<ImportJob>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<ImportJob?> DuplicateAsync(Guid tenantId, string sha, Guid exceptId, CancellationToken ct);
    Task<ImportMappingPreset?> MappingAsync(Guid tenantId, string signature, CancellationToken ct);
    Task<IReadOnlyList<Rug>> RugsAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<ShipmentLine>> LinesAsync(Guid tenantId, IReadOnlyList<Guid> rugIds, CancellationToken ct);
    Task<IReadOnlyList<string>> UndoBlockersAsync(Guid tenantId, ImportJob job, IReadOnlyList<Guid> rugIds,
        IReadOnlyList<Guid> baleIds, CancellationToken ct);
    Task AddAsync<T>(T entity, CancellationToken ct) where T : BaseEntity;
    void Remove<T>(T entity) where T : BaseEntity;
    Task<Bale?> BaleAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Shipment?> ShipmentAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<RugBatch?> BatchAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task LockUndoAsync(Guid tenantId, ImportJob job, IReadOnlyList<Guid> rugIds, CancellationToken ct);
    Task<ImportRelatedState> RelatedAsync(Guid tenantId, ImportJob job, IReadOnlyList<Guid> rugIds, CancellationToken ct);
}
