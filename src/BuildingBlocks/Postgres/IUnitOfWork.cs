using BuildingBlocks.Core.Model;

namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL Unit of Work interface
/// Maintains same contract as MongoDB IUnitOfWork for architectural consistency
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Save all changes to database and return number of affected records
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Begin a new database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if there are pending changes
    /// </summary>
    bool HasChanges { get; }
    
    /// <summary>
    /// Execute operation within transaction scope
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute operation within transaction scope
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic Unit of Work interface with typed context
/// Maintains same pattern as MongoDB implementation
/// </summary>
public interface IUnitOfWork<out TContext> : IUnitOfWork where TContext : IPostgresDbContext
{
    /// <summary>
    /// Get the underlying database context
    /// </summary>
    TContext Context { get; }
}

/// <summary>
/// PostgreSQL-specific Unit of Work interface
/// Equivalent to IMongoUnitOfWork for architectural consistency
/// </summary>
public interface IPostgresUnitOfWork<out TContext> : IUnitOfWork<TContext> where TContext : IPostgresDbContext
{
    /// <summary>
    /// Get repository for entity type (relaxed constraints for compatibility)
    /// </summary>
    IRepository<T, TId> GetRepository<T, TId>() where T : class;
    
    /// <summary>
    /// Get repository for entity type with Guid key
    /// </summary>
    IRepository<T> GetRepository<T>() where T : class, IEntity<Guid>;
    
    /// <summary>
    /// Get aggregate repository for domain aggregates with event support
    /// </summary>
    IAggregateRepository<T, TId> GetAggregateRepository<T, TId>() where T : class, IAggregate<TId>;
    
    /// <summary>
    /// Get read-only repository for entity type
    /// </summary>
    IReadRepository<T, TId> GetReadRepository<T, TId>() where T : class;
    
    /// <summary>
    /// Get write-only repository for entity type
    /// </summary>
    IWriteRepository<T, TId> GetWriteRepository<T, TId>() where T : class;
}

