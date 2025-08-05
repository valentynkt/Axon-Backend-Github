using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Unified database context interface for all modules
/// Provides common transaction and persistence operations
/// </summary>
public interface IDbContext : IDisposable
{
    /// <summary>
    /// Get DbSet for entity type
    /// </summary>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    
    /// <summary>
    /// Save all pending changes to database
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
    /// Check if context has active transaction
    /// </summary>
    bool HasActiveTransaction { get; }
    
    /// <summary>
    /// Get current transaction ID for tracking
    /// </summary>
    string? CurrentTransactionId { get; }
    
    /// <summary>
    /// Create execution strategy for resilience
    /// </summary>
    IExecutionStrategy CreateExecutionStrategy();
    
    /// <summary>
    /// Execute operation within transaction scope
    /// </summary>
    Task ExecuteTransactionalAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute operation within transaction scope and return result
    /// </summary>
    Task<T> ExecuteTransactionalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get domain events from tracked aggregates
    /// </summary>
    IReadOnlyList<IDomainEvent> GetDomainEvents();
}