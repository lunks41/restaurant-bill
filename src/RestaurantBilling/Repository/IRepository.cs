using System.Linq.Expressions;

namespace Repository;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}
