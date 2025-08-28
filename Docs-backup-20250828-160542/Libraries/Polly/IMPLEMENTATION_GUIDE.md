# Polly.Core 8.6.2 Implementation Guide for Axon Backend

## Overview

This guide covers the implementation of Polly.Core 8.6.2 in the Axon Backend project, focusing on .NET 10 compatibility, resilience patterns, Clean Architecture integration, and modern HTTP client resilience strategies.

## Research Summary

**Research Question**: Document Polly.Core 8.6.2 for .NET 10 compatibility and resilience implementation in Axon Backend's Clean Architecture + DDD + CQRS patterns.

**Primary Sources**:
- NuGet Gallery: [Polly.Core 8.6.2](https://www.nuget.org/packages/polly.core/)
- Official Documentation: [pollydocs.org](https://www.pollydocs.org/)
- Microsoft Learn: [HTTP Resilience Patterns](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- .NET Blog: [Building Resilient Cloud Services](https://devblogs.microsoft.com/dotnet/building-resilient-cloud-services-with-dotnet-8/)
- GitHub Repository: [App-vNext/Polly](https://github.com/App-vNext/Polly)

**Confidence Level**: HIGH - Primary sources confirm .NET 10 compatibility and modern resilience patterns with significant performance improvements

## Version 8.6.2 Key Features

### Major Improvements in v8.x
1. **Zero-Allocation Performance**: Designed from the ground up for zero-allocations and enhanced performance
2. **Built-in Telemetry**: Native observability and metrics integration
3. **Resilience Pipelines**: Unified execution model replacing legacy policies
4. **Fluent Builder Pattern**: Simplified configuration with explicit strategy ordering
5. **Enhanced HttpClient Integration**: Via Microsoft.Extensions.Http.Resilience

### Performance Benchmarks
- **Memory Usage**: ~4x reduction (3,816 B → 1,008 B)
- **Execution Speed**: Marginally faster execution times
- **Zero Allocations**: Optimized for high-throughput scenarios

## Target Framework Compatibility

### Supported Frameworks
```xml
<!-- Polly.Core 8.6.2 targets -->
<TargetFrameworks>net6.0;netstandard2.0;net462</TargetFrameworks>
```

### .NET 10 Compatibility
- **Status**: ✅ FULLY COMPATIBLE
- **Evidence**: Targets .NET 6.0+ framework range, supports .NET 10 preview features
- **Preview Support**: Compatible with `#:package` directive in .NET 10 preview 4+

## Installation & Configuration

### Core Packages
```xml
<!-- Core resilience library -->
<PackageReference Include="Polly.Core" Version="8.6.2" />

<!-- Microsoft resilience extensions (recommended for HTTP) -->
<PackageReference Include="Microsoft.Extensions.Resilience" Version="9.0.7" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.0.7" />

<!-- Dependency injection support -->
<PackageReference Include="Polly.Extensions" Version="8.6.2" />
```

### Dependency Injection Setup
```csharp
// Program.cs - Register resilience pipelines
builder.Services.AddResiliencePipeline("external-api", pipelineBuilder =>
{
    pipelineBuilder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15)
        })
        .AddTimeout(TimeSpan.FromSeconds(10));
});

// HTTP Client with standard resilience
builder.Services.AddHttpClient("external-service")
    .AddStandardResilienceHandler();

// Custom HTTP resilience configuration
builder.Services.AddHttpClient("custom-service")
    .AddResilienceHandler("custom-pipeline", resilienceBuilder =>
    {
        resilienceBuilder
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(response => !response.IsSuccessStatusCode)
            })
            .AddTimeout(TimeSpan.FromSeconds(30));
    });
```

## Resilience Patterns

### 1. Retry Strategy
```csharp
public class RetryStrategyOptions
{
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
    public DelayBackoffType BackoffType { get; set; } = DelayBackoffType.Exponential;
    public bool UseJitter { get; set; } = true;
    public PredicateBuilder<T> ShouldHandle { get; set; }
}
```

### 2. Circuit Breaker Pattern
```csharp
public class CircuitBreakerStrategyOptions
{
    public double FailureRatio { get; set; } = 0.5;
    public int MinimumThroughput { get; set; } = 10;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}
```

### 3. Timeout Strategy
```csharp
// Simple timeout
builder.AddTimeout(TimeSpan.FromSeconds(10));

// Advanced timeout with options
builder.AddTimeout(new TimeoutStrategyOptions
{
    Timeout = TimeSpan.FromSeconds(10),
    OnTimeout = args =>
    {
        // Custom timeout handling
        return ValueTask.CompletedTask;
    }
});
```

### 4. Rate Limiting
```csharp
builder.AddRateLimiter(new RateLimiterStrategyOptions
{
    RateLimiter = args => RateLimitLease.Acquired
});
```

### 5. Hedging Strategy (New in v8)
```csharp
builder.AddHedging(new HedgingStrategyOptions<HttpResponseMessage>
{
    MaxHedgedAttempts = 2,
    Delay = TimeSpan.FromMilliseconds(500),
    ActionGenerator = args => () => httpClient.GetAsync(args.PrimaryContext.Properties["url"])
});
```

## Clean Architecture Integration

### Infrastructure Layer Implementation

#### 1. External Service Adapter
```csharp
// src/Modules/{Module}/Infrastructure/ExternalServices/
public sealed class ResilientExternalApiClient : IExternalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<ResilientExternalApiClient> _logger;

    public ResilientExternalApiClient(
        HttpClient httpClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ResilientExternalApiClient> logger)
    {
        _httpClient = httpClient;
        _pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>("external-api");
        _logger = logger;
    }

    public async Task<Result<ApiResponse>> GetDataAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            var response = await _pipeline.ExecuteAsync(
                async cancellationToken => await _httpClient.GetAsync(endpoint, cancellationToken),
                ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<ApiResponse>(content);
                return Result.Success(data);
            }

            return Result.Failure(Error.External($"API call failed: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call external API endpoint: {Endpoint}", endpoint);
            return Result.Failure(Error.External($"External service unavailable: {ex.Message}"));
        }
    }
}
```

#### 2. Repository with Database Resilience
```csharp
// src/Modules/{Module}/Infrastructure/Persistence/
public sealed class ResilientRepository<T> : IRepository<T> where T : AggregateRoot
{
    private readonly DbContext _context;
    private readonly ResiliencePipeline _pipeline;

    public ResilientRepository(
        DbContext context,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _context = context;
        _pipeline = pipelineProvider.GetPipeline("database-operations");
    }

    public async Task<Result<T?>> GetByIdAsync(EntityId id, CancellationToken ct)
    {
        try
        {
            var entity = await _pipeline.ExecuteAsync(
                async _ => await _context.Set<T>().FindAsync(id.Value, ct),
                ct);

            return entity is null 
                ? Result.Success<T?>(null)
                : Result.Success<T?>(entity);
        }
        catch (Exception ex)
        {
            return Result.Failure<T?>(Error.Database($"Failed to retrieve entity: {ex.Message}"));
        }
    }
}
```

### Application Layer Integration

#### Command Handler with Resilience
```csharp
// src/Modules/{Module}/Application/Commands/
public sealed class ProcessExternalDataHandler : IRequestHandler<ProcessExternalDataCommand, Result<string>>
{
    private readonly IExternalApiClient _apiClient;
    private readonly ResiliencePipeline<Result<string>> _pipeline;

    public ProcessExternalDataHandler(
        IExternalApiClient apiClient,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _apiClient = apiClient;
        _pipeline = pipelineProvider.GetPipeline<Result<string>>("command-processing");
    }

    public async Task<Result<string>> Handle(ProcessExternalDataCommand request, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async _ =>
        {
            var apiResult = await _apiClient.GetDataAsync(request.Endpoint, ct);
            
            if (apiResult.IsFailure)
                return Result.Failure<string>(apiResult.Error);

            // Process the data
            var processedData = ProcessApiResponse(apiResult.Value);
            
            return Result.Success(processedData);
        }, ct);
    }

    private string ProcessApiResponse(ApiResponse response)
    {
        // Business logic implementation
        return response.Data.ToString();
    }
}
```

## Configuration Patterns

### 1. Options-Based Configuration
```csharp
// appsettings.json
{
  "Resilience": {
    "ExternalApi": {
      "Retry": {
        "MaxRetryAttempts": 3,
        "DelaySeconds": 1,
        "BackoffType": "Exponential",
        "UseJitter": true
      },
      "CircuitBreaker": {
        "FailureRatio": 0.5,
        "MinimumThroughput": 10,
        "SamplingDurationSeconds": 30,
        "BreakDurationSeconds": 15
      },
      "TimeoutSeconds": 10
    }
  }
}

// Configuration binding
public class ResilienceOptions
{
    public const string SectionName = "Resilience";
    
    public ExternalApiOptions ExternalApi { get; set; } = new();
}

public class ExternalApiOptions
{
    public RetryOptions Retry { get; set; } = new();
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
}
```

### 2. Dynamic Configuration
```csharp
builder.Services.AddResiliencePipeline("configurable-pipeline", (pipelineBuilder, context) =>
{
    var options = context.ServiceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>();
    var config = options.CurrentValue.ExternalApi;
    
    pipelineBuilder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = config.Retry.MaxRetryAttempts,
            Delay = TimeSpan.FromSeconds(config.Retry.DelaySeconds),
            BackoffType = config.Retry.BackoffType,
            UseJitter = config.Retry.UseJitter
        })
        .AddTimeout(TimeSpan.FromSeconds(config.TimeoutSeconds));
});
```

## Telemetry and Observability

### 1. Built-in Metrics
Polly v8 automatically provides metrics for:
- Retry attempts and outcomes
- Circuit breaker state changes
- Timeout occurrences
- Pipeline execution duration

### 2. Custom Telemetry
```csharp
builder.Services.AddResiliencePipeline("telemetry-enabled", pipelineBuilder =>
{
    pipelineBuilder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            OnRetry = args =>
            {
                // Custom retry telemetry
                var logger = args.Context.ServiceProvider?.GetService<ILogger<Program>>();
                logger?.LogWarning("Retry attempt {Attempt} for operation {Operation}", 
                    args.AttemptNumber, args.Context.OperationKey);
                return ValueTask.CompletedTask;
            }
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            OnOpened = args =>
            {
                // Circuit breaker opened telemetry
                var logger = args.Context.ServiceProvider?.GetService<ILogger<Program>>();
                logger?.LogError("Circuit breaker opened for operation {Operation}", 
                    args.Context.OperationKey);
                return ValueTask.CompletedTask;
            }
        });
});
```

### 3. Integration with Application Insights
```csharp
// Add Application Insights integration
builder.Services.AddApplicationInsightsTelemetry();

// Custom telemetry processor for Polly events
public class PollyTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;

    public PollyTelemetryProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        if (item is TraceTelemetry trace && trace.Message.Contains("Polly"))
        {
            trace.Properties["Component"] = "Resilience";
        }
        
        _next.Process(item);
    }
}
```

## HTTP Client Integration Patterns

### 1. Standard Resilience Handler
```csharp
// Simple standard configuration
services.AddHttpClient("standard-client")
    .AddStandardResilienceHandler();
```

The standard resilience handler provides:
- Rate limiter (concurrent request limiting)
- Total request timeout (including retry attempts)
- Retry strategy (for transient failures)
- Circuit breaker (failure prevention)
- Attempt timeout (individual request timeout)

### 2. Standard Hedging Handler
```csharp
// Hedging for improved latency
services.AddHttpClient("hedging-client")
    .AddStandardHedgingHandler();
```

### 3. Named Resilience Handlers
```csharp
services.AddHttpClient("named-client")
    .AddResilienceHandler("custom-http-pipeline", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Linear,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(response => 
                    response.StatusCode >= HttpStatusCode.InternalServerError)
        });
    });
```

## Advanced Pipeline Composition

### 1. Strategy Ordering
Pipeline strategies execute in the order they are added (first-in, outermost):

```csharp
// Execution order: RateLimit → Retry → CircuitBreaker → Timeout → HttpCall
builder.Services.AddResiliencePipeline("ordered-pipeline", pipelineBuilder =>
{
    pipelineBuilder
        .AddRateLimiter(rateLimiterOptions)    // 1st (outermost)
        .AddRetry(retryOptions)                // 2nd
        .AddCircuitBreaker(circuitBreakerOptions) // 3rd  
        .AddTimeout(timeoutOptions);           // 4th (innermost)
});
```

### 2. Conditional Strategy Application
```csharp
builder.Services.AddResiliencePipeline("conditional-pipeline", (pipelineBuilder, context) =>
{
    var environment = context.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    
    // Always add retry
    pipelineBuilder.AddRetry(retryOptions);
    
    // Add circuit breaker only in production
    if (environment.IsProduction())
    {
        pipelineBuilder.AddCircuitBreaker(circuitBreakerOptions);
    }
    
    // Add timeout
    pipelineBuilder.AddTimeout(timeoutOptions);
});
```

### 3. Multiple Named Pipelines
```csharp
// Critical operations pipeline
builder.Services.AddResiliencePipeline("critical-operations", pipelineBuilder =>
{
    pipelineBuilder
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 5 })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions { FailureRatio = 0.3 })
        .AddTimeout(TimeSpan.FromSeconds(30));
});

// Non-critical operations pipeline  
builder.Services.AddResiliencePipeline("non-critical-operations", pipelineBuilder =>
{
    pipelineBuilder
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2 })
        .AddTimeout(TimeSpan.FromSeconds(10));
});
```

## Testing Strategies

### 1. Unit Testing with Polly
```csharp
[Test]
public async Task Handler_Should_Retry_On_Transient_Failure()
{
    // Arrange
    var mockHttpClient = new Mock<HttpClient>();
    var retryStrategy = new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = 3,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
        })
        .Build();

    // Mock transient failure then success
    mockHttpClient.SetupSequence(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new HttpRequestException("Transient failure"))
        .ThrowsAsync(new HttpRequestException("Transient failure"))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

    // Act & Assert
    var result = await retryStrategy.ExecuteAsync(async _ => 
        await mockHttpClient.Object.GetAsync("/test"));
    
    result.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### 2. Integration Testing
```csharp
[Test]
public async Task ExternalApiClient_Should_Handle_Circuit_Breaker()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddResiliencePipeline("test-pipeline", pipelineBuilder =>
                {
                    pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        MinimumThroughput = 2,
                        SamplingDuration = TimeSpan.FromSeconds(10),
                        BreakDuration = TimeSpan.FromSeconds(5)
                    });
                });
            });
        });

    var client = factory.CreateClient();
    
    // Act - Trigger circuit breaker
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => client.GetAsync("/failing-endpoint"))
        .ToArray();
        
    var responses = await Task.WhenAll(tasks);
    
    // Assert - Some requests should be circuit broken
    responses.Should().Contain(r => r.StatusCode == HttpStatusCode.ServiceUnavailable);
}
```

## Migration from Legacy Polly

### From Microsoft.Extensions.Http.Polly
```csharp
// OLD: Microsoft.Extensions.Http.Polly (v7 and earlier)
services.AddHttpClient("legacy-client")
    .AddPolicyHandler(Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// NEW: Microsoft.Extensions.Http.Resilience (v8+)
services.AddHttpClient("modern-client")
    .AddResilienceHandler("retry-pipeline", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential
        });
    });
```

### Key Migration Points
1. **Policy → Strategy**: Replace `Policy` with resilience strategies
2. **PolicyWrap → Pipeline**: Use `ResiliencePipelineBuilder` for composition
3. **Sync/Async Separation**: No longer needed, unified execution model
4. **Configuration**: Move to options-based configuration pattern

## Performance Considerations

### 1. Pipeline Caching
```csharp
// Efficient: Cache pipelines at startup
services.AddSingleton<ResiliencePipelineProvider<string>>();

// Avoid: Creating pipelines per request
// DON'T DO THIS
services.AddScoped(provider => 
{
    return new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddRetry(retryOptions)
        .Build(); // Creates new pipeline each time
});
```

### 2. Memory Optimization
```csharp
// Use generic pipelines when possible
services.AddResiliencePipeline("generic-pipeline", builder =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        // Don't capture large objects in delegates
        OnRetry = args => ValueTask.CompletedTask
    });
});
```

### 3. Cancellation Token Propagation
```csharp
public async Task<Result<T>> ExecuteWithResilienceAsync<T>(
    Func<CancellationToken, Task<T>> operation,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await _pipeline.ExecuteAsync(operation, cancellationToken);
        return Result.Success(result);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Result.Failure<T>(Error.Cancelled("Operation was cancelled"));
    }
    catch (Exception ex)
    {
        return Result.Failure<T>(Error.Unexpected(ex.Message));
    }
}
```

## Error Handling Patterns

### 1. Result Pattern Integration
```csharp
public static class ResilienceExtensions
{
    public static async Task<Result<T>> ExecuteAsync<T>(
        this ResiliencePipeline<Result<T>> pipeline,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await pipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(Error.Unexpected($"Resilience pipeline failed: {ex.Message}"));
        }
    }
}
```

### 2. Domain Error Mapping
```csharp
public sealed class DomainAwareRetryStrategy : RetryStrategyOptions<Result<T>>
{
    public DomainAwareRetryStrategy()
    {
        ShouldHandle = new PredicateBuilder<Result<T>>()
            .HandleResult(result => result.IsFailure && result.Error.IsTransient)
            .Handle<TimeoutException>()
            .Handle<HttpRequestException>();
            
        MaxRetryAttempts = 3;
        BackoffType = DelayBackoffType.Exponential;
        UseJitter = true;
    }
}
```

## Best Practices Summary

### 1. Architecture Integration
- ✅ Use in Infrastructure layer for external service calls
- ✅ Keep resilience logic separate from business logic
- ✅ Integrate with dependency injection container
- ✅ Use options pattern for configuration

### 2. Performance Optimization
- ✅ Cache resilience pipelines at application startup
- ✅ Use appropriate generic types for pipeline reuse
- ✅ Properly propagate cancellation tokens
- ✅ Minimize allocations in strategy delegates

### 3. Observability
- ✅ Enable built-in telemetry and metrics
- ✅ Add custom logging for business-critical operations
- ✅ Monitor circuit breaker state changes
- ✅ Track retry patterns and success rates

### 4. Testing
- ✅ Unit test resilience strategies in isolation
- ✅ Integration test full pipeline behavior
- ✅ Test failure scenarios and recovery paths
- ✅ Validate timeout and cancellation handling

### 5. Configuration Management
- ✅ Use environment-specific resilience settings
- ✅ Support dynamic configuration updates
- ✅ Document strategy selection rationale
- ✅ Monitor and adjust based on telemetry

## Apply vs Not-Apply Recommendations

### ✅ APPLY - Recommended for Axon Backend

1. **Core Integration**: Implement Polly.Core 8.6.2 in Infrastructure layer adapters
2. **HTTP Resilience**: Use Microsoft.Extensions.Http.Resilience for external API calls
3. **Pipeline Composition**: Combine retry, circuit breaker, and timeout strategies
4. **Clean Architecture**: Keep resilience concerns in Infrastructure, expose via ports
5. **Telemetry Integration**: Enable built-in metrics and add custom logging
6. **Options Configuration**: Use appsettings.json for environment-specific tuning

### ❌ NOT-APPLY - Not Recommended

1. **Legacy Microsoft.Extensions.Http.Polly**: Skip v7 packages, use v8+ resilience extensions
2. **Domain Layer Resilience**: Don't add resilience logic to domain entities or services
3. **Synchronous Policies**: Avoid sync-specific configurations, use unified model
4. **Manual Pipeline Creation**: Don't create pipelines per request, use DI registration

## Summary

Polly.Core 8.6.2 provides excellent .NET 10 compatibility with significant performance improvements and modern resilience patterns. The integration with Microsoft.Extensions.Http.Resilience offers a streamlined approach for HTTP client resilience that aligns well with Axon Backend's Clean Architecture principles. The zero-allocation design and built-in telemetry make it suitable for high-performance cloud services while maintaining separation of concerns through proper Infrastructure layer integration.