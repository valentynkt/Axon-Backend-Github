# Story 05: RetryBehavior - ❌ NEEDS FULL IMPLEMENTATION

## Story Overview
**Story ID**: Epic_05_Story_05  
**Story Name**: RetryBehavior - Advanced Resilience  
**Estimated Duration**: **1-2 days**  
**Status**: **❌ STUB EXISTS - FULL IMPLEMENTATION REQUIRED**
**Dependencies**: 
- ✅ Epic_04 (CQRS Foundation)
- ❌ Microsoft.Extensions.Resilience (Polly integration) - needs installation
- ✅ Result pattern from Epic_03

## Current Implementation Status
**Location**: `src/BuildingBlocks/Infrastructure/Resilience/RetryBehavior.cs` (stub only)

**❌ CURRENT STATE:**
- Basic stub class exists ❌
- No Polly integration implemented ❌
- Missing all resilience functionality ❌
- No circuit breaker implementation ❌

**🎯 IMPLEMENTATION REQUIRED:**

## User Story
**As a developer**, I want sophisticated retry logic with circuit breakers so that transient failures are handled gracefully while preventing cascade failures.

## Acceptance Criteria - FULL IMPLEMENTATION NEEDED
- [ ] RetryBehavior class created with Polly integration
- [ ] Configurable retry policies per request type
- [ ] Intelligent transient vs permanent error detection
- [ ] Exponential backoff with jitter implementation
- [ ] Circuit breaker pattern to prevent cascading failures
- [ ] Comprehensive retry metrics via OpenTelemetry
- [ ] IRetryable interface for opt-in retry configuration
- [ ] Unit and integration tests for all failure scenarios

## Technical Implementation

### Core Components

#### 1. RetryBehavior Class
```csharp
namespace Axon.BuildingBlocks.Infrastructure.Resilience;

public sealed class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly IRetryPolicyResolver _policyResolver;
    private readonly ITransientFaultDetector _faultDetector;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private readonly IMetrics _metrics;
    
    private static readonly Counter<long> RetryAttempts = Metrics.CreateCounter<long>(
        "axon.retry.attempts",
        description: "Total retry attempts");
    private static readonly Histogram<double> RetryDelay = Metrics.CreateHistogram<double>(
        "axon.retry.delay",
        unit: "ms",
        description: "Delay between retry attempts");
    private static readonly Counter<long> CircuitBreakerOpens = Metrics.CreateCounter<long>(
        "axon.circuit_breaker.opens",
        description: "Circuit breaker open events");
}
```

#### 2. Retry Configuration
```csharp
namespace Axon.BuildingBlocks.Infrastructure.Resilience;

public interface IRetryable
{
    RetryPolicy GetRetryPolicy();
}

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

[AttributeUsage(AttributeTargets.Class)]
public sealed class RetryableAttribute : Attribute
{
    public int MaxAttempts { get; init; } = 3;
    public int InitialDelayMs { get; init; } = 100;
    public bool UseCircuitBreaker { get; init; } = true;
}
```

### Tasks

#### Task 1: Create RetryBehavior Infrastructure
- [ ] Create `src/BuildingBlocks/Infrastructure/Resilience/RetryBehavior.cs`
- [ ] Integrate with Microsoft.Extensions.Resilience
- [ ] Create IRetryable interface and RetryableAttribute
- [ ] Implement IRetryPolicyResolver for policy lookup

#### Task 2: Transient Fault Detection
- [ ] Create ITransientFaultDetector interface
- [ ] Implement detection for database errors (deadlocks, timeouts)
- [ ] Detect HTTP transient errors (503, 429, timeout)
- [ ] Handle Azure/AWS SDK transient exceptions
- [ ] Support custom transient error registration

#### Task 3: Exponential Backoff with Jitter
- [ ] Implement exponential delay calculation
- [ ] Add jitter using decorrelated jitter algorithm
- [ ] Apply maximum delay cap
- [ ] Support immediate first retry for specific errors
- [ ] Log calculated delays for debugging

#### Task 4: Circuit Breaker Implementation
- [ ] Configure circuit breaker per request type
- [ ] Track failure rates over sliding window
- [ ] Implement half-open state logic
- [ ] Support manual circuit reset via API
- [ ] Log state transitions

#### Task 5: Polly Pipeline Configuration
- [ ] Create resilience pipelines dynamically
- [ ] Combine retry and circuit breaker strategies
- [ ] Add timeout strategy for long-running operations
- [ ] Support bulkhead isolation
- [ ] Configure hedging for critical operations

#### Task 6: Comprehensive Metrics
- [ ] Count retry attempts by request type
- [ ] Track success rate after retries
- [ ] Measure cumulative retry delay
- [ ] Monitor circuit breaker state changes
- [ ] Record final outcome (success/failure/timeout)

#### Task 7: Advanced Features
- [ ] Implement retry with modified request
- [ ] Support fallback responses
- [ ] Add retry budget to prevent overload
- [ ] Implement adaptive retry based on success rate
- [ ] Support distributed circuit breaker state

#### Task 8: Testing
- [ ] Unit tests for retry logic
- [ ] Integration tests with real failures
- [ ] Circuit breaker state machine tests
- [ ] Jitter distribution validation
- [ ] Load tests for thundering herd prevention
- [ ] Chaos engineering tests

## Code Examples

### Request with Retry Configuration
```csharp
[Retryable(MaxAttempts = 3, InitialDelayMs = 100, UseCircuitBreaker = true)]
public record SendEmailCommand(string To, string Subject, string Body) 
    : ICommand<Result<Unit>>;

// Or implementing IRetryable for dynamic configuration
public record ProcessPaymentCommand(decimal Amount) 
    : ICommand<Result<PaymentResult>>, IRetryable
{
    public RetryPolicy GetRetryPolicy() => new()
    {
        MaxAttempts = Amount > 1000 ? 5 : 3,
        InitialDelay = TimeSpan.FromMilliseconds(200),
        UseCircuitBreaker = true
    };
}
```

### Transient Error Detection
```csharp
public sealed class TransientFaultDetector : ITransientFaultDetector
{
    public bool IsTransient(Exception exception) => exception switch
    {
        SqlException sqlEx => IsTransientSqlError(sqlEx),
        HttpRequestException httpEx => true,
        TaskCanceledException => false,
        TimeoutException => true,
        OperationCanceledException => false,
        _ when exception.InnerException != null => IsTransient(exception.InnerException),
        _ => false
    };
    
    private static bool IsTransientSqlError(SqlException sqlEx) =>
        sqlEx.Number is 
            1205 or  // Deadlock
            1222 or  // Lock timeout
            49918 or // Cannot process request. Not enough resources
            49919 or // Cannot process create or update request. Too many operations
            49920;   // Cannot process request. Too many resources
}
```

### Polly Configuration
```csharp
services.AddResiliencePipeline<string>("default", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions<Result>
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<Result>()
                .HandleResult(r => !r.IsSuccess && IsTransientError(r.Error))
                .Handle<Exception>(ex => _faultDetector.IsTransient(ex))
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<Result>
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder<Result>()
                .HandleResult(r => !r.IsSuccess)
                .Handle<Exception>()
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});
```

## Definition of Done
- [ ] RetryBehavior fully implemented with Polly
- [ ] Transient error detection accurate and extensible
- [ ] Exponential backoff with jitter working correctly
- [ ] Circuit breaker preventing cascade failures
- [ ] All retry metrics properly collected
- [ ] Per-operation policies configurable
- [ ] All tests passing including chaos scenarios
- [ ] Performance overhead < 2ms (no retry case)
- [ ] Documentation includes configuration examples

## Technical Notes

### Performance Targets
- No-retry overhead: < 2ms
- Retry decision: < 0.5ms
- Jitter calculation: < 0.1ms
- Circuit breaker check: < 0.2ms

### Registration Order
```csharp
services.AddMediatR(cfg =>
{
    cfg.AddOpenBehavior(typeof(ObservabilityBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(RetryBehavior<,>));         // This story
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

### Jitter Algorithm (Decorrelated)
```csharp
// Decorrelated jitter for better distribution
private static TimeSpan CalculateDelay(int attempt, RetryPolicy policy)
{
    var random = Random.Shared;
    var baseDelay = policy.InitialDelay.TotalMilliseconds;
    var maxDelay = policy.MaxDelay.TotalMilliseconds;
    
    // Decorrelated jitter
    var delay = Math.Min(
        maxDelay,
        random.NextDouble() * Math.Pow(policy.BackoffMultiplier, attempt) * baseDelay
    );
    
    return TimeSpan.FromMilliseconds(delay);
}
```

## Dependencies
- Microsoft.Extensions.Resilience 8.0.0
- Polly.Core 8.4.0
- System.Diagnostics.Metrics

## References
- Polly documentation: https://www.pollydocs.org/
- Circuit Breaker pattern: https://docs.microsoft.com/azure/architecture/patterns/circuit-breaker
- Exponential backoff: https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/