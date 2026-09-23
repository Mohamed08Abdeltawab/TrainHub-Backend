using System.Collections.Concurrent;
using TrainHub.Application.Interfaces;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure;

/// <summary>
/// Concrete Unit of Work backed by <see cref="TrainHubDbContext"/>.
/// Repositories are lazily created and cached for the lifetime of this instance.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly TrainHubDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private bool _disposed;

    public UnitOfWork(TrainHubDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public IGenericRepository<T> Repository<T>() where T : class
    {
        return (IGenericRepository<T>)_repositories.GetOrAdd(//if found get else add in concurntDictionary
            typeof(T),
            _ => new GenericRepository<T>(_context));//only on add
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _context.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _context.DisposeAsync();
            _disposed = true;
        }
    }
}
