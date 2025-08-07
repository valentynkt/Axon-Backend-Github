using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Implementation of IRetryPolicy using Polly for retry logic.
/// Handles transient exceptions with configurable retry attempts and delays.
/// </summary>
public class PollyRetryPolicy : IRetryPolicy
{
    private readonly ILogger<PollyRetryPolicy> _logger;

    public PollyRetryPolicy(ILogger<PollyRetryPolicy> logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation, 
        int maxRetries, 
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        var retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1)), // Exponential backoff
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry attempt {RetryCount}/{MaxRetries} after {Delay}ms. Exception: {Exception}",
                        retryCount, maxRetries, timespan.TotalMilliseconds, outcome.Exception?.Message);
                });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await operation();
        });
    }

    private static bool IsTransientException(Exception exception)
    {
        // Define what constitutes a transient exception
        return exception switch
        {
            TaskCanceledException => false, // Don't retry cancellations
            OperationCanceledException => false, // Don't retry cancellations
            TimeoutException => true,
            HttpRequestException => true,
            _ => false // Be conservative - don't retry by default
        };
    }
}