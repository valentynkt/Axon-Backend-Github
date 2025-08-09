# Resilience Module

This module provides comprehensive retry and resilience patterns for the Axon Backend using Clean Architecture principles and the Result pattern.

## Architecture Overview

The module follows a layered approach with clear separation of concerns:

### Core Interfaces
- `IRetryPolicy` - Defines retry execution with Result pattern support
- `IRetryPolicyResolver` - Resolves retry policies by request type
- `ITransientFaultDetector` - Classifies exceptions as transient
- `IRetryableRequest` - Marker interface for retryable requests

### Implementations
- `PollyRetryPolicy` - Polly-based retry implementation with exponential backoff
- `RetryPolicyResolver` - Configurable policy resolution
- `TransientFaultDetector` - Comprehensive fault classification
- `TransientFaultClassifier` - Static utility for fault classification

### Configuration
- `RetryPolicy` - Immutable configuration record with predefined policies
- `RetryOptions` - Global retry configuration

## Key Features

### 1. Result Pattern Integration
All retry operations return `Result<T>` for functional error handling:

```csharp
var result = await retryPolicy.ExecuteAsync(
    async () => await SomeOperationReturningResult(), 
    maxRetries: 3, 
    retryDelay: TimeSpan.FromMilliseconds(100)
);
```

### 2. Exception Handling Support
For operations that may throw exceptions:

```csharp
var result = await retryPolicy.ExecuteWithExceptionHandlingAsync(
    async () => await SomeOperationThatMayThrow(), 
    maxRetries: 3, 
    retryDelay: TimeSpan.FromMilliseconds(100)
);
```

### 3. Comprehensive Transient Fault Detection
Supports classification of:
- SQL Server transient errors (deadlocks, timeouts, resource limits)
- HTTP transient errors (5xx, 429, timeouts)
- Socket errors (connection issues, network unreachable)
- Task cancellation (properly handled, not retried)

### 4. Configurable Policies
Predefined policies for common scenarios:

```csharp
// For commands - conservative with circuit breaker
var commandPolicy = RetryPolicy.ForCommands;

// For queries - more aggressive retries
var queryPolicy = RetryPolicy.ForQueries;

// Custom policy
var customPolicy = new RetryPolicy
{
    MaxAttempts = 5,
    InitialDelay = TimeSpan.FromMilliseconds(200),
    MaxDelay = TimeSpan.FromSeconds(30),
    UseCircuitBreaker = true,
    CircuitBreakerThreshold = 3,
    UseJitter = true
};
```

## Dependency Injection Setup

```csharp
services.Configure<RetryOptions>(configuration.GetSection("Retry"));
services.AddSingleton<ITransientFaultDetector, TransientFaultDetector>();
services.AddSingleton<IRetryPolicyResolver, RetryPolicyResolver>();
services.AddScoped<IRetryPolicy, PollyRetryPolicy>();
```

## Usage Examples

### Basic Retry with Result Pattern
```csharp
public class PaymentService
{
    private readonly IRetryPolicy _retryPolicy;
    
    public async Task<Result<PaymentResult>> ProcessPaymentAsync(PaymentRequest request)
    {
        return await _retryPolicy.ExecuteAsync(
            () => ProcessPaymentInternalAsync(request),
            maxRetries: 3,
            retryDelay: TimeSpan.FromMilliseconds(500)
        );
    }
}
```

### Extension Method Usage
```csharp
public class DataService
{
    private readonly ILogger<DataService> _logger;
    
    public async Task<string> GetDataAsync()
    {
        return await this.RetryOnFailureAsync(
            () => HttpClient.GetStringAsync("https://api.example.com/data"),
            _logger,
            retryCount: 3
        );
    }
}
```

## Best Practices

1. **Use Result Pattern**: Prefer `ExecuteAsync` over `ExecuteWithExceptionHandlingAsync` when possible
2. **Conservative Retries**: Use fewer retries for commands, more for queries
3. **Circuit Breakers**: Enable for external service calls and commands
4. **Jitter**: Always enable jitter to prevent thundering herd problems
5. **Logging**: All retry attempts are automatically logged with context
6. **Cancellation**: Always pass cancellation tokens for proper shutdown

## Configuration

### appsettings.json
```json
{
  "Retry": {
    "EnableQueryRetry": false,
    "DefaultMaxAttempts": 3,
    "DefaultInitialDelay": "00:00:00.100",
    "DefaultMaxDelay": "00:00:30"
  }
}
```

## Thread Safety

All implementations are thread-safe and can be used as singletons or in concurrent scenarios.

## Performance Considerations

- Uses exponential backoff with jitter to reduce load
- Circuit breakers prevent cascading failures
- Efficient static classification for common error types
- Minimal allocations in retry logic