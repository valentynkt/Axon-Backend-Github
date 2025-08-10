namespace BuildingBlocks.Application;

/// <summary>
/// Application-level transaction token (commit/rollback).
/// Implemented by infrastructure; passed around by use cases if needed.
/// </summary>
public interface ITransaction : IDisposable
{
    /// <summary>Unique identifier for this transaction (for logging/tracing).</summary>
    string TransactionId { get; }

    /// <summary>Commit all changes in this transaction.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rollback all changes in this transaction.</summary>
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>True if the transaction has been committed or rolled back.</summary>
    bool IsCompleted { get; }
}