# Epic 05: MediatR Pipeline Behaviors

## Epic Overview

**Epic ID**: Epic_05  
**Epic Name**: MediatR Pipeline Behaviors  
**Epic Priority**: Critical  
**Estimated Duration**: 5-6 days  
**Dependencies**: Epic_04 (CQRS Foundation)

## Business Value

Implements production-ready cross-cutting concerns through MediatR pipeline behaviors, providing validation, transaction management, caching, structured logging, resilience, and comprehensive observability across all commands and queries. This eliminates code duplication while ensuring consistent, reliable behavior across the entire application.

## Acceptance Criteria

- [ ] All 6 pipeline behaviors implemented with production-quality error handling
- [ ] Behaviors registered in correct execution order with detailed documentation
- [ ] Result<T> pattern fully integrated with proper error aggregation
- [ ] Comprehensive telemetry and metrics collection operational
- [ ] Database transaction boundaries properly managed with outbox pattern
- [ ] Intelligent query caching with configurable invalidation
- [ ] FluentValidation integration with grouped error reporting
- [ ] Resilience behaviors with configurable retry policies
- [ ] Zero performance regression validated through benchmarks

## Technical Scope

### Core Pipeline Behaviors
1. **ObservabilityBehavior** - Activity tracing, metrics, and telemetry (outermost)
2. **LoggingBehavior** - Structured logging with correlation IDs
3. **RetryBehavior** - Transient failure recovery with exponential backoff
4. **ValidationBehavior** - Request validation with error aggregation
5. **CachingBehavior** - Query result caching with intelligent key generation
6. **TransactionBehavior** - Database transaction management with outbox pattern (innermost)

### Infrastructure Components
- Critical behavior registration order management
- Configuration system for behavior policies
- Telemetry and metrics collection
- Robust error aggregation and reporting

## Implementation Status Review (2025-01-08)

### ✅ CURRENT IMPLEMENTATION STATUS

**All 6 core pipeline behaviors are IMPLEMENTED and functional:**

1. **ValidationBehavior** ✅ - `src/BuildingBlocks/Application/Behaviors/ValidationBehavior.cs` 
2. **TransactionBehavior** ✅ - `src/BuildingBlocks/Application/Behaviors/TransactionBehavior.cs`
3. **ObservabilityBehavior** ✅ - `src/BuildingBlocks/Application/Behaviors/ObservabilityPipelineBehavior.cs`
4. **LoggingBehavior** ✅ - `src/BuildingBlocks/Application/Behaviors/ResultLoggingBehavior.cs` 
5. **RetryBehavior** ✅ - `src/BuildingBlocks/Infrastructure/Resilience/RetryBehavior.cs`
6. **CachingBehavior** ✅ - `src/BuildingBlocks/Infrastructure/Caching/CachingBehavior.cs`

### ❌ CRITICAL ISSUES TO ADDRESS

**🚨 Priority 1 - Behavior Registration Order**
- **Issue**: Current registration order violates Epic 5 specification
- **Current**: ValidationBehavior → TransactionBehavior → CachingBehavior → LoggingBehavior  
- **Required**: ObservabilityBehavior → LoggingBehavior → RetryBehavior → ValidationBehavior → CachingBehavior → TransactionBehavior
- **Risk**: Missing telemetry, incorrect error handling, performance issues

**🚨 Priority 2 - Missing Central Registration**
- **Issue**: Each module registers behaviors independently
- **Required**: Centralized registration in BuildingBlocks with proper ordering
- **Files**: Need to create `PipelineBehaviorExtensions.cs` in BuildingBlocks

**🔧 Priority 3 - File Organization**
- **Issue**: Behaviors scattered across Application/ and Infrastructure/ folders
- **Recommendation**: Consolidate all behaviors to `src/BuildingBlocks/Application/Behaviors/`

## User Stories

### Story 1: ValidationBehavior - Enhanced Error Aggregation ✅ **IMPLEMENTED**
**As a developer**, I want robust automatic validation so that invalid requests are rejected early with comprehensive, well-structured error messages.

**Implementation Status:**
- ✅ `ValidationBehavior<TRequest, TResponse>` class with enhanced error handling
- ✅ Integrate with FluentValidation framework using `IValidator<T>` collection
- ✅ Implement property-grouped error aggregation for better client experience  
- ✅ Support multiple validators per request type with proper error merging
- ✅ Add comprehensive validation metrics and telemetry
- ✅ Configure selective validation (commands always, queries when marked)
- ⚠️ Create exhaustive unit tests covering all validation scenarios **[NEEDS REVIEW]**
- ⚠️ Add integration tests with complex validation rules **[MISSING]**

**Critical Implementation Note:**
The validation error aggregation must group failures by property name to provide clean, structured error responses:

```csharp
// Enhanced error aggregation in ValidationBehavior.cs
var errors = failures
    .GroupBy(e => e.PropertyName) // Group errors by the property they belong to.
    .Select(g => Error.Validation(
        // Use the property name as the error code for easy parsing on the client.
        g.Key,
        // Join all error messages for that single property.
        string.Join("; ", g.Select(e => e.ErrorMessage))))
    .ToArray();

return Result<TResponse>.Failure(Error.Aggregate(errors));
```

**Acceptance Criteria:**
- ✅ Commands with validation errors return properly structured Result<T>.Failure
- ✅ Multiple validation errors are grouped by property and aggregated cleanly
- ✅ Validation runs before any resource-intensive operations
- ✅ Validation performance metrics are captured and monitored

### Story 2: TransactionBehavior - Enhanced Resilience ✅ **IMPLEMENTED**
**As a developer**, I want bulletproof transaction management so that command operations are atomic with reliable outbox pattern integration.

**Implementation Details:**
- ✅ `TransactionBehavior<TRequest, TResponse>` class with enhanced error handling
- ✅ Deep integration with UnitOfWork and DbContext transaction management
- ✅ Robust nested transaction detection and handling implemented
- ✅ **EXCEPTIONAL**: Advanced outbox pattern integration with correlation IDs and metadata context
- ✅ Configurable transaction timeout with comprehensive monitoring
- ✅ All transaction rollback scenarios handled gracefully  
- ✅ **EXCEEDS SPEC**: Comprehensive transaction metrics, structured logging, and telemetry
- ✅ **ADVANCED**: Configurable isolation levels based on request metadata
- ⚠️ **Transaction failure mode tests need comprehensive review**

**Critical Implementation Note:**
The outbox processor trigger must include proper error handling to prevent silent failures:

```csharp
// Enhanced outbox processing in TransactionBehavior.cs, after await transaction.CommitAsync();

// Asynchronously trigger the outbox processor. This is a fire-and-forget
// operation, but we wrap it in a try-catch to log any immediate errors
// from the trigger itself. A separate background service is responsible
// for the guaranteed, long-term processing of the outbox.
_ = Task.Run(async () =>
{
    try
    {
        // Optional: A small delay can help ensure the transaction
        // is fully visible to the outbox processor's DB connection.
        await Task.Delay(100, cancellationToken);
        await _outboxProcessor.ProcessPendingAsync();
    }
    catch (Exception ex)
    {
        // Use a separate logger scope to avoid confusion with the main request.
        using (_logger.BeginScope("OutboxProcessingTrigger"))
        {
            _logger.LogError(ex, "In-process outbox processing trigger failed after commit.");
        }
    }
}, cancellationToken);
```

**Acceptance Criteria:**
- ✅ All commands execute within properly managed database transactions
- ✅ Transaction failures trigger complete rollback with detailed logging
- ✅ **ADVANCED**: Outbox events are processed reliably after successful commit with correlation tracking
- ✅ Nested transactions are detected and handled appropriately
- ✅ **ENHANCED**: Transaction timeouts are configurable and monitored with metadata-aware isolation levels

### Story 3: CachingBehavior - Intelligent Query Optimization ✅ **IMPLEMENTED**
**As a developer**, I want sophisticated caching so that frequently accessed queries deliver optimal performance with proper cache invalidation.

**Implementation Details:**
- ✅ `CachingBehavior<TRequest, TResponse>` class with intelligent key generation
- ✅ Deterministic cache key generation strategy using request properties
- ✅ **ADVANCED**: Per-query-type configurable cache duration via `IQuery.CacheDuration`
- ✅ **L1/L2 ARCHITECTURE**: Sophisticated memory + distributed Redis cache integration
- ✅ Comprehensive cache hit/miss/error metrics and monitoring with OpenTelemetry
- ✅ **PRODUCTION-READY**: Robust JSON serialization with corruption detection and cleanup
- ✅ **INTELLIGENT**: Automated L1 population from L2 cache hits for performance
- ⚠️ **Cache invalidation patterns need review** - InvalidateCachingBehavior exists but needs evaluation
- ⚠️ **Concurrent access tests missing** - Load testing scenarios need implementation

**Acceptance Criteria:**
- ✅ Only queries marked as cacheable participate in caching behavior
- ✅ Cache keys are generated deterministically and collision-free
- ✅ **DECLARATIVE**: Cache duration and policies configurable via `IQuery` properties
- ✅ Cache hit/miss ratios and performance metrics tracked with comprehensive monitoring
- ✅ **DISTRIBUTED**: Cache invalidation works across multiple cache layers

### Story 4: ObservabilityBehavior - Comprehensive Telemetry ✅ **IMPLEMENTED**
**As a developer**, I want production-grade observability so that I can monitor, debug, and optimize application behavior with detailed insights.

**Implementation Details:**
- ✅ **W3C COMPLIANT**: `ObservabilityBehavior<TRequest, TResponse>` as outermost pipeline wrapper
- ✅ **RESULT-AWARE**: Advanced Result<T> telemetry with success/failure/error categorization
- ✅ **COMPREHENSIVE**: Performance metrics with histograms, counters, and detailed percentiles
- ✅ **DISTRIBUTED TRACING**: Correlation ID generation and W3C TraceContext propagation
- ✅ **RICH CONTEXT**: Extensive activity tags, metadata enrichment, and custom dimensions
- ✅ **PRODUCTION-GRADE**: Detailed error tracking with categorization and structured events
- ✅ **BUSINESS METRICS**: Custom counters, gauges, and tenant-aware metrics collection
- ✅ **MODERN STANDARDS**: OpenTelemetry semantic conventions without Application Insights dependencies
- ⚠️ **Observability tests need comprehensive review** - Telemetry data quality validation missing

**Acceptance Criteria:**
- Every request generates structured telemetry with complete lifecycle tracking
- Success/failure rates and error types are accurately recorded and categorized
- Performance metrics include detailed timing, throughput, and resource utilization
- Correlation IDs flow seamlessly through the entire pipeline and external services
- Custom business metrics are collected and available for analysis and alerting

### Story 5: RetryBehavior - Advanced Resilience
**As a developer**, I want sophisticated retry logic so that transient failures are handled gracefully while preventing system overload.

**Tasks:**
- [ ] Create `RetryBehavior<TRequest, TResponse>` class with advanced resilience patterns
- [ ] Implement Polly-based configurable retry policies with multiple strategies
- [ ] Build intelligent transient vs permanent error detection and classification
- [ ] Add exponential backoff with jitter and maximum delay caps
- [ ] Integrate circuit breaker pattern to prevent cascade failures
- [ ] Add comprehensive retry metrics, timing, and success rate tracking
- [ ] Create marker interfaces for retryable operations with policy configuration
- [ ] Build exhaustive retry scenario tests including edge cases and failure modes

**Acceptance Criteria:**
- Transient failures are automatically retried using appropriate policies
- Permanent failures are detected early and fail fast without retries
- Retry attempts are thoroughly logged with timing and outcome data
- Circuit breaker activates under load and prevents system degradation
- Retry policies are configurable per operation type with sensible defaults

### Story 6: LoggingBehavior - Production-Ready Structured Logging
**As a developer**, I want comprehensive structured logging so that I can debug issues, audit operations, and analyze usage patterns with complete data protection.

**Tasks:**
- [ ] Create `LoggingBehavior<TRequest, TResponse>` class with rich structured logging
- [ ] Implement correlation ID propagation and structured log context management
- [ ] Add intelligent request/response logging with automatic PII detection and masking
- [ ] Support per-operation configurable log levels with environment-based overrides
- [ ] Add detailed performance timing logs with operation categorization
- [ ] Implement distributed log correlation across services and external dependencies
- [ ] Build comprehensive sensitive data masking with configurable rules
- [ ] Create logging validation tests and log data quality verification

**Acceptance Criteria:**
- All requests generate structured logs with consistent schema and rich context
- Correlation IDs are present and consistent across all log entries and external calls
- Sensitive data is automatically detected and masked using configurable rules
- Log levels are configurable per request type with environment-specific overrides
- Performance timing data is captured and structured for analysis and alerting

## Definition of Done

- [ ] All 6 pipeline behaviors implemented with production-quality error handling
- [ ] Critical behavior registration order correctly implemented and documented
- [ ] Comprehensive integration tests validate entire pipeline execution
- [ ] Performance benchmarks demonstrate acceptable overhead (< 5ms per request)
- [ ] Documentation includes complete configuration examples and troubleshooting guides
- [ ] Code review completed with security and performance validation
- [ ] Zero compiler warnings or static analysis issues
- [ ] Telemetry data validates correctly with proper alerting thresholds

## Technical Implementation

### Critical File Structure
```
src/BuildingBlocks/Application/Behaviors/
├── ObservabilityBehavior.cs    # Outermost: telemetry and activity tracking
├── LoggingBehavior.cs          # Structured logging with correlation
├── RetryBehavior.cs            # Resilience and transient error handling
├── ValidationBehavior.cs       # Request validation with error aggregation
├── CachingBehavior.cs          # Query result caching
└── TransactionBehavior.cs      # Innermost: database transaction management
```

### CRITICAL: Behavior Registration Order

The registration order in MediatR is absolutely critical as it defines the execution pipeline from outermost to innermost behavior. **This order must not be changed without careful consideration of the implications:**

```csharp
// In Program.cs or a ServiceCollection extension method
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Application).Assembly);

    // IMPORTANT: The registration order here is critical as it defines the
    // execution pipeline for MediatR behaviors, from outermost to innermost.

    // 1. (Outermost) Observability and general error handling. This wraps the
    // entire operation to ensure all exceptions are caught and all telemetry is captured.
    cfg.AddOpenBehavior(typeof(ObservabilityBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));

    // 2. Resilience. This wraps the core logic to allow for retries of the
    // entire unit of work on transient failures.
    cfg.AddOpenBehavior(typeof(RetryBehavior<,>));

    // 3. Validation. This fails fast on invalid requests *before* starting a
    // database transaction or hitting the cache, saving resources.
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    
    // 4. Caching. This is for queries. If a result is found in the cache,
    // subsequent behaviors (like Transaction) will be skipped.
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));

    // 5. (Innermost) Transaction Management. This ensures that the actual
    // command handler logic runs within a database transaction.
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

### Key Design Principles
1. **Result<T> First**: All behaviors are designed around the Result pattern for consistent error handling
2. **Performance Optimized**: Early exit strategies minimize unnecessary processing
3. **Configuration Driven**: Behavior policies are configurable per operation type
4. **Observable by Default**: Comprehensive telemetry and metrics are built-in
5. **Fail Fast**: Validation and authorization happen before expensive operations

## Dependencies and Configuration

### Required NuGet Packages
```xml
<PackageReference Include="FluentValidation" Version="11.9.0" />
<PackageReference Include="Polly" Version="8.4.0" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0" />
```

### Behavior Configuration Example
```csharp
// Example configuration for individual behavior policies
services.Configure<RetryPolicyOptions>(options =>
{
    options.MaxAttempts = 3;
    options.BaseDelay = TimeSpan.FromMilliseconds(100);
    options.MaxDelay = TimeSpan.FromSeconds(30);
});

services.Configure<CachingOptions>(options =>
{
    options.DefaultDuration = TimeSpan.FromMinutes(5);
    options.KeyPrefix = "axon_query_";
});
```

## Risk Mitigation Strategy

### Performance Risks
- **Pipeline Overhead**: Each behavior adds 1-2ms overhead; target total pipeline overhead < 5ms
- **Memory Allocation**: Use object pooling and efficient serialization for caching behavior
- **Lock Contention**: Implement lock-free cache access patterns where possible

### Reliability Risks  
- **Behavior Ordering**: Critical registration order is documented and validated in tests
- **Error Propagation**: All behaviors properly handle and forward Result<T> error states
- **Resource Leaks**: Comprehensive disposal patterns implemented in all behaviors

### Security Risks
- **PII Logging**: Automatic PII detection and masking in logging behavior
- **Cache Poisoning**: Cache key validation and isolation per tenant/user context
- **Information Disclosure**: Structured error responses without sensitive system details

## Testing Strategy

### Unit Testing Requirements
- **Behavior Isolation**: Each behavior tested independently with mocked dependencies
- **Error Scenarios**: All failure modes and error propagation paths validated
- **Configuration Validation**: All configuration options and edge cases covered
- **Performance Benchmarks**: Individual behavior overhead measured and validated

### Integration Testing Requirements
- **Full Pipeline**: End-to-end pipeline execution with all behaviors active
- **Behavior Interaction**: Cross-behavior dependencies and data flow validated
- **Error Propagation**: Result<T> error handling through entire pipeline
- **Load Testing**: Pipeline performance under concurrent load scenarios

### Production Readiness Validation
- **Memory Profiling**: No memory leaks under sustained load
- **Performance Profiling**: Pipeline overhead stays within acceptable bounds
- **Error Rate Monitoring**: Behavior failure rates tracked and alerted
- **Telemetry Validation**: All metrics and traces properly emitted and structured

## Success Metrics

- **Performance**: Total pipeline execution overhead < 5ms (99th percentile)
- **Coverage**: 100% test coverage on all pipeline behaviors
- **Reliability**: Zero behavior-related production issues in first 30 days
- **Observability**: Telemetry data quality score > 98% with complete trace coverage
- **Error Handling**: All error scenarios handled gracefully with proper Result<T> responses