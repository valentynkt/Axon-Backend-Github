using Microsoft.Extensions.Logging;
using Polly;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Polly-based retry policy implementation that provides robust retry capabilities
/// with exponential backoff for both Result pattern and exception-throwing operations.
/// Refactored to follow SOLID principles and eliminate unnecessary dependencies.
/// </summary>
public sealed class PollyRetryPolicy : IRetryPolicy
{
    private readonly ILogger<PollyRetryPolicy> _logger;

    public PollyRetryPolicy(ILogger<PollyRetryPolicy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        Func<Task<Result<T>>> operation, 
        int maxRetries, 
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var retryPolicy = Policy
            .HandleResult<Result<T>>(result => result.IsFailure)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => CalculateDelay(retryDelay, retryAttempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry attempt {RetryCount}/{MaxRetries} after {Delay}ms. Error: {Error}",
                        retryCount, maxRetries, timespan.TotalMilliseconds, outcome.Result.Error?.Message);
                });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await operation();
        });
    }

    public async Task<Result<T>> ExecuteWithExceptionHandlingAsync<T>(
        Func<Task<T>> operation, 
        int maxRetries, 
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var retryPolicy = Policy
            .Handle<Exception>(TransientFaultClassifier.IsTransientError)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => CalculateDelay(retryDelay, retryAttempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry attempt {RetryCount}/{MaxRetries} after {Delay}ms. Exception: {Exception}",
                        retryCount, maxRetries, timespan.TotalMilliseconds, outcome.Exception?.Message);
                });

        try
        {
            var result = await retryPolicy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await operation();
            });

            return Result<T>.Success(result);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Operation failed after {MaxRetries} retries", maxRetries);
            return Result<T>.Failure(Error.Failure("RetryPolicy.ExecutionFailed", 
                $"Operation failed after {maxRetries} retries: {ex.Message}"));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation was cancelled");
            return Result<T>.Failure(Error.Failure("RetryPolicy.OperationCancelled", 
                "Operation was cancelled"));
        }
    }

    /// <summary>
    /// Calculates exponential backoff delay with jitter to prevent thundering herd.
    /// </summary>
    private static TimeSpan CalculateDelay(TimeSpan baseDelay, int retryAttempt)
    {
        var exponentialDelay = TimeSpan.FromMilliseconds(
            baseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1));
        
        // Add jitter to prevent thundering herd (±25% random variance)
        var jitter = Random.Shared.NextDouble() * 0.5 + 0.75; // 0.75 to 1.25
        return TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds * jitter);
    }
}