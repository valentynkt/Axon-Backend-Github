using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Pipeline behavior that adds retry logic to requests implementing IRetryableRequest.
/// Uses the configured IRetryPolicy to handle transient failures.
/// </summary>
public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;

    public RetryBehavior(IRetryPolicy retryPolicy, ILogger<RetryBehavior<TRequest, TResponse>> logger)
    {
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IRetryableRequest retryable)
        {
            // No retry configuration found, so just continue through the pipeline
            return await next(cancellationToken);
        }

        _logger.LogDebug("Executing {RequestType} with retry policy: MaxRetries={MaxRetries}, RetryDelay={RetryDelay}",
            typeof(TRequest).Name, retryable.MaxRetries, retryable.RetryDelay);

        return await _retryPolicy.ExecuteAsync(
            () => next(cancellationToken), 
            retryable.MaxRetries, 
            retryable.RetryDelay,
            cancellationToken);
    }
}