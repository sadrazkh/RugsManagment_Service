using Microsoft.EntityFrameworkCore;
using Npgsql;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;

namespace RugsManagment.Infrastructure.Persistence;

/// <summary>
/// یک تراکنش واحد — همهٔ Repositoryها همان DbContext را share می‌کنند؛ SaveChanges یکجا commit می‌کند.
///
/// همچنین استثناهای EF/Npgsql را به استثناهای دامنه‌ای ترجمه می‌کند تا لایهٔ Application
/// (که عمداً به EF Core وابسته نیست) بتواند آن‌ها را بگیرد و پیام فارسی مناسب برگرداند.
/// </summary>
public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    /// <summary>کد خطای PostgreSQL برای نقض قید یکتایی</summary>
    private const string UniqueViolation = "23505";

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            throw new DuplicateKeyException();
        }
    }
}
