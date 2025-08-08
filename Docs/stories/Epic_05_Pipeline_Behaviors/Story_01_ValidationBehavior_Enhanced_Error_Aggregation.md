# Story 01: ValidationBehavior - Enhanced Error Aggregation

## Story Overview
**Story ID**: Epic_05_Story_01  
**Story Name**: ValidationBehavior - Enhanced Error Aggregation  
**Estimated Duration**: 1 day  
**Dependencies**: 
- Epic_03 (Error System with Result Pattern)
- Epic_04 (CQRS Foundation)
- FluentValidation package

## User Story
**As a developer**, I want robust automatic validation so that invalid requests are rejected early with comprehensive, well-structured error messages compatible with our Result pattern.

## Acceptance Criteria
- [ ] ValidationBehavior class created with Result<T> pattern integration
- [ ] Integration with FluentValidation framework using IValidator<T> collection
- [ ] Property-grouped error aggregation for better client experience
- [ ] Support for multiple validators per request with proper error merging
- [ ] Comprehensive validation metrics via OpenTelemetry
- [ ] Selective validation (commands always, queries when marked with IValidatable)
- [ ] Unit tests covering all validation scenarios
- [ ] Integration tests with complex validation rules

## Technical Implementation

### Core Components

#### 1. ValidationBehavior Class
```csharp
namespace Axon.BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;
    private readonly IMetrics _metrics;
    private static readonly Counter<long> ValidationCounter = Metrics.CreateCounter<long>(
        "axon.validation.total",
        description: "Total validation attempts");
    private static readonly Histogram<double> ValidationDuration = Metrics.CreateHistogram<double>(
        "axon.validation.duration",
        unit: "ms",
        description: "Validation execution duration");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Implementation in tasks
    }
}
```

#### 2. Enhanced Error Aggregation
```csharp
private static ValidationError CreateValidationError(ValidationFailure[] failures)
{
    var errors = failures
        .GroupBy(e => e.PropertyName)
        .Select(g => new ValidationError.FieldError(
            Field: g.Key,
            Messages: g.Select(e => e.ErrorMessage).ToArray()))
        .ToArray();

    return new ValidationError(errors);
}
```

### Tasks

#### Task 1: Create ValidationBehavior Class
- [ ] Create file: `src/BuildingBlocks/Application/Behaviors/ValidationBehavior.cs`
- [ ] Inject IEnumerable<IValidator<TRequest>> for multiple validator support
- [ ] Add ILogger<ValidationBehavior<TRequest, TResponse>> dependency
- [ ] Add IMetrics for OpenTelemetry integration
- [ ] Implement IPipelineBehavior<TRequest, TResponse> interface

#### Task 2: Implement Validation Logic
- [ ] Check if request implements ICommand (always validate)
- [ ] Check if request implements IValidatable (opt-in for queries)
- [ ] Execute all validators using FluentValidation's ValidateAsync
- [ ] Combine validation results from multiple validators
- [ ] Continue to next handler if validation passes

#### Task 3: Enhanced Error Aggregation
- [ ] Group validation failures by PropertyName
- [ ] Create ValidationError with field-level errors
- [ ] Return Result<TResponse>.Failure with ValidationError
- [ ] Ensure error format matches ProblemDetails requirements

#### Task 4: Add OpenTelemetry Metrics
- [ ] Track validation execution time with histogram
- [ ] Count validation successes/failures with counter
- [ ] Add tags: request_type, validation_result, validator_count
- [ ] Record validator execution time per validator

#### Task 5: Implement Selective Validation
- [ ] Create IValidatable marker interface in Core project
- [ ] Check request type in Handle method
- [ ] Log decision when skipping validation for non-validatable queries
- [ ] Document validation strategy in code comments

#### Task 6: Unit Tests
- [ ] Test single validator with validation errors
- [ ] Test multiple validators combining errors correctly
- [ ] Test property grouping with multiple errors per field
- [ ] Test command validation (always validates)
- [ ] Test query without IValidatable (skips validation)
- [ ] Test query with IValidatable (validates)
- [ ] Test empty validator collection scenario
- [ ] Test cancellation token propagation

#### Task 7: Integration Tests
- [ ] Complex nested object validation
- [ ] Cross-property validation rules
- [ ] Async validation rules with database checks
- [ ] Performance test with 10+ validators
- [ ] Verify Result<T> error propagation

## Definition of Done
- [ ] ValidationBehavior fully implemented with Result pattern
- [ ] All unit and integration tests passing
- [ ] OpenTelemetry metrics properly exposed
- [ ] Commands always validated, queries selectively
- [ ] Error messages grouped by property with proper structure
- [ ] Performance benchmark: < 5ms overhead for typical request
- [ ] Code follows project conventions (file-scoped namespaces, nullable reference types)
- [ ] Zero compiler warnings

## Technical Notes

### Registration Order in DI Container
```csharp
// In Program.cs or module registration
services.AddMediatR(cfg =>
{
    cfg.AddOpenBehavior(typeof(ObservabilityBehavior<,>));  // Outermost
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));     // This story
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));    // Innermost
});
```

### Error Structure Example
```json
{
  "type": "ValidationError",
  "title": "Validation failed",
  "status": 400,
  "errors": [
    {
      "field": "Email",
      "messages": ["Email is required", "Email must be valid format"]
    },
    {
      "field": "Password",
      "messages": ["Password must be at least 8 characters"]
    }
  ]
}
```

### Performance Considerations
- Validators executed sequentially (parallel execution considered risky for shared resources)
- Early exit if no validators registered (< 0.1ms overhead)
- Reuse ValidationContext when possible
- Consider caching validator instances per request type

## Dependencies
- FluentValidation 11.9.0
- Microsoft.Extensions.Logging.Abstractions
- System.Diagnostics.Metrics (part of .NET)
- Axon.BuildingBlocks.Core (for Result pattern and Error types)

## References
- Epic_03 Error System documentation
- FluentValidation documentation: https://docs.fluentvalidation.net
- Result Pattern implementation: `src/BuildingBlocks/Core/Functional/Results/Result.cs`