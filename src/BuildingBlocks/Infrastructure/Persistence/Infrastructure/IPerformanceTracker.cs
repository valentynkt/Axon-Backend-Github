using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence.Infrastructure;

/// <summary>
/// Performance tracking interface for database operations
/// Provides monitoring and metrics collection for persistence layer operations
/// </summary>
public interface IPerformanceTracker<TContext> where TContext : DbContext
{
    /// <summary>
    /// Track execution time and performance metrics for async operations with return value
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operationName">Name of the operation being tracked</param>
    /// <param name="operation">The async operation to execute and track</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the tracked operation</returns>
    Task<T> TrackAsync<T>(string operationName, Func<Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Track execution time and performance metrics for async operations without return value
    /// </summary>
    /// <param name="operationName">Name of the operation being tracked</param>
    /// <param name="operation">The async operation to execute and track</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TrackAsync(string operationName, Func<Task> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Track execution time and performance metrics for synchronous operations with return value
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operationName">Name of the operation being tracked</param>
    /// <param name="operation">The synchronous operation to execute and track</param>
    /// <returns>Result of the tracked operation</returns>
    T Track<T>(string operationName, Func<T> operation);
    
    /// <summary>
    /// Track execution time and performance metrics for synchronous operations without return value
    /// </summary>
    /// <param name="operationName">Name of the operation being tracked</param>
    /// <param name="operation">The synchronous operation to execute and track</param>
    void Track(string operationName, Action operation);
    
    /// <summary>
    /// Get performance metrics for the current context
    /// </summary>
    /// <returns>Performance metrics data</returns>
    Task<PerformanceMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Performance metrics data structure
/// </summary>
public class PerformanceMetrics
{
    public string ContextName { get; set; } = string.Empty;
    public int TotalOperations { get; set; }
    public long AverageExecutionTimeMs { get; set; }
    public long TotalExecutionTimeMs { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public DateTime LastOperationTime { get; set; }
    public Dictionary<string, OperationMetrics> OperationBreakdown { get; set; } = new();
}

/// <summary>
/// Metrics for individual operation types
/// </summary>
public class OperationMetrics
{
    public string OperationName { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public long AverageExecutionTimeMs { get; set; }
    public long MinExecutionTimeMs { get; set; }
    public long MaxExecutionTimeMs { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime LastExecuted { get; set; }
}