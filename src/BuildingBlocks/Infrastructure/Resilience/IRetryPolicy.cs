namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Defines a retry policy for handling transient failures using the Result pattern.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes the specified operation with retry logic, returning a Result.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute that returns a Result</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelay">Delay between retries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation wrapped in Result pattern</returns>
    Task<Result<T>> ExecuteAsync<T>(
        Func<Task<Result<T>>> operation, 
        int maxRetries, 
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the specified operation with retry logic for operations that may throw exceptions.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute that may throw exceptions</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelay">Delay between retries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation wrapped in Result pattern</returns>
    Task<Result<T>> ExecuteWithExceptionHandlingAsync<T>(
        Func<Task<T>> operation, 
        int maxRetries, 
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}