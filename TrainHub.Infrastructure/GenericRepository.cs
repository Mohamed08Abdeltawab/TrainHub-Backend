using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrainHub.Application.Interfaces;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure;

/// <summary>
/// Generic repository implementation backed by EF Core.
/// </summary>
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly TrainHubDbContext Context;
    protected readonly DbSet<T> DbSet;

    //
    public GenericRepository(TrainHubDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = context.Set<T>();
    }

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public void Update(T entity)
    {
        DbSet.Update(entity);
    }

    /// <inheritdoc />
    public void Delete(T entity)
    {
        DbSet.Remove(entity);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbSet.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            // Detach so Find doesn't interfere with subsequent tracked queries
            Context.Entry(entity).State = EntityState.Detached;
        }
        return entity is not null;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(predicate, cancellationToken);
    }
}
