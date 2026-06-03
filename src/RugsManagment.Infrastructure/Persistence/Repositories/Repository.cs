using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Common;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>عملیات پایه CRUD روی هر موجودیت — کلاس‌های تخصصی از این ارث می‌برند</summary>
public class Repository<T>(AppDbContext db) : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Db = db;
    protected readonly DbSet<T> Set = db.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await Set.FindAsync([id], cancellationToken);

    public virtual async Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = Set;
        if (predicate is not null)
            query = query.Where(predicate);
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await Set.AddAsync(entity, cancellationToken);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Remove(T entity) => Set.Remove(entity);
}
