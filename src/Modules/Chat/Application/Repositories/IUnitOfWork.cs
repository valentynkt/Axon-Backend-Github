namespace Axon.Modules.Chat.Application.Repositories;

/// <summary>
/// Unit of Work interface for Chat module following SPARC architecture patterns
/// Extended to include transaction management capabilities for consistency with other modules
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