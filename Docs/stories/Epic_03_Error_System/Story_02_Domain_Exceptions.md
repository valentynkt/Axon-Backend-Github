# Story 02: Domain Exception Classes

**Story ID:** AXON-ERR-002  
**Epic:** Epic_03_Enhanced_Error_System  
**Priority:** P0 - Critical Foundation  
**Estimated Effort:** ✅ COMPLETED (Originally 5 hours)  
**Dependencies:** Story_01_Core_Error_Types  
**Status:** ✅ IMPLEMENTED - Verification Required  

---

## 📋 User Story

**As a** domain developer implementing business logic in Axon Backend,  
**I want** structured domain exception classes that seamlessly integrate with the Error system and Result pattern,  
**So that** I can handle business rule violations, validation failures, and domain errors consistently while maintaining clean architecture boundaries and enabling proper error recovery strategies.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** ✅ **FULLY IMPLEMENTED** - Structured domain exceptions exist in `src/BuildingBlocks/Core/Diagnostics/Exceptions/` with full Error system integration, FluentValidation support, and Result pattern conversion
- **Integration Points:**
  - Enhanced Error system from Story 01
  - Domain aggregates and entities
  - Business rule validation (IBusinessRule pattern)
  - FluentValidation pipeline
  - Result<T> pattern conversion
- **Technology Stack:** .NET 10, Domain-Driven Design patterns, FluentValidation
- **Architectural Layer:** BuildingBlocks/Core (Domain Support Layer)

### Patterns to Follow

```csharp
// Clean Architecture domain pattern
public interface IBusinessRule
{
    string Code { get; }
    string Message { get; }
    bool IsBroken();
}

// Result pattern integration
public Result<T> Operation()
{
    try { /* ... */ }
    catch (DomainException ex)
    {
        return Result<T>.Failure(ex.Error);
    }
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **DomainException Base Class**
   ```csharp
   public class DomainException : Exception
   {
       public Error Error { get; }
       public IReadOnlyList<Error> Errors { get; }
       
       // Constructors for single/multiple errors
       public DomainException(Error error);
       public DomainException(string message, Error error);
       public DomainException(IEnumerable<Error> errors);
       public DomainException(string message, IEnumerable<Error> errors);
   }
   ```

2. **BusinessRuleException Implementation**
   ```csharp
   public sealed class BusinessRuleException : DomainException
   {
       public IBusinessRule? BusinessRule { get; }
       public IReadOnlyList<IBusinessRule>? BusinessRules { get; }
       
       // Constructors for single/multiple rule violations
       public BusinessRuleException(IBusinessRule rule);
       public BusinessRuleException(string message, IBusinessRule rule);
       public BusinessRuleException(IEnumerable<IBusinessRule> rules);
   }
   ```

3. **ValidationException with FluentValidation Support**
   ```csharp
   public sealed class ValidationException : DomainException
   {
       public IReadOnlyList<ValidationFailure> ValidationFailures { get; }
       
       // FluentValidation compatibility
       public ValidationException(IEnumerable<ValidationFailure> failures);
       public ValidationException(string message, IEnumerable<ValidationFailure> failures);
       
       // Helper methods
       public bool HasErrorsForProperty(string propertyName);
       public IEnumerable<ValidationFailure> GetErrorsForProperty(string propertyName);
   }
   ```

4. **Specialized Domain Exceptions**
   - ✅ `AggregateNotFoundException` - When aggregate not found by ID
   - ✅ `ConcurrencyException` - For optimistic concurrency violations
   - ✅ `DomainStateException` - For invalid state transitions
   - ✅ `InvariantViolationException` - For aggregate invariant violations

5. **Exception to Result Conversion**
   - ✅ Extension method `ToResult<T>()` on all domain exceptions
   - ✅ Automatic Error extraction with metadata preservation
   - ✅ Support for error aggregation in validation scenarios

6. **Business Rule Interface Enhancement**
   ```csharp
   public interface IBusinessRule
   {
       string Code { get; }
       string Message { get; }
       bool IsBroken();
       Error ToError(); // New: Direct Error conversion
   }
   ```

### Integration Requirements

7. **Error System Integration**
   - ✅ All exceptions create properly categorized Errors
   - ✅ Metadata from exceptions flows to Error metadata
   - ✅ Severity levels set appropriately per exception type
   - ✅ Correlation IDs preserved through exception chain

8. **FluentValidation Compatibility**
   - ✅ ValidationException works with FluentValidation ValidationResult
   - ✅ Property-level error access for UI binding
   - ✅ Severity levels (Error, Warning, Info) supported
   - ✅ Custom error codes preserved

9. **Serialization Support**
   - ✅ All exceptions are [Serializable] for distributed scenarios
   - ✅ Custom serialization for Error properties
   - ✅ Preserve metadata through serialization
   - ✅ Support for Exception.Data dictionary

### Quality Requirements

10. **Testing Coverage**
    - ✅ Unit tests for all exception types
    - ✅ Serialization round-trip tests
    - ✅ FluentValidation integration tests
    - ✅ Result conversion tests
    - ✅ Business rule violation tests
    - ✅ Error aggregation tests

11. **Documentation Requirements**
    - ✅ XML documentation for all public members
    - ✅ Usage examples in comments
    - ✅ Exception handling best practices guide
    - ✅ Migration guide from existing exceptions

---

## 🛠 Technical Design

### File Structure

```
BuildingBlocks/Core/
├── Diagnostics/
│   ├── Exceptions/
│   │   ├── DomainException.cs
│   │   ├── BusinessRuleException.cs
│   │   ├── ValidationException.cs
│   │   ├── AggregateNotFoundException.cs
│   │   ├── ConcurrencyException.cs
│   │   ├── DomainStateException.cs
│   │   └── InvariantViolationException.cs
│   └── Interfaces/
│       └── IBusinessRule.cs
└── Functional/
    └── Extensions/
        └── ExceptionExtensions.cs
```

### Implementation Details

```csharp
// DomainException.cs
namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Base exception for all domain-related errors.
/// Provides structured error information and integrates with Result pattern.
/// </summary>
[Serializable]
public class DomainException : Exception
{
    /// <summary>
    /// The primary error information
    /// </summary>
    public Error Error { get; }
    
    /// <summary>
    /// All errors (for scenarios with multiple failures)
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }
    
    public DomainException(Error error) 
        : base(error.Message)
    {
        Error = error;
        Errors = new[] { error };
        
        // Preserve error metadata in Exception.Data
        foreach (var kvp in error.Metadata ?? new Dictionary<string, object>())
        {
            Data[kvp.Key] = kvp.Value;
        }
    }
    
    public DomainException(IEnumerable<Error> errors)
        : base(CreateAggregateMessage(errors))
    {
        var errorList = errors.ToList();
        if (!errorList.Any())
            throw new ArgumentException("At least one error is required", nameof(errors));
            
        Error = errorList.Count == 1 
            ? errorList[0] 
            : Error.Aggregate(errorList.ToArray());
        Errors = errorList;
    }
    
    // Custom serialization
    protected DomainException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Error = (Error)info.GetValue(nameof(Error), typeof(Error))!;
        Errors = (IReadOnlyList<Error>)info.GetValue(nameof(Errors), typeof(IReadOnlyList<Error>))!;
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Error), Error);
        info.AddValue(nameof(Errors), Errors);
    }
}

// BusinessRuleException.cs
public sealed class BusinessRuleException : DomainException
{
    public IBusinessRule? BusinessRule { get; }
    public IReadOnlyList<IBusinessRule>? BusinessRules { get; }
    
    public BusinessRuleException(IBusinessRule rule)
        : base(rule.ToError())
    {
        BusinessRule = rule;
    }
    
    public BusinessRuleException(IEnumerable<IBusinessRule> rules)
        : base(rules.Select(r => r.ToError()))
    {
        var ruleList = rules.ToList();
        BusinessRules = ruleList;
        
        // Check all rules are actually broken
        var brokenRules = ruleList.Where(r => r.IsBroken()).ToList();
        if (brokenRules.Count != ruleList.Count)
        {
            throw new ArgumentException("All business rules must be broken", nameof(rules));
        }
    }
    
    /// <summary>
    /// Static helper for checking and throwing
    /// </summary>
    public static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleException(rule);
        }
    }
    
    public static void CheckRules(params IBusinessRule[] rules)
    {
        var brokenRules = rules.Where(r => r.IsBroken()).ToList();
        if (brokenRules.Any())
        {
            throw new BusinessRuleException(brokenRules);
        }
    }
}

// ValidationException.cs with FluentValidation support
public sealed class ValidationException : DomainException
{
    public IReadOnlyList<ValidationFailure> ValidationFailures { get; }
    
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base(ConvertFailuresToErrors(failures))
    {
        ValidationFailures = failures.ToList();
    }
    
    private static IEnumerable<Error> ConvertFailuresToErrors(IEnumerable<ValidationFailure> failures)
    {
        return failures.Select(f => 
        {
            var error = Error.Validation(
                f.ErrorMessage,
                f.ErrorCode ?? "VALIDATION_ERROR")
                .WithMetadata("PropertyName", f.PropertyName)
                .WithMetadata("AttemptedValue", f.AttemptedValue ?? "null");
                
            // Map FluentValidation severity
            if (f.Severity == Severity.Warning)
                error = error.WithSeverity(ErrorSeverity.Warning);
            else if (f.Severity == Severity.Info)
                error = error.WithSeverity(ErrorSeverity.Info);
                
            return error;
        });
    }
    
    public bool HasErrorsForProperty(string propertyName)
    {
        return ValidationFailures.Any(f => 
            f.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
    
    public IEnumerable<ValidationFailure> GetErrorsForProperty(string propertyName)
    {
        return ValidationFailures.Where(f => 
            f.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
    
    public IEnumerable<string> GetErrorMessagesForProperty(string propertyName)
    {
        return GetErrorsForProperty(propertyName).Select(f => f.ErrorMessage);
    }
}
```

### Business Rule Pattern Implementation

```csharp
// IBusinessRule.cs
public interface IBusinessRule
{
    string Code { get; }
    string Message { get; }
    bool IsBroken();
    
    /// <summary>
    /// Convert this business rule to an Error
    /// </summary>
    Error ToError()
    {
        return Error.BusinessRule(Message, Code);
    }
}

// Example business rule implementation
public sealed class OrderMustHaveItemsRule : IBusinessRule
{
    private readonly int _itemCount;
    
    public OrderMustHaveItemsRule(int itemCount)
    {
        _itemCount = itemCount;
    }
    
    public string Code => "ORDER_EMPTY";
    public string Message => "Order must contain at least one item";
    public bool IsBroken() => _itemCount <= 0;
}

// Usage in aggregate
public class Order : AggregateRoot<OrderId>
{
    public void Submit()
    {
        BusinessRuleException.CheckRule(new OrderMustHaveItemsRule(Items.Count));
        BusinessRuleException.CheckRule(new OrderMustBeInDraftStatusRule(Status));
        
        // Business logic continues...
    }
}
```

---

## 🔧 Developer Guidance

### Implementation Checklist

- [ ] Create DomainException base class with Error integration
- [ ] Implement BusinessRuleException with IBusinessRule support
- [ ] Implement ValidationException with FluentValidation compatibility
- [ ] Create specialized domain exceptions (4 types)
- [ ] Implement IBusinessRule interface with ToError method
- [ ] Add serialization support to all exceptions
- [ ] Create extension methods for Result conversion
- [ ] Add static helper methods for rule checking
- [ ] Implement property-level error access
- [ ] Add comprehensive XML documentation
- [ ] Create unit tests for all scenarios
- [ ] Create integration tests with FluentValidation

### Code Review Checklist

- [ ] All exceptions properly categorize errors
- [ ] Serialization works correctly
- [ ] FluentValidation integration verified
- [ ] Business rule pattern correctly implemented
- [ ] Exception.Data preserved
- [ ] Metadata flows through properly
- [ ] Thread safety maintained
- [ ] Documentation complete

---

## 📊 Test Scenarios

### Unit Tests Required

```csharp
[Fact]
public void DomainException_WithSingleError_InitializesCorrectly()
{
    var error = Error.BusinessRule("Test rule violated", "TEST_RULE");
    var exception = new DomainException(error);
    
    exception.Error.Should().Be(error);
    exception.Errors.Should().HaveCount(1);
    exception.Message.Should().Be(error.Message);
}

[Fact]
public void BusinessRuleException_CheckRule_ThrowsWhenBroken()
{
    var rule = new Mock<IBusinessRule>();
    rule.Setup(r => r.IsBroken()).Returns(true);
    rule.Setup(r => r.Code).Returns("RULE_BROKEN");
    rule.Setup(r => r.Message).Returns("Rule is broken");
    
    var act = () => BusinessRuleException.CheckRule(rule.Object);
    
    act.Should().Throw<BusinessRuleException>()
        .Which.BusinessRule.Should().Be(rule.Object);
}

[Fact]
public void ValidationException_WithFluentValidationFailures_MapsCorrectly()
{
    var failures = new[]
    {
        new ValidationFailure("Email", "Email is required"),
        new ValidationFailure("Email", "Email format is invalid"),
        new ValidationFailure("Age", "Age must be greater than 18")
    };
    
    var exception = new ValidationException(failures);
    
    exception.ValidationFailures.Should().HaveCount(3);
    exception.HasErrorsForProperty("Email").Should().BeTrue();
    exception.GetErrorsForProperty("Email").Should().HaveCount(2);
}

[Fact]
public void DomainException_Serialization_RoundTrip()
{
    var original = new DomainException(
        Error.Internal("Test error").WithMetadata("key", "value"));
    
    var serialized = SerializeToBytes(original);
    var deserialized = DeserializeFromBytes<DomainException>(serialized);
    
    deserialized.Error.Code.Should().Be(original.Error.Code);
    deserialized.Error.Metadata.Should().ContainKey("key");
}
```

---

## 🚀 Definition of Done

- [ ] **Code Complete**
  - [ ] DomainException base class implemented
  - [ ] BusinessRuleException implemented
  - [ ] ValidationException with FluentValidation support
  - [ ] All specialized exceptions implemented
  - [ ] IBusinessRule interface enhanced
  - [ ] Serialization support added
  - [ ] Extension methods created

- [ ] **Quality Assurance**
  - [ ] Unit test coverage > 95%
  - [ ] Integration tests with FluentValidation
  - [ ] Serialization tests pass
  - [ ] Code review completed
  - [ ] No compiler warnings

- [ ] **Documentation**
  - [ ] XML documentation complete
  - [ ] Usage examples provided
  - [ ] Exception handling guide created
  - [ ] Migration guide written

- [ ] **Integration Verified**
  - [ ] Error system integration works
  - [ ] FluentValidation compatibility confirmed
  - [ ] Result pattern conversion works
  - [ ] Business rule pattern validated

---

## 🎯 Success Metrics

- **Coverage:** > 95% unit test coverage
- **Integration:** FluentValidation fully compatible
- **Performance:** Minimal overhead for exception creation
- **Documentation:** All public APIs documented
- **Quality:** Zero defects in QA review

---

## 📝 Notes

- This story builds upon Story 01's Error system
- Critical for domain layer error handling consistency
- FluentValidation compatibility is essential for validation pipeline
- Business rule pattern enables declarative domain validation
- Consider adding more specialized exceptions as needed

---

**Story Status:** ✅ **COMPLETED - VERIFICATION PHASE**  
**Implementation:** Located in `src/BuildingBlocks/Core/Diagnostics/Exceptions/` and `src/BuildingBlocks/Application/Exceptions/`  
**Next Action:** Verify implementation completeness and FluentValidation integration