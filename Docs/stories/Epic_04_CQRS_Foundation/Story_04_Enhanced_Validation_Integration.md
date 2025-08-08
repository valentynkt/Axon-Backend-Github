# Story 04: Enhanced Validation Integration with Metadata Context

**Story ID:** AXON-CQRS-004  
**Epic:** Epic_04_CQRS_Foundation  
**Priority:** P1 - High  
**Estimated Effort:** 5 hours  
**Dependencies:** Story_01 (W3C TraceContext and Metadata)  
**Target Sprint:** Current  

---

## 📋 User Story

**As a** backend developer implementing CQRS handlers,  
**I want** enhanced validation that leverages metadata context and provides rich error aggregation,  
**So that** I can implement comprehensive validation rules with tenant-aware, feature-flag sensitive validation logic.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** 
  - Basic ValidationBehavior exists with FluentValidation
  - No metadata-aware validation
  - Simple error aggregation
  - Limited context passing to validators

- **Integration Points:**
  - FluentValidation library
  - MediatR pipeline behaviors
  - Request metadata from Story 01
  - Result<T> error handling pattern

- **Technology Stack:** 
  - FluentValidation 11+
  - MediatR for CQRS
  - Result<T> pattern
  - W3C TraceContext

- **Architectural Layer:** BuildingBlocks/Application/Behaviors

### Patterns to Follow

```csharp
// Existing validation pattern
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Metadata-Aware Validation**
   - [ ] Validators can access request metadata
   - [ ] Tenant-specific validation rules
   - [ ] Feature flag conditional validation
   - [ ] User context validation

2. **Enhanced Error Aggregation**
   - [ ] Collect all validation errors before failing
   - [ ] Group errors by property/field
   - [ ] Include error codes for client handling
   - [ ] Maintain error severity levels

3. **Context Propagation**
   - [ ] Pass W3C trace context to validators
   - [ ] Include correlation information in errors
   - [ ] Support async validation scenarios
   - [ ] Maintain request context throughout validation

4. **Performance Optimization**
   - [ ] Short-circuit validation on critical errors
   - [ ] Parallel validation for independent rules
   - [ ] Cache validation results where appropriate
   - [ ] Minimize allocations during validation

### Non-Functional Requirements

1. **Maintainability**
   - [ ] Clear separation of business vs technical validation
   - [ ] Reusable validation components
   - [ ] Consistent error message formatting
   - [ ] Easy validator composition

2. **Testability**
   - [ ] Isolated validator unit tests
   - [ ] Mock metadata and context injection
   - [ ] Integration test support
   - [ ] Validation behavior testing

3. **Observability**
   - [ ] Log validation failures with context
   - [ ] Track validation performance metrics
   - [ ] Include validation info in traces
   - [ ] Monitor validation rule usage

---

## 🔧 Technical Implementation

### Files to Create/Modify

```yaml
Enhanced_Files:
  - src/BuildingBlocks/Application/Behaviors/ValidationBehavior.cs

New_Files:
  - src/BuildingBlocks/Application/Validation/IValidationContext.cs
  - src/BuildingBlocks/Application/Validation/ValidationContext.cs
  - src/BuildingBlocks/Application/Validation/IMetadataValidator.cs
  - src/BuildingBlocks/Application/Validation/ValidatorBase.cs
  - src/BuildingBlocks/Application/Validation/Extensions/ValidationExtensions.cs

Configuration:
  - src/BuildingBlocks/Application/Configuration/ValidationConfiguration.cs

Tests:
  - tests/BuildingBlocks.Tests/Application/Behaviors/ValidationBehaviorTests.cs
  - tests/BuildingBlocks.Tests/Application/Validation/MetadataValidatorTests.cs
```

### Implementation Steps

#### Step 1: Validation Context Interface

```csharp
public interface IValidationContext
{
    /// <summary>
    /// Current request metadata
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
    
    /// <summary>
    /// W3C Trace context information
    /// </summary>
    string? TraceId { get; }
    string? SpanId { get; }
    
    /// <summary>
    /// Request identification
    /// </summary>
    Guid RequestId { get; }
    DateTime RequestedAt { get; }
    
    /// <summary>
    /// Tenant information from metadata
    /// </summary>
    string? TenantId => GetMetadata<string>("TenantId");
    
    /// <summary>
    /// User information from metadata  
    /// </summary>
    string? UserId => GetMetadata<string>("UserId");
    
    /// <summary>
    /// Feature flags from metadata
    /// </summary>
    IReadOnlyDictionary<string, bool> FeatureFlags => GetFeatureFlags();
    
    /// <summary>
    /// Helper to get strongly typed metadata
    /// </summary>
    T? GetMetadata<T>(string key);
    
    /// <summary>
    /// Check if feature flag is enabled
    /// </summary>
    bool IsFeatureEnabled(string featureName);
}

public class ValidationContext : IValidationContext
{
    public IReadOnlyDictionary<string, object> Metadata { get; }
    public string? TraceId { get; }
    public string? SpanId { get; }
    public Guid RequestId { get; }
    public DateTime RequestedAt { get; }
    
    public ValidationContext(IAxonRequest request)
    {
        Metadata = request.Metadata;
        TraceId = request.TraceId;
        SpanId = request.SpanId;
        RequestId = request.RequestId;
        RequestedAt = request.RequestedAt;
    }
    
    public T? GetMetadata<T>(string key)
    {
        return Metadata.TryGetValue(key, out var value) && value is T typedValue 
            ? typedValue 
            : default;
    }
    
    public bool IsFeatureEnabled(string featureName)
    {
        var flags = GetFeatureFlags();
        return flags.TryGetValue(featureName, out var enabled) && enabled;
    }
    
    private IReadOnlyDictionary<string, bool> GetFeatureFlags()
    {
        if (Metadata.TryGetValue("FeatureFlags", out var flags) && 
            flags is IReadOnlyDictionary<string, bool> typedFlags)
        {
            return typedFlags;
        }
        
        return new Dictionary<string, bool>().AsReadOnly();
    }
}
```

#### Step 2: Enhanced Validation Behavior

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxonRequest<TResponse>
    where TResponse : class
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger,
        IServiceProvider serviceProvider)
    {
        _validators = validators;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }
        
        // Create validation context
        var context = new ValidationContext(request);
        
        _logger.LogDebug("Validating {RequestType} with {ValidatorCount} validators (Trace: {TraceId})",
            typeof(TRequest).Name, _validators.Count(), request.TraceId);
        
        // Inject context into validators that support it
        foreach (var validator in _validators)
        {
            if (validator is IMetadataValidator metadataValidator)
            {
                metadataValidator.SetContext(context);
            }
        }
        
        // Run all validators
        var validationTasks = _validators
            .Select(validator => validator.ValidateAsync(request, cancellationToken))
            .ToArray();
        
        var validationResults = await Task.WhenAll(validationTasks);
        
        // Aggregate all errors
        var failures = validationResults
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)
            .ToList();
        
        if (failures.Any())
        {
            var aggregatedError = CreateValidationError(failures, context);
            
            _logger.LogWarning("Validation failed for {RequestType} with {ErrorCount} errors (Trace: {TraceId}): {Errors}",
                typeof(TRequest).Name, failures.Count, request.TraceId,
                string.Join(", ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));
            
            // Return validation failure as Result<T>
            if (typeof(TResponse).IsGenericType && 
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = typeof(TResponse).GetGenericArguments()[0];
                var failureMethod = typeof(Result<>)
                    .MakeGenericType(resultType)
                    .GetMethod(nameof(Result<object>.Failure), new[] { typeof(Error) });
                
                return (TResponse)failureMethod!.Invoke(null, new object[] { aggregatedError })!;
            }
            
            // For non-Result types, throw validation exception
            throw new ValidationException("Validation failed", failures);
        }
        
        _logger.LogDebug("Validation passed for {RequestType} (Trace: {TraceId})",
            typeof(TRequest).Name, request.TraceId);
        
        return await next();
    }
    
    private Error CreateValidationError(List<ValidationFailure> failures, IValidationContext context)
    {
        var errorDetails = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => new
                {
                    Message = f.ErrorMessage,
                    Code = f.ErrorCode,
                    Severity = f.Severity.ToString()
                }).ToArray()
            );
        
        var metadata = new Dictionary<string, object>
        {
            ["TraceId"] = context.TraceId ?? "unknown",
            ["RequestId"] = context.RequestId,
            ["TenantId"] = context.TenantId ?? "unknown",
            ["ValidationErrors"] = errorDetails,
            ["ErrorCount"] = failures.Count
        };
        
        return Error.Validation(
            "VALIDATION_FAILED",
            $"Validation failed with {failures.Count} error(s)",
            metadata);
    }
}
```

#### Step 3: Metadata-Aware Validator Base

```csharp
public interface IMetadataValidator
{
    void SetContext(IValidationContext context);
}

public abstract class ValidatorBase<T> : AbstractValidator<T>, IMetadataValidator
    where T : class
{
    protected IValidationContext? Context { get; private set; }
    
    public void SetContext(IValidationContext context)
    {
        Context = context;
    }
    
    /// <summary>
    /// Apply rules only when feature is enabled
    /// </summary>
    protected IRuleBuilderOptions<T, TProperty> WhenFeatureEnabled<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder,
        string featureName)
    {
        return ruleBuilder.When(x => Context?.IsFeatureEnabled(featureName) == true);
    }
    
    /// <summary>
    /// Apply rules only for specific tenant
    /// </summary>
    protected IRuleBuilderOptions<T, TProperty> WhenTenant<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder,
        string tenantId)
    {
        return ruleBuilder.When(x => Context?.TenantId == tenantId);
    }
    
    /// <summary>
    /// Apply different validation based on metadata
    /// </summary>
    protected IRuleBuilderOptions<T, TProperty> WhenMetadata<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder,
        string key,
        object expectedValue)
    {
        return ruleBuilder.When(x => 
            Context?.GetMetadata<object>(key)?.Equals(expectedValue) == true);
    }
}
```

#### Step 4: Usage Example with Enhanced Validators

```csharp
public class CreateUserCommandValidator : ValidatorBase<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        // Basic validation rules
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithErrorCode("INVALID_EMAIL");
        
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .WithErrorCode("WEAK_PASSWORD");
        
        // Feature flag dependent validation
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WhenFeatureEnabled("RequirePhoneNumber")
            .WithErrorCode("PHONE_REQUIRED")
            .WithMessage("Phone number is required when phone verification is enabled");
        
        // Tenant-specific validation
        RuleFor(x => x.Department)
            .NotEmpty()
            .WhenTenant("enterprise-tenant")
            .WithErrorCode("DEPARTMENT_REQUIRED")
            .WithMessage("Department is required for enterprise accounts");
        
        // Complex metadata-based validation
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .WhenMetadata("UserType", "Business")
            .WithErrorCode("COMPANY_NAME_REQUIRED");
        
        // Async validation with context
        RuleFor(x => x.Email)
            .MustAsync(BeUniqueEmailForTenant)
            .WithErrorCode("DUPLICATE_EMAIL")
            .WithMessage("Email already exists in this tenant");
    }
    
    private async Task<bool> BeUniqueEmailForTenant(string email, CancellationToken cancellationToken)
    {
        if (Context?.TenantId == null) return true;
        
        // Use tenant context in validation
        // This would typically call a repository or service
        var repository = GetService<IUserRepository>();
        return !await repository.EmailExistsInTenantAsync(email, Context.TenantId, cancellationToken);
    }
    
    private T GetService<T>() where T : notnull
    {
        // Access service provider through context
        return (T)Context!.Metadata["ServiceProvider"];
    }
}
```

---

## 🧪 Testing Requirements

### Unit Tests

```csharp
[Fact]
public async Task Should_Pass_Validation_Context_To_Validators()
{
    // Arrange
    var request = new CreateUserCommand("test@example.com")
    {
        Metadata = new Dictionary<string, object>
        {
            ["TenantId"] = "tenant-123",
            ["FeatureFlags"] = new Dictionary<string, bool>
            {
                ["RequirePhoneNumber"] = true
            }.AsReadOnly()
        }.AsReadOnly()
    };
    
    var validator = new Mock<IValidator<CreateUserCommand>>();
    validator.As<IMetadataValidator>();
    
    var behavior = new ValidationBehavior<CreateUserCommand, Result<UserId>>(
        new[] { validator.Object }, logger, serviceProvider);
    
    // Act
    await behavior.Handle(request, next, CancellationToken.None);
    
    // Assert
    validator.As<IMetadataValidator>()
        .Verify(v => v.SetContext(It.Is<IValidationContext>(ctx => 
            ctx.TenantId == "tenant-123" && 
            ctx.IsFeatureEnabled("RequirePhoneNumber"))), Times.Once);
}

[Fact]
public async Task Should_Create_Rich_Validation_Error_With_Metadata()
{
    // Arrange
    var failures = new[]
    {
        new ValidationFailure("Email", "Email is required") { ErrorCode = "REQUIRED" },
        new ValidationFailure("Password", "Password too weak") { ErrorCode = "WEAK_PASSWORD" }
    };
    
    var validator = new Mock<IValidator<CreateUserCommand>>();
    validator.Setup(v => v.ValidateAsync(It.IsAny<CreateUserCommand>(), default))
        .ReturnsAsync(new ValidationResult(failures));
    
    // Act
    var result = await behavior.Handle(request, next, CancellationToken.None);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Type.Should().Be(ErrorType.Validation);
    result.Error.Metadata.Should().ContainKey("ValidationErrors");
    result.Error.Metadata.Should().ContainKey("TraceId");
}
```

### Integration Tests

```csharp
[Fact]
public async Task Should_Validate_With_Feature_Flags_From_Metadata()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    services.AddValidation();
    services.AddScoped<IValidator<CreateUserCommand>, CreateUserCommandValidator>();
    
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();
    
    var command = new CreateUserCommand("test@example.com")
        .WithMetadata<CreateUserCommand>("FeatureFlags", new Dictionary<string, bool>
        {
            ["RequirePhoneNumber"] = true
        });
    
    // Act
    var result = await mediator.Send(command);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Metadata["ValidationErrors"]
        .Should().ContainKey("PhoneNumber");
}
```

---

## 📐 Architecture Considerations

### Validation Layers

1. **Syntax Validation**: Basic format, required fields
2. **Semantic Validation**: Business rules, cross-field validation  
3. **Authorization**: Permission-based validation
4. **External Validation**: Database uniqueness, external service validation

### Performance Optimization

- Parallel validation execution for independent rules
- Early termination on critical errors
- Caching of expensive validation results
- Async validation for I/O bound operations

### Context Injection Strategy

```csharp
// Option 1: Through behavior (current approach)
validator.SetContext(context);

// Option 2: Through DI container (alternative)
services.AddScoped<IValidationContext>(provider => context);

// Option 3: Through validator constructor (for simple cases)
public MyValidator(IValidationContext context) : base()
```

---

## 📦 Definition of Done

- [ ] Enhanced ValidationBehavior with metadata context
- [ ] IValidationContext interface and implementation
- [ ] ValidatorBase with metadata helpers
- [ ] Unit tests with 100% coverage
- [ ] Integration tests with feature flag validation
- [ ] Performance benchmarks documented
- [ ] Migration guide for existing validators
- [ ] Code review approved

---

## 🔄 Migration Strategy

### Phase 1: Non-Breaking Enhancement
- Deploy enhanced ValidationBehavior
- Existing validators continue to work
- New validators can opt-in to metadata features

### Phase 2: Selective Migration
```csharp
// Before: Simple validator
public class MyValidator : AbstractValidator<MyCommand>
{
    // Basic rules only
}

// After: Enhanced validator
public class MyValidator : ValidatorBase<MyCommand>
{
    // Basic rules + metadata-aware rules
}
```

### Phase 3: Advanced Features
- Implement tenant-specific validation
- Add feature flag controlled validation
- Create reusable validation components

---

## 📊 Success Metrics

- Zero breaking changes to existing validators
- 100% of new validators use metadata context
- Validation error detail richness improved by 3x
- Validation performance maintained < 1ms overhead

---

## 🚀 Follow-up Stories

1. **Story 05**: Cross-Field Validation with Business Rules
2. **Story 06**: Async External Validation Services  
3. **Story 07**: Validation Result Caching
4. **Story 08**: Multi-Tenant Validation Rules Engine