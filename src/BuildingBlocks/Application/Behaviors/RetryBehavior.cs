using System.Collections.Concurrent;
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using System.Diagnostics.Metrics;
using System.Net;
using System.Reflection;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Advanced retry behavior with Polly integration for Epic 05.
/// Provides intelligent transient failure detection, exponential backoff with jitter,
/// circuit breaker pattern, and comprehensive telemetry.
/// </summary>
public sealed class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly ITransientFaultDetector _faultDetector;
    private readonly IRetryPolicyResolver _policyResolver;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private readonly IOptions<RetryOptions> _options;
    
    // OpenTelemetry metrics
    private static readonly Counter<long> RetryAttempts = TelemetryTags.Metrics.CreateCounter<long>(
        "axon.retry.attempts",
        description: "Total retry attempts");
    private static readonly Histogram<double> RetryDelay = TelemetryTags.Metrics.CreateHistogram<double>(
        "axon.retry.delay",
        unit: "ms", 
        description: "Delay between retry attempts");
    private static readonly Counter<long> CircuitBreakerOpens = TelemetryTags.Metrics.CreateCounter<long>(
        "axon.circuit_breaker.opens",
        description: "Circuit breaker open events");

    // Circuit breaker cache per request type
    private static readonly ConcurrentDictionary<string, ICircuitBreakerPolicy> CircuitBreakers = new();

    public RetryBehavior(
        ITransientFaultDetector faultDetector,
        IRetryPolicyResolver policyResolver,
        ILogger<RetryBehavior<TRequest, TResponse>> logger,
        IOptions<RetryOptions> options)
    {
        _faultDetector = faultDetector ?? throw new ArgumentNullException(nameof(faultDetector));
        _policyResolver = policyResolver ?? throw new ArgumentNullException(nameof(policyResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        // Get retry policy for this request type
        var retryPolicy = GetRetryPolicy(request);
        if (retryPolicy == null)
        {
            _logger.LogDebug("No retry policy configured for {RequestType}, proceeding without retry",
                typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        _logger.LogDebug("Executing {RequestType} with retry policy: MaxAttempts={MaxAttempts}",
            typeof(TRequest).Name, retryPolicy.MaxAttempts);

        // Build Polly resilience pipeline
        var resiliencePipeline = BuildResiliencePipeline(retryPolicy);

        try
        {
            var result = await resiliencePipeline.ExecuteAsync(
                async (ctx) => await next(ctx),
                cancellationToken);

            return result;
        }
        catch (CircuitBreakerRejectedException ex)
        {
            _logger.LogWarning("Request {RequestType} rejected by circuit breaker: {Message}",
                typeof(TRequest).Name, ex.Message);

            CircuitBreakerOpens.Add(1, new TagList
            {
                { "request.type", typeof(TRequest).Name },
                { "reason", "circuit_breaker_open" }
            });

            // Return circuit breaker failure
            var error = Error.External("Circuit breaker is open", "CIRCUIT_BREAKER_OPEN");
            return CreateFailureResponse<TResponse>(error);
        }
    }

    /// <summary>
    /// Get retry policy for the request type using multiple resolution strategies.
    /// </summary>
    private RetryPolicy? GetRetryPolicy(TRequest request)
    {
        // 1. Check if request implements IRetryable interface
        if (request is IRetryable retryableRequest)
        {
            return retryableRequest.GetRetryPolicy();
        }

        // 2. Check for RetryableAttribute
        var retryableAttribute = typeof(TRequest).GetCustomAttribute<RetryableAttribute>();
        if (retryableAttribute != null)
        {
            return new RetryPolicy
            {
                MaxAttempts = retryableAttribute.MaxAttempts,
                InitialDelay = TimeSpan.FromMilliseconds(retryableAttribute.InitialDelayMs),
                UseCircuitBreaker = retryableAttribute.UseCircuitBreaker
            };
        }

        // 3. Check policy resolver for type-specific configuration
        return _policyResolver.GetPolicy(typeof(TRequest).Name);
    }

    /// <summary>
    /// Build Polly resilience pipeline with retry and circuit breaker strategies.
    /// </summary>
    private ResiliencePipeline<TResponse> BuildResiliencePipeline(RetryPolicy policy)
    {
        var pipelineBuilder = new ResiliencePipelineBuilder<TResponse>();

        // Add retry strategy
        pipelineBuilder.AddRetry(new RetryStrategyOptions<TResponse>
        {
            MaxRetryAttempts = policy.MaxAttempts,
            Delay = policy.InitialDelay,
            MaxDelay = policy.MaxDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            JitterType = JitterType.Decorrelated,
            ShouldHandle = new PredicateBuilder<TResponse>()
                .HandleResult(response => IsTransientFailure(response))
                .Handle<Exception>(ex => _faultDetector.IsTransient(ex)),
            OnRetry = async (args) =>
            {
                var attemptNumber = args.AttemptNumber + 1;
                var delay = args.Duration.TotalMilliseconds;
                
                _logger.LogWarning(
                    "Retry attempt {AttemptNumber}/{MaxAttempts} for {RequestType} after {DelayMs}ms delay",
                    attemptNumber, policy.MaxAttempts, typeof(TRequest).Name, delay);

                // Record metrics
                RetryAttempts.Add(1, new TagList
                {
                    { "request.type", typeof(TRequest).Name },
                    { "attempt", attemptNumber },
                    { "max_attempts", policy.MaxAttempts }
                });

                RetryDelay.Record(delay, new TagList
                {
                    { "request.type", typeof(TRequest).Name },
                    { "attempt", attemptNumber }
                });

                await Task.CompletedTask;
            }
        });

        // Add circuit breaker if enabled
        if (policy.UseCircuitBreaker)
        {
            var circuitBreakerKey = typeof(TRequest).Name;
            
            pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResponse>
            {
                FailureRatio = 0.5, // Open when 50% of requests fail
                SamplingDuration = policy.CircuitBreakerDuration,
                MinimumThroughput = policy.CircuitBreakerThreshold,
                BreakDuration = policy.CircuitBreakerDuration,
                ShouldHandle = new PredicateBuilder<TResponse>()
                    .HandleResult(response => IsTransientFailure(response))
                    .Handle<Exception>(ex => _faultDetector.IsTransient(ex)),
                OnOpened = async (args) =>
                {
                    _logger.LogError("Circuit breaker opened for {RequestType}: {Reason}",
                        typeof(TRequest).Name, args.Outcome?.Exception?.Message ?? "Unknown");

                    CircuitBreakerOpens.Add(1, new TagList
                    {
                        { "request.type", typeof(TRequest).Name },
                        { "state", "opened" }
                    });

                    await Task.CompletedTask;
                },
                OnClosed = async (args) =>
                {
                    _logger.LogInformation("Circuit breaker closed for {RequestType}",
                        typeof(TRequest).Name);
                    await Task.CompletedTask;
                },
                OnHalfOpened = async (args) =>
                {
                    _logger.LogInformation("Circuit breaker half-opened for {RequestType}",
                        typeof(TRequest).Name);
                    await Task.CompletedTask;
                }
            });
        }

        return pipelineBuilder.Build();
    }

    /// <summary>
    /// Determine if a Result response indicates a transient failure.
    /// </summary>
    private static bool IsTransientFailure(TResponse response)
    {
        if (response.IsSuccess)
            return false;

        var error = response.Error;
        return error?.Type is ErrorType.External or ErrorType.Internal &&
               error?.Code?.Contains("TIMEOUT") == true ||
               error?.Code?.Contains("TRANSIENT") == true;
    }

    /// <summary>
    /// Create a failure response with the appropriate type.
    /// </summary>
    private static TResponse CreateFailureResponse<T>(Error error) where T : IResult
    {
        var responseType = typeof(T);
        
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(Error) });
                
            if (failureMethod != null)
            {
                var result = failureMethod.Invoke(null, new object[] { error });
                return (TResponse)result!;
            }
        }
        
        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }
        
        throw new InvalidOperationException(
            $"RetryBehavior can only be used with Result or Result<T> response types. " +
            $"Got: {responseType.Name}");
    }
}

// Supporting interfaces and types for retry behavior

/// <summary>
/// Interface for requests that want to configure their own retry policy.
/// </summary>
public interface IRetryable
{
    RetryPolicy GetRetryPolicy();
}

/// <summary>
/// Attribute for declarative retry configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RetryableAttribute : Attribute
{
    public int MaxAttempts { get; init; } = 3;
    public int InitialDelayMs { get; init; } = 100;
    public bool UseCircuitBreaker { get; init; } = true;
}

/// <summary>
/// Retry policy configuration.
/// </summary>
public sealed class RetryPolicy
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public double BackoffMultiplier { get; init; } = 2.0;
    public double JitterFactor { get; init; } = 0.2;
    public bool UseCircuitBreaker { get; init; } = true;
    public int CircuitBreakerThreshold { get; init; } = 5;
    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Resolves retry policies for request types.
/// </summary>
public interface IRetryPolicyResolver
{
    RetryPolicy? GetPolicy(string requestTypeName);
}

/// <summary>
/// Detects transient failures that should be retried.
/// </summary>
public interface ITransientFaultDetector
{
    bool IsTransient(Exception exception);
}

/// <summary>
/// Configuration options for retry behavior.
/// </summary>
public sealed class RetryOptions
{
    public bool EnableRetry { get; set; } = true;
    public int DefaultMaxAttempts { get; set; } = 3;
    public TimeSpan DefaultInitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan DefaultMaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}