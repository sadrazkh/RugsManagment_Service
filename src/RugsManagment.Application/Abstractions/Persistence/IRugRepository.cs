using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>دسترسی دیتابیس به فرش — همیشه همراه TenantId برای امنیت</summary>
public interface IRugRepository : IRepository<Rug>
{
    Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Rug>> ListByTenantAsync(Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default);
    Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
