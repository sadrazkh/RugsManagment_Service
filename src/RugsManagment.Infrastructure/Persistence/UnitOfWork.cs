using RugsManagment.Application.Abstractions;

namespace RugsManagment.Infrastructure.Persistence;

/// <summary>
/// یک تراکنش واحد — همهٔ Repositoryها همان DbContext را share می‌کنند؛ SaveChanges یکجا commit می‌کند.
/// </summary>
public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
