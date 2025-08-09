namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Abstraction for different transaction behavior strategies.
/// </summary>
public interface ITransactionBehaviorHandler
{
    // Existing simple-delegate overloads (kept for compatibility)
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    // New CT-friendly overloads (avoid closures, easier testing)
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Transaction behavior type implemented by this handler.</summary>
    TransactionBehavior BehaviorType { get; }

    /// <summary>True if a transaction is currently active.</summary>
    bool HasActiveTransaction { get; }
}