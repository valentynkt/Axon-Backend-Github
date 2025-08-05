using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL database context interface
/// Equivalent to IMongoDbContext but for PostgreSQL with Entity Framework Core
/// </summary>
public interface IPostgresDbContext : IDisposable
{
    /// <summary>
    /// Get DbSet for entity type (PostgreSQL equivalent of GetCollection)
    /// </summary>
    DbSet<T> GetCollection<T>() where T : class;
    
    /// <summary>
    /// Get DbSet for entity type with optional table name
    /// </summary>
    DbSet<T> GetCollection<T>(string? name = null) where T : class;
    
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
    Task RollbackTransaction(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add command to execution queue for deferred execution
    /// Maintains same pattern as MongoDB implementation
    /// </summary>
    void AddCommand(Func<Task> func);
    
    /// <summary>
    /// Execute all queued commands within a transaction
    /// </summary>
    Task ExecuteQueuedCommandsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if context has active transaction
    /// </summary>
    bool HasActiveTransaction { get; }
    
    /// <summary>
    /// Get current transaction ID for tracking
    /// </summary>
    string? CurrentTransactionId { get; }
}