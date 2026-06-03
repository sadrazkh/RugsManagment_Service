using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

public interface IRugBatchRepository : IRepository<RugBatch>
{
    Task<IReadOnlyList<RugBatch>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<RugBatch?> GetWithRugsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
