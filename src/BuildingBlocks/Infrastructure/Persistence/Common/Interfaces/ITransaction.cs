namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// Represents a database transaction with explicit commit/rollback operations.
/// Ensures unit-of-work contract compliance and prevents double-commit scenarios.
/// </summary>
public interface ITransaction : IDisposable
{
    /// <summary>
    /// Unique identifier for this transaction.
    /// </summary>
    string TransactionId { get; }
    
    /// <summary>
    /// Commit all changes in this transaction.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Rollback all changes in this transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Indicates whether the transaction has been committed or rolled back.
    /// </summary>
    bool IsCompleted { get; }
}