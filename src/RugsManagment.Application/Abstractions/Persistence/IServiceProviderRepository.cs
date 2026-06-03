using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>قالیشوی‌ها و رفوگرهای ثبت‌شده توسط کارگاه</summary>
public interface IServiceProviderRepository : IRepository<ServiceProvider>
{
    Task<IReadOnlyList<ServiceProvider>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
