using Microsoft.Extensions.Logging;
using Polly;

namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Polly-based extension methods for retry operations.
/// Provides simple retry functionality with proper dependency injection.
/// </summary>
public static class PollyExtensions
{
    /// <summary>
    /// Executes an operation with retry logic using a provided logger.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="retrySource">The source object (unused but required for extension method)</param>
    /// <param name="action">The operation to execute</param>
    /// <param name="logger">Logger for retry attempts</param>
    /// <param name="retryCount">Maximum number of retries</param>
    /// <returns>The result of the operation</returns>
    public static T RetryOnFailure<T>(
        this object retrySource, 
        Func<T> action, 
        ILogger logger, 
        int retryCount = 3)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(logger);

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry(retryCount, (exception, retryAttempt, _) =>
            {
                logger.LogWarning("Retry attempt {RetryAttempt}/{MaxRetries}. Exception: {ExceptionMessage}",
                    retryAttempt, retryCount, exception.Message);
            });

        return retryPolicy.Execute(action);
    }

    /// <summary>
    /// Executes an async operation with retry logic using a provided logger.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="retrySource">The source object (unused but required for extension method)</param>
    /// <param name="action">The async operation to execute</param>
    /// <param name="logger">Logger for retry attempts</param>
    /// <param name="retryCount">Maximum number of retries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> RetryOnFailureAsync<T>(
        this object retrySource,
        Func<Task<T>> action,
        ILogger logger,
        int retryCount = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(logger);

        var retryPolicy = Policy
            .Handle<Exception>()
            .RetryAsync(retryCount, (exception, retryAttempt, _) =>
            {
                logger.LogWarning("Async retry attempt {RetryAttempt}/{MaxRetries}. Exception: {ExceptionMessage}",
                    retryAttempt, retryCount, exception.Message);
            });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await action();
        });
    }
}