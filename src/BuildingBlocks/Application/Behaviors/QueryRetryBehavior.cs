// /BuildingBlocks/Application/Behaviors/QueryRetryBehavior.cs
#nullable enable
using System.Diagnostics;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Abstractions.CQRS.Policies; // RetryableAttribute (Polly-native)
using BuildingBlocks.Core.Diagnostics.Errors;          // Error, ErrorType
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Polly-first retry for idempotent queries returning Result&lt;T,Error&gt; (opt-in via [Retryable]).
/// No custom options; we translate the attribute directly to Polly's RetryStrategyOptions.
/// </summary>
public sealed class QueryRetryBehavior<TRequest, TValue>
    : IPipelineBehavior<TRequest, Result<TValue, Error>>
    where TRequest : IQuery<TValue>
{
    private readonly ILogger<QueryRetryBehavior<TRequest, TValue>> _logger;

    public QueryRetryBehavior(ILogger<QueryRetryBehavior<TRequest, TValue>> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<TValue, Error>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue, Error>> next,
        CancellationToken ct)
    {
        // Opt-in via [Retryable] attribute
        var attr = request.GetType()
            .GetCustomAttributes(typeof(RetryableAttribute), inherit: true)
            .OfType<RetryableAttribute>()
            .FirstOrDefault();

        if (attr is null || !attr.Enabled || attr.MaxRetryAttempts <= 0 || attr.Delay <= TimeSpan.Zero)
            return await next();

        // Build Polly strategy directly from attribute values
        var strategy = new RetryStrategyOptions<Result<TValue, Error>>
        {
            MaxRetryAttempts = attr.MaxRetryAttempts,
            Delay            = attr.Delay,
            MaxDelay         = attr.MaxDelay,
            BackoffType      = attr.BackoffType,
            UseJitter        = attr.UseJitter,

            // Retry only on transient conditions (based on your Core Error)
            ShouldHandle = new PredicateBuilder<Result<TValue, Error>>()
                .HandleResult(r =>
                    !r.IsSuccess &&
                    r.Error is { } e &&
                    (e.IsRetryable ||
                     e.Type is ErrorType.Timeout
                             or ErrorType.Unavailable
                             or ErrorType.Network
                             or ErrorType.External
                             or ErrorType.RateLimit))
                .Handle<Exception>(),

            // Lightweight observability hook
            OnRetry = args =>
            {
                Activity.Current?.AddEvent(new ActivityEvent("axon.retry", tags: new ActivityTagsCollection
                {
                    ["request.type"] = typeof(TRequest).FullName ?? typeof(TRequest).Name,
                    ["attempt"]      = args.AttemptNumber + 1,
                    ["delay.ms"]     = args.Duration.TotalMilliseconds
                }));
                _logger.LogWarning("Retry {Attempt}/{Max} for {Request} after {Delay}ms",
                    args.AttemptNumber + 1, attr.MaxRetryAttempts, typeof(TRequest).Name, args.Duration.TotalMilliseconds);
                return default; // Polly v8 callback (ValueTask default)
            }
        };

        var pipeline = new ResiliencePipelineBuilder<Result<TValue, Error>>()
            .AddRetry(strategy)
            .Build();

        try
        {
            // MediatR delegate has no token parameter; Polly handles cancellation via ct
            return await pipeline.ExecuteAsync(async _ => await next(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Never convert cancellations
            throw;
        }
    }
}
