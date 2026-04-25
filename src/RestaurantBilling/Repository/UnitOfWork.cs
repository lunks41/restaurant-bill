using Data.Persistence;

namespace Repository;

public class UnitOfWork(AppDbContext db)
{
    private readonly Dictionary<Type, object> _repos = [];

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (_repos.TryGetValue(type, out var repo))
        {
            return (IRepository<T>)repo;
        }

        var created = new Repository<T>(db);
        _repos[type] = created;
        return created;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
