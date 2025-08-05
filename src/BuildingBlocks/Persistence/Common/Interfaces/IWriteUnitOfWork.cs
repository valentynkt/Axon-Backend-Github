using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Write-side Unit of Work interface for CQRS command operations
/// Handles transactional consistency and domain event processing
/// </summary>
public interface IWriteUnitOfWork : IDisposable
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
    /// Check if there is an active transaction
    /// </summary>
    bool HasActiveTransaction { get; }
    
    /// <summary>
    /// Get domain events from all aggregates for event processing
    /// </summary>
    IReadOnlyList<IDomainEvent> GetDomainEvents();
    
    /// <summary>
    /// Clear all domain events after processing
    /// </summary>
    void ClearDomainEvents();
    
    /// <summary>
    /// Execute operation within transaction scope
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute operation within transaction scope
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<Task> operation, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Typed Unit of Work interface for specific write context
/// </summary>
public interface IWriteUnitOfWork<out TWriteContext> : IWriteUnitOfWork 
    where TWriteContext : class, IWriteDbContext<object>
{
    /// <summary>
    /// Get the underlying write database context
    /// </summary>
    TWriteContext Context { get; }
}