using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Minimal, safe retry behavior for **idempotent queries only**.
/// - Opt-in via interface or attribute
/// - Exponential backoff + jitter (Polly v8)
/// - Adds Activity events per retry (observability), but no extra metrics/counters
/// - Never wraps/duplicates circuit breakers (those belong on HttpClient/DB)
/// - Rethrows cancellations
/// </summary>
public sealed class QueryRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
    where TResponse : IResult
{
    private readonly ILogger<QueryRetryBehavior<TRequest, TResponse>> _logger;
    private readonly RetryOptions _defaults;

    public QueryRetryBehavior(
        ILogger<QueryRetryBehavior<TRequest, TResponse>> logger,
        IOptions<RetryOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaults = options?.Value ?? new RetryOptions();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var policy = ResolvePolicy(request, _defaults);
        if (!policy.Enabled || policy.MaxAttempts <= 0 || policy.InitialDelay <= TimeSpan.Zero)
        {
            return await next(); // no retry configured
        }

        var builder = new ResiliencePipelineBuilder<TResponse>();

        builder.AddRetry(new RetryStrategyOptions<TResponse>
        {
            MaxRetryAttempts = policy.MaxAttempts,
            Delay = policy.InitialDelay,
            MaxDelay = policy.MaxDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<TResponse>()
                .HandleResult(ShouldRetryResult)
                .Handle<Exception>(),
            OnRetry = args =>
            {
                // Add a lightweight span event; ObservabilityBehavior handles the rest
                Activity.Current?.AddEvent(new ActivityEvent(
                    "axon.retry",
                    tags: new ActivityTagsCollection
                    {
                        ["request.type"] = typeof(TRequest).Name,
                        ["attempt"] = args.AttemptNumber + 1,
                        ["max_attempts"] = policy.MaxAttempts,
                        ["delay.ms"] = args.Duration.TotalMilliseconds
                    }));

                _logger.LogWarning("Retry {Attempt}/{Max} for {RequestType} after {Delay}ms",
                    args.AttemptNumber + 1, policy.MaxAttempts, typeof(TRequest).Name, args.Duration.TotalMilliseconds);

                return default; // Task.CompletedTask in Polly v8 callbacks
            }
        });

        var pipeline = builder.Build();

        try
        {
            // MediatR delegate has no token parameter; Polly handles cancellation via outer token
            return await pipeline.ExecuteAsync(async _ => await next(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Never convert cancellations
            throw;
        }
    }

    private static bool ShouldRetryResult(TResponse response)
    {
        if (response.IsSuccess) return false;

        // Conservative default: only retry on external/transient signals
        var err = response.Error;
        if (err is null) return false;

        if (err.Type is ErrorType.External or ErrorType.Internal)
        {
            var code = err.Code ?? string.Empty;
            // very simple heuristics; tune per your error catalog
            if (code.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase)) return true;
            if (code.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase)) return true;
            if (code.Contains("THROTTLE", StringComparison.OrdinalIgnoreCase)) return true;
            if (code == "429") return true;
        }

        return false;
    }

    private static EffectiveRetryPolicy ResolvePolicy(TRequest request, RetryOptions defaults)
    {
        // 1) interface opt-in
        if (request is IRetryableQuery rqi)
        {
            var p = rqi.GetRetryPolicy();
            return new EffectiveRetryPolicy(true, p.MaxAttempts, p.InitialDelay, p.MaxDelay);
        }

        // 2) attribute opt-in
        var attr = request.GetType().GetCustomAttributes(typeof(RetryableAttribute), inherit: true)
            .OfType<RetryableAttribute>()
            .FirstOrDefault();

        if (attr is not null)
        {
            return new EffectiveRetryPolicy(
                Enabled: true,
                MaxAttempts: attr.MaxAttempts,
                InitialDelay: TimeSpan.FromMilliseconds(attr.InitialDelayMs),
                MaxDelay: TimeSpan.FromMilliseconds(attr.MaxDelayMs <= 0 ? defaults.DefaultMaxDelay.TotalMilliseconds : attr.MaxDelayMs));
        }

        // 3) global default (off unless explicitly enabled)
        return new EffectiveRetryPolicy(
            Enabled: defaults.EnableQueryRetry,
            MaxAttempts: defaults.DefaultMaxAttempts,
            InitialDelay: defaults.DefaultInitialDelay,
            MaxDelay: defaults.DefaultMaxDelay);
    }

    private readonly record struct EffectiveRetryPolicy(bool Enabled, int MaxAttempts, TimeSpan InitialDelay, TimeSpan MaxDelay);
}

/// <summary>
/// Queries can programmatically define retry policy.
/// Keep this to queries only; do not implement on commands.
/// </summary>
public interface IRetryableQuery
{
    RetryPolicy GetRetryPolicy();
}

/// <summary> Attribute to opt-in query retry. </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RetryableAttribute : Attribute
{
    public int MaxAttempts { get; init; } = 3;
    public int InitialDelayMs { get; init; } = 100;
    public int MaxDelayMs { get; init; } = 30_000;
}

/// <summary> Simple policy DTO for programmatic opt-in. </summary>
public sealed class RetryPolicy
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary> Global defaults; keep retry OFF unless explicitly enabled. </summary>
public sealed class RetryOptions
{
    public bool EnableQueryRetry { get; set; }  // default OFF for safety
    public int DefaultMaxAttempts { get; set; } = 3;
    public TimeSpan DefaultInitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan DefaultMaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
