namespace TrainHub.Application.Interfaces;

/// <summary>
/// Unit of Work contract — groups repository operations into a single transaction.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Returns the repository instance for the given entity type.
    /// Repositories are cached per Unit of Work lifetime.
    /// </summary>
    IGenericRepository<T> Repository<T>() where T : class;

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
