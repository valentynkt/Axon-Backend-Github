namespace BuildingBlocks.Persistence.Common;

/// <summary>
/// Interface for handling different transaction behavior patterns
/// Provides abstraction for various transaction management strategies
/// </summary>
public interface ITransactionBehaviorHandler
{
    /// <summary>
    /// Execute an operation within the configured transaction behavior pattern
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the execution</returns>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute an operation within the configured transaction behavior pattern and return result
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the execution with result</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the transaction behavior type this handler implements
    /// </summary>
    TransactionBehavior BehaviorType { get; }
    
    /// <summary>
    /// Check if the handler currently has an active transaction
    /// </summary>
    bool HasActiveTransaction { get; }
}