# Resilience Guide

**Comprehensive guide to retry policies, circuit breakers, timeouts, and fault tolerance in Axon.**

---

## Overview

**Library**: Polly (resilience and transient-fault-handling)
**Patterns**: Retry with exponential backoff, Circuit Breaker, Timeout
**Scope**: Database connections, HTTP clients, external services

---

## Database Resilience

### EF Core Retry Policy

```csharp
// In ServiceRegistration.cs
services.AddDbContext<IdentityWriteDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");

        // Automatic retry on transient failures
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null); // null = use default transient error codes
    });
});
```

**Behavior**:
- Retries up to 5 times on transient database errors
- Exponential backoff up to 10 seconds between retries
- Automatically handles:
  - Connection failures
  - Timeout exceptions
  - Deadlocks
  - Transient network issues

**Default Transient Errors** (PostgreSQL):
- `NpgsqlException` with transient error codes
- `TimeoutException`
- `SocketException`
- Connection pool exhaustion

---

## HTTP Client Resilience

### Polly HTTP Policies

```csharp
// For external services (Dynamic.xyz, Helius, OpenAI)
services.AddHttpClient<IDynamicApiClient, DynamicApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["Dynamic:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Axon/1.0");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy())
.AddPolicyHandler(GetTimeoutPolicy());
```

### Retry Policy (Exponential Backoff)

```csharp
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx and 408
        .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning(
                    "Retry {RetryAttempt} after {Delay}ms due to {StatusCode}",
                    retryAttempt,
                    timespan.TotalMilliseconds,
                    outcome.Result?.StatusCode ?? HttpStatusCode.InternalServerError);
            });
}
```

**Retry Schedule**:
- Attempt 1: Immediate
- Attempt 2: Wait 2 seconds (2^1)
- Attempt 3: Wait 4 seconds (2^2)
- Attempt 4: Wait 8 seconds (2^3)

**Triggers**:
- HTTP 5xx (Server Error)
- HTTP 408 (Request Timeout)
- HTTP 429 (Too Many Requests)
- Network errors (connection reset, DNS failures)

---

### Circuit Breaker Policy

```csharp
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                var logger = GetLogger();
                logger.LogError(
                    "Circuit breaker opened for {Duration}s after {Failures} consecutive failures",
                    duration.TotalSeconds,
                    5);
            },
            onReset: () =>
            {
                var logger = GetLogger();
                logger.LogInformation("Circuit breaker reset - service recovered");
            });
}
```

**States**:
1. **Closed** (Normal): Requests flow through, failures counted
2. **Open** (Tripped): All requests immediately fail-fast for 30 seconds
3. **Half-Open** (Testing): After 30s, allow 1 test request
   - Success → Reset to Closed
   - Failure → Re-open for another 30s

**Behavior**:
- After **5 consecutive failures**, circuit opens
- Fails fast for **30 seconds** (no requests to downstream service)
- After 30s, allows 1 test request to check if service recovered

**Benefits**:
- Prevents cascading failures
- Reduces load on failing service (allows recovery)
- Fast failure (immediate response instead of timeout)

---

### Timeout Policy

```csharp
static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
{
    return Policy.TimeoutAsync<HttpResponseMessage>(
        TimeSpan.FromSeconds(10),
        onTimeoutAsync: (context, timespan, task) =>
        {
            var logger = context.GetLogger();
            logger?.LogWarning("Request timed out after {Timeout}s", timespan.TotalSeconds);
            return Task.CompletedTask;
        });
}
```

**Behavior**:
- Each HTTP request has maximum 10-second timeout
- Separate from `HttpClient.Timeout` (which is 30s)
- Allows per-request timeout control

---

## Complete HTTP Client Configuration

```csharp
services.AddHttpClient<IDynamicApiClient, DynamicApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["Dynamic:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30); // Global timeout
})
.AddPolicyHandler((services, request) =>
    Policy.WrapAsync(
        GetRetryPolicy(),          // Outer: Retry on transient errors
        GetCircuitBreakerPolicy(), // Middle: Circuit breaker
        GetTimeoutPolicy()         // Inner: Per-request timeout
    ));
```

**Policy Order** (Wrap from outer to inner):
1. **Retry** - Outer layer (retries entire request including circuit breaker)
2. **Circuit Breaker** - Middle layer (fails fast when open)
3. **Timeout** - Inner layer (enforces per-request deadline)

**Example Flow** (Circuit Open):
1. Request arrives
2. Retry policy wraps circuit breaker
3. Circuit breaker is OPEN → Fail-fast immediately
4. Retry policy catches `BrokenCircuitException`
5. Does NOT retry (circuit breaker exception not retryable)
6. Returns 503 Service Unavailable

**Example Flow** (Transient Error):
1. Request arrives
2. Timeout policy: 10s limit
3. Request fails with 503
4. Circuit breaker: Counts failure (1/5)
5. Retry policy: Waits 2s, retries
6. Second attempt succeeds → Return 200 OK

---

## Cancellation Token Propagation

```csharp
public sealed class ExchangeCredentialHandler : IRequestHandler<ExchangeCredentialCommand, Result<ExchangeOutcome, Error>>
{
    public async Task<Result<ExchangeOutcome, Error>> Handle(
        ExchangeCredentialCommand cmd,
        CancellationToken ct)
    {
        // Always propagate cancellation token to downstream calls
        var principal = await _repository.GetByIdAsync(id, ct);
        var result = await _apiClient.ValidateTokenAsync(token, ct);
        await _repository.UpdateAsync(principal, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return result;
    }
}
```

**Benefits**:
- Request cancellation propagates through entire call chain
- Database operations abort gracefully
- HTTP requests cancel in-flight
- Reduces wasted resources

**Timeout Scenarios**:
1. Client disconnects → CancellationToken triggers
2. Kestrel request timeout → CancellationToken triggers
3. Polly timeout policy → CancellationToken triggers

---

## Fallback Strategies

### Fallback Policy

```csharp
static IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy()
{
    return Policy<HttpResponseMessage>
        .Handle<Exception>()
        .FallbackAsync(
            fallbackValue: new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"error\": \"Service temporarily unavailable\"}")
            },
            onFallbackAsync: (outcome, context) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning("Fallback triggered due to: {Exception}", outcome.Exception?.Message);
                return Task.CompletedTask;
            });
}
```

### Cache-as-Fallback

```csharp
public async Task<Result<UserData, Error>> GetUserDataAsync(AxonUserId userId, CancellationToken ct)
{
    try
    {
        // Try live API call
        var response = await _apiClient.GetUserAsync(userId, ct);
        if (response.IsSuccess)
        {
            // Cache successful response
            await _cache.SetAsync($"user:{userId}", response.Value, TimeSpan.FromMinutes(5), ct);
            return response;
        }
    }
    catch (BrokenCircuitException)
    {
        // Circuit is open - try cache
        var cached = await _cache.GetAsync<UserData>($"user:{userId}", ct);
        if (cached != null)
        {
            _logger.LogInformation("Serving stale data from cache due to circuit breaker");
            return Result.Success<UserData, Error>(cached);
        }
    }

    return Error.External("Service unavailable", "SERVICE.UNAVAILABLE");
}
```

---

## Configuration

### appsettings.json

```json
{
  "Resilience": {
    "Database": {
      "MaxRetryCount": 5,
      "MaxRetryDelay": "00:00:10"
    },
    "HttpClient": {
      "RetryCount": 3,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDuration": "00:00:30",
      "TimeoutSeconds": 10
    }
  }
}
```

### Configuration Binding

```csharp
public sealed class ResilienceOptions
{
    public DatabaseOptions Database { get; set; } = new();
    public HttpClientOptions HttpClient { get; set; } = new();

    public sealed class DatabaseOptions
    {
        public int MaxRetryCount { get; set; } = 5;
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
    }

    public sealed class HttpClientOptions
    {
        public int RetryCount { get; set; } = 3;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);
        public int TimeoutSeconds { get; set; } = 10;
    }
}
```

---

## Best Practices

### ✅ DO
- Use exponential backoff for retries (avoid thundering herd)
- Propagate `CancellationToken` through entire call chain
- Log retry attempts with context (attempt number, delay, reason)
- Implement circuit breakers for external dependencies
- Use separate timeout policies per operation type
- Combine retry + circuit breaker + timeout (Polly wrap)
- Cache responses as fallback during outages
- Monitor circuit breaker state changes

### ❌ DON'T
- Retry on 4xx errors (client errors are not transient)
- Use infinite retries (always have max retry count)
- Retry without backoff (causes thundering herd)
- Ignore circuit breaker exceptions (handle gracefully)
- Set timeouts longer than HTTP client timeout
- Retry on `OperationCanceledException` (user cancelled)
- Use same retry policy for all services (tune per dependency)

---

## Monitoring Resilience

### Metrics

```csharp
// Track retry attempts
Instrumentation.Retries.Add(1,
    new KeyValuePair<string, object?>("service", "dynamic-api"),
    new KeyValuePair<string, object?>("attempt", retryAttempt));

// Track circuit breaker state
Instrumentation.CircuitBreakerState.Record(state switch
{
    CircuitState.Closed => 0,
    CircuitState.Open => 1,
    CircuitState.HalfOpen => 2
}, new KeyValuePair<string, object?>("service", "dynamic-api"));
```

### Logging

```csharp
// In retry policy
onRetry: (outcome, timespan, retryAttempt, context) =>
{
    _logger.LogWarning(
        "Retry {Attempt}/{MaxRetries} for {Service} after {Delay}ms. Reason: {StatusCode}",
        retryAttempt,
        maxRetries,
        serviceName,
        timespan.TotalMilliseconds,
        outcome.Result?.StatusCode);
};

// In circuit breaker
onBreak: (outcome, duration) =>
{
    _logger.LogError(
        "Circuit breaker OPENED for {Service}. Duration: {Duration}s. Consecutive failures: {Failures}",
        serviceName,
        duration.TotalSeconds,
        consecutiveFailures);
};
```

---

## Testing Resilience

```csharp
[Test]
public async Task HttpClient_TransientError_ShouldRetry()
{
    // Arrange
    var handler = new MockHttpMessageHandler();
    handler
        .When("*/api/test")
        .Respond(HttpStatusCode.InternalServerError) // First call fails
        .Then()
        .Respond(HttpStatusCode.OK); // Second call succeeds

    var client = new HttpClient(handler);
    var policy = GetRetryPolicy();

    // Act
    var response = await policy.ExecuteAsync(() => client.GetAsync("/api/test"));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    handler.GetMatchCount("/api/test").ShouldBe(2); // Retried once
}

[Test]
public async Task CircuitBreaker_ConsecutiveFailures_ShouldOpen()
{
    // Arrange
    var handler = new MockHttpMessageHandler();
    handler.When("*").Respond(HttpStatusCode.InternalServerError);

    var policy = GetCircuitBreakerPolicy();

    // Act - Trigger 5 failures
    for (int i = 0; i < 5; i++)
    {
        await policy.ExecuteAsync(() => handler.SendAsync(new HttpRequestMessage()));
    }

    // Assert - 6th call should fail immediately (circuit open)
    var ex = await Should.ThrowAsync<BrokenCircuitException>(async () =>
        await policy.ExecuteAsync(() => handler.SendAsync(new HttpRequestMessage())));
}
```

---

## Related Documentation

- [Polly Library Guide](../../../Libraries/Polly/IMPLEMENTATION_GUIDE.md) - Polly patterns
- [Testing Guide](../../testing/TESTING-GUIDE.md) - Testing resilience

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team