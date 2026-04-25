using System.Linq.Expressions;
using Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Repository;

public class Repository<T>(AppDbContext db) : IRepository<T> where T : class
{
    private readonly DbSet<T> _set = db.Set<T>();

    public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => await _set.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        IQueryable<T> q = _set;
        if (predicate is not null) q = q.Where(predicate);
        return await q.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _set.AddAsync(entity, cancellationToken);

    public void Update(T entity) => _set.Update(entity);
    public void Delete(T entity) => _set.Remove(entity);
}
