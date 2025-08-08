# Story 06: LoggingBehavior - Production-Ready Structured Logging

## Story Overview
**Story ID**: Epic_05_Story_06  
**Story Name**: LoggingBehavior - Production-Ready Structured Logging  
**Estimated Duration**: 1 day  
**Dependencies**: Epic_04 (CQRS Foundation), Structured logging infrastructure

## User Story
**As a developer**, I want comprehensive structured logging so that I can debug issues, audit operations, and analyze usage patterns with complete data protection.

## Acceptance Criteria
- [ ] LoggingBehavior class created with rich structured logging
- [ ] Correlation ID propagation and structured log context management
- [ ] Intelligent request/response logging with automatic PII detection and masking
- [ ] Per-operation configurable log levels with environment-based overrides
- [ ] Detailed performance timing logs with operation categorization
- [ ] Distributed log correlation across services and external dependencies
- [ ] Comprehensive sensitive data masking with configurable rules
- [ ] Logging validation tests and log data quality verification

## Technical Implementation

### Core Components

#### 1. LoggingBehavior Class
```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ISensitiveDataMasker _dataMasker;
    private readonly ILogLevelResolver _logLevelResolver;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogContextEnricher _contextEnricher;
}
```

#### 2. Sensitive Data Masking
```csharp
public interface ISensitiveDataMasker
{
    object MaskSensitiveData(object data);
    bool ContainsSensitiveData(PropertyInfo property);
}

[AttributeUsage(AttributeTargets.Property)]
public class SensitiveDataAttribute : Attribute
{
    public MaskingStrategy Strategy { get; init; }
}
```

### Tasks

#### Task 1: Create LoggingBehavior Infrastructure
- [ ] Create LoggingBehavior<TRequest, TResponse> class
- [ ] Implement IPipelineBehavior interface
- [ ] Set up structured logging context
- [ ] Configure log enrichment pipeline

#### Task 2: Correlation ID Management
- [ ] Extract correlation ID from context
- [ ] Add to all log entries as property
- [ ] Create nested scopes with correlation
- [ ] Ensure propagation through async flows
- [ ] Support multiple correlation ID formats

#### Task 3: Request Logging with PII Protection
- [ ] Log request type and key properties
- [ ] Detect sensitive data attributes
- [ ] Apply masking strategies (partial, full, hash)
- [ ] Handle nested object masking
- [ ] Configure maskable property patterns

#### Task 4: Response Logging
- [ ] Log success/failure status
- [ ] Include business error details
- [ ] Mask sensitive response data
- [ ] Log response size metrics
- [ ] Handle large response truncation

#### Task 5: Performance Timing
- [ ] Measure total execution time
- [ ] Log slow operations (configurable threshold)
- [ ] Include timing breakdown if available
- [ ] Track database query counts
- [ ] Memory allocation tracking

#### Task 6: Configurable Log Levels
- [ ] Per-request-type log level configuration
- [ ] Environment-based overrides (Dev/Staging/Prod)
- [ ] Dynamic log level adjustment
- [ ] Verbose mode for debugging
- [ ] Silent mode for sensitive operations

#### Task 7: Log Context Enrichment
- [ ] Add user identity information
- [ ] Include tenant/organization context
- [ ] Add request source (IP, user agent)
- [ ] Include deployment information
- [ ] Custom domain context

#### Task 8: Distributed Tracing Support
- [ ] Include trace ID in logs
- [ ] Add span ID for correlation
- [ ] Support W3C trace context
- [ ] External service call correlation
- [ ] Async operation tracking

#### Task 9: Testing and Validation
- [ ] Unit tests for masking logic
- [ ] Log output format validation
- [ ] Performance overhead tests
- [ ] PII leak detection tests
- [ ] Log level configuration tests

## Definition of Done
- [ ] LoggingBehavior fully implemented with structured logging
- [ ] PII detection and masking working correctly
- [ ] Correlation IDs present in all log entries
- [ ] Configurable log levels operational
- [ ] Performance timing accurately logged
- [ ] All sensitive data properly masked
- [ ] Tests validate log quality and security
- [ ] Documentation includes log query examples

## Technical Notes

### Structured Log Example
```json
{
  "timestamp": "2024-01-15T10:30:45Z",
  "level": "Information",
  "correlationId": "abc-123-def",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId": "00f067aa0ba902b7",
  "userId": "user-456",
  "tenantId": "tenant-789",
  "requestType": "CreateUserCommand",
  "requestId": "req-xyz",
  "duration": 145,
  "outcome": "success",
  "message": "Executed CreateUserCommand in 145ms",
  "properties": {
    "email": "u***@example.com",
    "name": "John Doe"
  }
}
```

### Masking Strategies
```csharp
public enum MaskingStrategy
{
    Full,        // ********
    Partial,     // Jo** D**
    Email,       // u***@example.com
    Phone,       // ***-***-1234
    CreditCard,  // ****-****-****-1234
    Hash         // SHA256 hash for correlation
}
```

### Log Level Configuration
```csharp
services.Configure<LoggingOptions>(options =>
{
    options.DefaultLevel = LogLevel.Information;
    options.RequestLevels[typeof(GetUserQuery)] = LogLevel.Debug;
    options.SlowOperationThreshold = TimeSpan.FromSeconds(1);
    options.Environment = "Production";
});
```

### Performance Logging
```csharp
if (duration > _slowOperationThreshold)
{
    _logger.LogWarning(
        "Slow operation detected: {RequestType} took {Duration}ms",
        requestType, duration.TotalMilliseconds);
}
```

### PII Detection Rules
```csharp
private static readonly string[] SensitivePatterns = 
{
    "password", "pwd", "secret", "token", "key",
    "ssn", "social", "credit", "card", "cvv",
    "email", "phone", "address", "dob", "birth"
};
```

### Performance Targets
- Logging overhead: < 1ms
- Masking operation: < 0.5ms
- Context enrichment: < 0.2ms
- Log serialization: < 1ms

### Registration Order
LoggingBehavior should be registered:
- After ObservabilityBehavior (to use correlation ID)
- Before other business logic behaviors
- Close to the top of the pipeline

## Dependencies
- Microsoft.Extensions.Logging
- Serilog (optional, for structured logging)
- System.Text.Json (for serialization)