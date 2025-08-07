namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Defines a retry policy for handling transient failures.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes the specified operation with retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelay">Delay between retries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation</returns>
    Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation, 
        int maxRetries, 
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}