using System.Linq.Expressions;
using RugsManagment.Domain.Common;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>قرارداد عمومی خواندن/نوشتن روی دیتابیس — پیاده‌سازی در Infrastructure</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}
