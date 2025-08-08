# Story 01: Core Error Types and Categorization

**Story ID:** AXON-ERR-001  
**Epic:** Epic_03_Enhanced_Error_System  
**Priority:** P0 - Critical Foundation  
**Estimated Effort:** 6 hours  
**Dependencies:** None (Foundation Story)  
**Target Sprint:** Current  

---

## 📋 User Story

**As a** backend developer working with the Axon Backend,  
**I want** comprehensive error categorization with rich metadata support and full observability integration,  
**So that** I can create consistent, well-structured, and traceable errors across all application layers while maintaining zero-allocation performance for common scenarios.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** Basic Error record exists in `BuildingBlocks/Core/Functional/Error.cs` with simple factory methods
- **Integration Points:**
  - Existing `Result<T>` pattern in functional foundation
  - Current pipeline behaviors (MediatR)
  - Domain layer error handling
  - API response generation
- **Technology Stack:** .NET 10, C# 12, Immutable records, Factory pattern
- **Architectural Layer:** BuildingBlocks/Core (Foundation Layer)

### Patterns to Follow

```csharp
// Existing pattern from Result<T>
public readonly record struct Result<T>
{
    private Result(T value) { /* ... */ }
    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Comprehensive ErrorType Enumeration**
   - ✅ Create `ErrorType` enum with 16+ categorized error types
   - ✅ Each type maps to specific HTTP status codes
   - ✅ Categories cover: Validation, NotFound, Conflict, BusinessRule, Unauthorized, Forbidden, Internal, External, Timeout, Cancelled, RateLimit, Persistence, Aggregate, Configuration, Network, Security
   - ✅ XML documentation for each type with usage scenarios

2. **Error Severity Levels**
   - ✅ Create `ErrorSeverity` enum: Info, Warning, Error, Critical, Fatal
   - ✅ Each severity level includes alerting thresholds
   - ✅ Integration with logging infrastructure levels
   - ✅ Default severity mappings per ErrorType

3. **Enhanced Error Record**
   ```csharp
   public sealed record Error
   {
       public string Code { get; }
       public string Message { get; }
       public ErrorType Type { get; }
       public ErrorSeverity Severity { get; }
       public Exception? InnerException { get; }
       public IReadOnlyDictionary<string, object>? Metadata { get; }
       public string? StackTrace { get; } // DEBUG only
       public DateTime OccurredAt { get; }
       public string? CorrelationId { get; }
       public string? Source { get; }
   }
   ```

4. **Factory Method Implementation**
   - ✅ Maintain ALL existing factory methods for backward compatibility
   - ✅ Add new categorized factory methods:
     - `Error.Validation(message, code?, metadata?)`
     - `Error.NotFound(message, code?, metadata?)`
     - `Error.Conflict(message, code?, metadata?)`
     - `Error.BusinessRule(message, code?, metadata?)`
     - `Error.Unauthorized(message?, code?, metadata?)`
     - `Error.Forbidden(message?, code?, metadata?)`
     - `Error.Internal(message, code?, exception?, metadata?)`
     - `Error.External(message, code?, exception?, metadata?)`
     - `Error.Timeout(message?, code?, timeout?, metadata?)`
     - `Error.Cancelled(message?, code?, metadata?)`
     - `Error.RateLimit(message?, code?, retryAfter?, metadata?)`
     - `Error.Persistence(message, code?, exception?, metadata?)`
     - `Error.Configuration(message, code?, configKey?, metadata?)`
     - `Error.Network(message, code?, exception?, metadata?)`
     - `Error.Security(message, code?, metadata?)`
   - ✅ Smart `Error.FromException(Exception ex)` with automatic categorization

5. **Builder Methods**
   - ✅ `WithMetadata(string key, object value)` - Add single metadata entry
   - ✅ `WithMetadata(IReadOnlyDictionary<string, object> metadata)` - Add multiple entries
   - ✅ `WithCorrelationId(string correlationId)` - Add tracing correlation
   - ✅ `WithSeverity(ErrorSeverity severity)` - Override default severity
   - ✅ `WithSource(string source)` - Add source component/layer
   - ✅ `WithInnerException(Exception exception)` - Attach exception

6. **Conversion Methods**
   - ✅ `ToHttpStatusCode()` - Convert ErrorType to HTTP status code
   - ✅ `ToProblemDetails()` - Convert to RFC 7807 Problem Details
   - ✅ `ToLogData()` - Convert to structured logging dictionary
   - ✅ `ToString()` - Human-readable representation

### Integration Requirements

7. **Backward Compatibility**
   - ✅ All existing Error usage continues to work unchanged
   - ✅ Existing Result<T>.Failure(Error) patterns work seamlessly
   - ✅ No breaking changes to public APIs

8. **Performance Requirements**
   - ✅ Zero allocations for common error creation paths
   - ✅ Metadata dictionary lazy initialization
   - ✅ StackTrace capture only in DEBUG builds
   - ✅ String intern for common error codes

9. **Observability Integration**
   - ✅ OpenTelemetry Activity integration support
   - ✅ Structured logging format compatibility
   - ✅ Metrics collection support (counters per ErrorType)
   - ✅ Correlation ID propagation

### Quality Requirements

10. **Testing Coverage**
    - ✅ Unit tests for all factory methods
    - ✅ Unit tests for all builder methods
    - ✅ Unit tests for all conversion methods
    - ✅ Exception categorization test matrix
    - ✅ Performance benchmarks for error creation
    - ✅ Thread safety verification tests

11. **Documentation**
    - ✅ XML documentation for all public APIs
    - ✅ Usage examples in XML comments
    - ✅ Update architecture documentation
    - ✅ Migration guide from old Error pattern

---

## 🛠 Technical Design

### File Structure

```
BuildingBlocks/Core/
├── Diagnostics/
│   └── Errors/
│       ├── ErrorType.cs          # Error categorization enum
│       ├── ErrorSeverity.cs      # Severity levels enum
│       └── Error.cs              # Enhanced Error record
└── Functional/
    └── Error.cs                  # DEPRECATED - Redirect to new location
```

### Implementation Details

```csharp
// ErrorType.cs
namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Error type categorization for routing, handling, and observability.
/// Each type maps to specific HTTP status codes and handling strategies.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Input validation failures (400 Bad Request)
    /// Use for: Invalid input format, missing required fields, constraint violations
    /// </summary>
    Validation = 1,
    
    // ... (all 16+ types with detailed documentation)
}

// Error.cs - Key implementation details
public sealed record Error
{
    // Use init-only properties for immutability
    // Lazy-initialize collections for performance
    private readonly Lazy<Dictionary<string, object>> _metadata;
    
    // Smart factory with defaults
    public static Error Validation(
        string message,
        string code = "VALIDATION_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(
            code: code,
            message: message,
            type: ErrorType.Validation,
            severity: ErrorSeverity.Warning, // Default severity
            metadata: metadata);
    }
    
    // Exception categorization logic
    public static Error FromException(Exception exception)
    {
        return exception switch
        {
            ArgumentException => Validation(/*...*/),
            ArgumentNullException => Validation(/*...*/),
            InvalidOperationException => BusinessRule(/*...*/),
            UnauthorizedAccessException => Unauthorized(/*...*/),
            TimeoutException => Timeout(/*...*/),
            OperationCanceledException => Cancelled(/*...*/),
            HttpRequestException => External(/*...*/),
            DbUpdateException => Persistence(/*...*/),
            ConfigurationException => Configuration(/*...*/),
            SocketException => Network(/*...*/),
            SecurityException => Security(/*...*/),
            _ => Internal(/*...*/)
        };
    }
}
```

### Performance Optimizations

```csharp
// String interning for common codes
private static readonly ConcurrentDictionary<string, string> InternedCodes = new();

private static string InternCode(string code)
{
    return InternedCodes.GetOrAdd(code, c => string.Intern(c));
}

// Metadata lazy initialization
private Dictionary<string, object> GetOrCreateMetadata()
{
    return _metadata.Value ??= new Dictionary<string, object>();
}

// Object pooling for metadata dictionaries (Story 6 will enhance)
// Placeholder for future ErrorPool integration
```

---

## 🔧 Developer Guidance

### Implementation Checklist

- [ ] Create ErrorType.cs with all 16+ error types
- [ ] Create ErrorSeverity.cs with 5 severity levels
- [ ] Move existing Error.cs to new location (maintain redirect)
- [ ] Implement all factory methods with smart defaults
- [ ] Implement all builder methods with immutability
- [ ] Implement conversion methods (HTTP, ProblemDetails, Logging)
- [ ] Add DEBUG-only StackTrace capture
- [ ] Implement string interning for codes
- [ ] Add comprehensive XML documentation
- [ ] Create unit tests (minimum 95% coverage)
- [ ] Create performance benchmarks
- [ ] Update existing Error usages to new location
- [ ] Create migration documentation

### Code Review Checklist

- [ ] Immutability maintained throughout
- [ ] Factory methods follow established patterns
- [ ] Zero allocations for common paths verified
- [ ] Thread safety guaranteed
- [ ] XML documentation complete and helpful
- [ ] Backward compatibility verified
- [ ] Performance benchmarks pass
- [ ] Integration with Result<T> tested

---

## 📊 Test Scenarios

### Unit Tests Required

```csharp
[Fact]
public void Error_Validation_CreatesCorrectType()
{
    var error = Error.Validation("Invalid email");
    
    error.Type.Should().Be(ErrorType.Validation);
    error.Severity.Should().Be(ErrorSeverity.Warning);
    error.ToHttpStatusCode().Should().Be(400);
}

[Fact]
public void Error_FromException_CategorizesCorrectly()
{
    var ex = new ArgumentNullException("param");
    var error = Error.FromException(ex);
    
    error.Type.Should().Be(ErrorType.Validation);
    error.InnerException.Should().Be(ex);
}

[Fact]
public void Error_WithMetadata_CreatesNewInstance()
{
    var error1 = Error.Internal("Error");
    var error2 = error1.WithMetadata("key", "value");
    
    error1.Should().NotBeSameAs(error2);
    error2.Metadata.Should().ContainKey("key");
}
```

### Performance Benchmarks

```csharp
[Benchmark]
public Error CreateValidationError() => Error.Validation("Test message");

[Benchmark]
public Error CreateErrorWithMetadata() => 
    Error.Internal("Test").WithMetadata("key", "value");

// Expected: 0 allocations for simple creation
// Expected: < 100ns for factory methods
```

---

## 🚀 Definition of Done

- [ ] **Code Complete**
  - [ ] All ErrorType enum values implemented
  - [ ] All ErrorSeverity enum values implemented
  - [ ] Enhanced Error record with all properties
  - [ ] All 15+ factory methods implemented
  - [ ] All builder methods implemented
  - [ ] All conversion methods implemented

- [ ] **Quality Assurance**
  - [ ] Unit test coverage > 95%
  - [ ] Performance benchmarks pass (0 allocations)
  - [ ] Thread safety tests pass
  - [ ] Code review completed
  - [ ] No compiler warnings

- [ ] **Documentation**
  - [ ] XML documentation complete
  - [ ] Architecture docs updated
  - [ ] Migration guide created
  - [ ] Usage examples provided

- [ ] **Integration Verified**
  - [ ] Existing Error usage works unchanged
  - [ ] Result<T> integration verified
  - [ ] Pipeline behaviors compatibility confirmed
  - [ ] No breaking changes detected

---

## 🎯 Success Metrics

- **Performance:** 0 allocations for common error creation
- **Coverage:** > 95% unit test coverage
- **Compatibility:** 100% backward compatible
- **Documentation:** All public APIs documented
- **Quality:** Zero defects in QA review

---

## 📝 Notes

- This is the foundation story for the entire error system enhancement
- Subsequent stories will build upon these core types
- Performance is critical - this will be used throughout the application
- Maintain immutability and thread safety at all times
- Consider future extensibility in design decisions

---

**Story Status:** Ready for Development  
**Assigned To:** [Developer Name]  
**Review By:** Quinn (QA Architect)