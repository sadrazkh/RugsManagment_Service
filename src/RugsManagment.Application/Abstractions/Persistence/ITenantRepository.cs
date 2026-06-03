using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>کارگاه‌ها — جستجو با slug هنگام ثبت مشتری</summary>
public interface ITenantRepository : IRepository<Tenant>
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
