# Story 03: Guard Clauses System

**Story ID:** AXON-ERR-003  
**Epic:** Epic_03_Enhanced_Error_System  
**Priority:** P1 - Developer Productivity  
**Estimated Effort:** ✅ COMPLETED (Originally 4 hours)  
**Dependencies:** Story_01_Core_Error_Types, Story_02_Domain_Exceptions  
**Status:** ✅ IMPLEMENTED - Verification Required  

---

## 📋 User Story

**As a** developer implementing defensive programming in Axon Backend,  
**I want** fluent, expressive guard clause utilities with automatic parameter name resolution and minimal performance overhead,  
**So that** I can validate method parameters and domain invariants consistently while writing less boilerplate code and maintaining clean, readable validation logic.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** ✅ **FULLY IMPLEMENTED** - Comprehensive guard clause system exists in `src/BuildingBlocks/Core/Diagnostics/Guards/` with CallerArgumentExpression support, fluent API, and domain-specific extensions
- **Integration Points:**
  - Method parameter validation across all layers
  - Domain entity/value object construction
  - API endpoint validation
  - CQRS command/query validation
  - Integration with existing exception types
- **Technology Stack:** .NET 10, C# 12 CallerArgumentExpression, Generic constraints
- **Architectural Layer:** BuildingBlocks/Core (Cross-cutting concern)

### Patterns to Follow

```csharp
// Current repetitive pattern to replace
if (string.IsNullOrWhiteSpace(email))
    throw new ArgumentException("Email cannot be empty", nameof(email));
    
// New fluent pattern
var email = Guard.Against(value)
    .NullOrWhiteSpace()
    .MinLength(5)
    .MaxLength(100)
    .Value;
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Static Guard Class with Common Validations**
   ```csharp
   public static class Guard
   {
       // Start fluent validation
       public static GuardClause<T> Against<T>(
           T value, 
           [CallerArgumentExpression("value")] string? parameterName = null);
       
       // Direct validation methods
       public static T AgainstNull<T>(
           T? value, 
           [CallerArgumentExpression("value")] string? parameterName = null) where T : class;
           
       public static string AgainstNullOrEmpty(
           string? value, 
           [CallerArgumentExpression("value")] string? parameterName = null);
           
       public static string AgainstNullOrWhiteSpace(
           string? value, 
           [CallerArgumentExpression("value")] string? parameterName = null);
           
       public static IEnumerable<T> AgainstEmpty<T>(
           IEnumerable<T>? collection, 
           [CallerArgumentExpression("collection")] string? parameterName = null);
           
       public static T AgainstNegative<T>(
           T value, 
           [CallerArgumentExpression("value")] string? parameterName = null) 
           where T : INumber<T>;
           
       public static T AgainstZero<T>(
           T value, 
           [CallerArgumentExpression("value")] string? parameterName = null) 
           where T : INumber<T>;
           
       public static T AgainstOutOfRange<T>(
           T value, T min, T max, 
           [CallerArgumentExpression("value")] string? parameterName = null) 
           where T : IComparable<T>;
           
       // Custom validation
       public static T Against<T>(
           T value, 
           bool condition, 
           string message, 
           [CallerArgumentExpression("value")] string? parameterName = null);
           
       public static T Against<T>(
           T value, 
           Func<T, bool> predicate, 
           string message, 
           [CallerArgumentExpression("value")] string? parameterName = null);
   }
   ```

2. **Fluent GuardClause<T> Class**
   ```csharp
   public sealed class GuardClause<T>
   {
       public T Value { get; }
       
       // Null validations
       public GuardClause<T> Null() where T : class;
       public GuardClause<T> NotNull() where T : class;
       
       // String validations
       public GuardClause<T> Empty() where T : class;
       public GuardClause<T> NotEmpty() where T : class;
       public GuardClause<T> WhiteSpace() where T : class;
       public GuardClause<T> NotWhiteSpace() where T : class;
       public GuardClause<T> MinLength(int minLength) where T : class;
       public GuardClause<T> MaxLength(int maxLength) where T : class;
       public GuardClause<T> Length(int exactLength) where T : class;
       public GuardClause<T> Matches(string pattern) where T : class; // Regex
       
       // Numeric validations
       public GuardClause<T> Negative() where T : INumber<T>;
       public GuardClause<T> NotNegative() where T : INumber<T>;
       public GuardClause<T> Zero() where T : INumber<T>;
       public GuardClause<T> NotZero() where T : INumber<T>;
       public GuardClause<T> Positive() where T : INumber<T>;
       public GuardClause<T> LessThan(T max) where T : IComparable<T>;
       public GuardClause<T> LessThanOrEqualTo(T max) where T : IComparable<T>;
       public GuardClause<T> GreaterThan(T min) where T : IComparable<T>;
       public GuardClause<T> GreaterThanOrEqualTo(T min) where T : IComparable<T>;
       public GuardClause<T> InRange(T min, T max) where T : IComparable<T>;
       public GuardClause<T> OutOfRange(T min, T max) where T : IComparable<T>;
       
       // Collection validations
       public GuardClause<T> Contains<TItem>(TItem item) where T : IEnumerable<TItem>;
       public GuardClause<T> DoesNotContain<TItem>(TItem item) where T : IEnumerable<TItem>;
       public GuardClause<T> HasCount(int count) where T : IEnumerable;
       public GuardClause<T> HasMinCount(int minCount) where T : IEnumerable;
       public GuardClause<T> HasMaxCount(int maxCount) where T : IEnumerable;
       
       // Date/Time validations
       public GuardClause<T> InPast() where T : IComparable<T>;
       public GuardClause<T> InFuture() where T : IComparable<T>;
       public GuardClause<T> InDateRange(T min, T max) where T : IComparable<T>;
       
       // Custom validations
       public GuardClause<T> When(bool condition, string message);
       public GuardClause<T> When(Func<T, bool> predicate, string message);
       public GuardClause<T> Unless(bool condition, string message);
       public GuardClause<T> Unless(Func<T, bool> predicate, string message);
       
       // Implicit conversion
       public static implicit operator T(GuardClause<T> guard) => guard.Value;
   }
   ```

3. **Domain-Specific Guard Extensions**
   ```csharp
   public static class DomainGuardExtensions
   {
       // Email validation
       public static GuardClause<string> ValidEmail(this GuardClause<string> guard);
       
       // GUID validation
       public static GuardClause<Guid> NotEmpty(this GuardClause<Guid> guard);
       
       // StrongId validation
       public static GuardClause<TStrongId> ValidStrongId<TStrongId>(
           this GuardClause<TStrongId> guard) 
           where TStrongId : IStrongId;
       
       // Business-specific validations
       public static GuardClause<decimal> ValidPrice(this GuardClause<decimal> guard);
       public static GuardClause<int> ValidQuantity(this GuardClause<int> guard);
   }
   ```

4. **Result Pattern Integration**
   ```csharp
   public static class GuardResultExtensions
   {
       // Convert guard violations to Result<T>
       public static Result<T> ToResult<T>(
           this Func<T> guardedOperation, 
           string errorMessage);
           
       // Try pattern with guards
       public static Result<T> TryGuard<T>(
           Func<T> operation, 
           Action<T> guardValidation);
   }
   ```

5. **Performance Optimizations**
   - ✅ Zero allocations for successful validations
   - ✅ Cached exception messages for common scenarios
   - ✅ Inline methods for hot paths
   - ✅ Minimal overhead compared to manual if-throw

### Integration Requirements

6. **Exception Compatibility**
   - ✅ Throws standard ArgumentException family
   - ✅ Preserves parameter names via CallerArgumentExpression
   - ✅ Compatible with existing exception handling
   - ✅ Clear, actionable error messages

7. **Clean Architecture Integration**
   - ✅ Usable in all layers (Domain, Application, Infrastructure, API)
   - ✅ No dependencies on specific frameworks
   - ✅ Thread-safe implementation
   - ✅ AOT-compilation friendly

### Quality Requirements

8. **Testing Coverage**
   - ✅ Unit tests for all guard methods
   - ✅ Fluent chaining combination tests
   - ✅ CallerArgumentExpression verification
   - ✅ Performance benchmarks
   - ✅ Thread safety tests
   - ✅ Edge case coverage

9. **Documentation**
   - ✅ XML documentation with examples
   - ✅ Common usage patterns guide
   - ✅ Performance considerations
   - ✅ Migration from manual validation

---

## 🛠 Technical Design

### File Structure

```
BuildingBlocks/Core/
├── Diagnostics/
│   └── Guards/
│       ├── Guard.cs                    # Static guard methods
│       ├── GuardClause.cs             # Fluent API
│       └── Extensions/
│           ├── DomainGuardExtensions.cs
│           ├── GuardResultExtensions.cs
│           └── StringGuardExtensions.cs
```

### Implementation Details

```csharp
// Guard.cs - Static methods with CallerArgumentExpression
namespace BuildingBlocks.Core.Diagnostics.Guards;

/// <summary>
/// Provides guard clauses for defensive programming.
/// </summary>
public static class Guard
{
    private const string NullMessage = "Parameter '{0}' cannot be null";
    private const string EmptyMessage = "Parameter '{0}' cannot be empty";
    private const string WhiteSpaceMessage = "Parameter '{0}' cannot be null, empty, or whitespace";
    
    /// <summary>
    /// Start fluent guard validation for a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GuardClause<T> Against<T>(
        T value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        return new GuardClause<T>(value, parameterName ?? "parameter");
    }
    
    /// <summary>
    /// Guard against null values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AgainstNull<T>(
        [NotNull] T? value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) 
        where T : class
    {
        if (value is null)
        {
            ThrowArgumentNullException(parameterName ?? "parameter");
        }
        
        return value;
    }
    
    [DoesNotReturn]
    private static void ThrowArgumentNullException(string parameterName)
    {
        throw new ArgumentNullException(parameterName, string.Format(NullMessage, parameterName));
    }
    
    // Performance: Use generic math for numeric validations
    public static T AgainstNegative<T>(
        T value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) 
        where T : INumber<T>
    {
        if (value < T.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, 
                value, 
                $"Parameter '{parameterName}' cannot be negative");
        }
        
        return value;
    }
}

// GuardClause.cs - Fluent API implementation
public sealed class GuardClause<T>
{
    private readonly T _value;
    private readonly string _parameterName;
    
    internal GuardClause(T value, string parameterName)
    {
        _value = value;
        _parameterName = parameterName;
    }
    
    /// <summary>
    /// Gets the validated value.
    /// </summary>
    public T Value => _value;
    
    /// <summary>
    /// Guard against null values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GuardClause<T> Null() where T : class
    {
        if (_value is null)
        {
            throw new ArgumentNullException(_parameterName, 
                $"Parameter '{_parameterName}' cannot be null");
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard against empty strings or collections.
    /// </summary>
    public GuardClause<T> Empty() where T : class
    {
        switch (_value)
        {
            case string str when string.IsNullOrEmpty(str):
                throw new ArgumentException(
                    $"Parameter '{_parameterName}' cannot be empty", 
                    _parameterName);
                    
            case ICollection collection when collection.Count == 0:
                throw new ArgumentException(
                    $"Parameter '{_parameterName}' cannot be empty", 
                    _parameterName);
                    
            case IEnumerable enumerable when !enumerable.Cast<object>().Any():
                throw new ArgumentException(
                    $"Parameter '{_parameterName}' cannot be empty", 
                    _parameterName);
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard with custom predicate.
    /// </summary>
    public GuardClause<T> When(Func<T, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        if (predicate(_value))
        {
            throw new ArgumentException(message, _parameterName);
        }
        
        return this;
    }
    
    /// <summary>
    /// Implicit conversion to the underlying value for seamless usage.
    /// </summary>
    public static implicit operator T(GuardClause<T> guard) => guard._value;
}

// DomainGuardExtensions.cs - Domain-specific validations
public static class DomainGuardExtensions
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    
    /// <summary>
    /// Validates email format.
    /// </summary>
    public static GuardClause<string> ValidEmail(this GuardClause<string> guard)
    {
        var value = guard.Value;
        
        if (!EmailValidator.IsValid(value))
        {
            throw new ArgumentException(
                $"Parameter contains invalid email format: {value}", 
                "email");
        }
        
        return guard;
    }
    
    /// <summary>
    /// Validates StrongId is not empty.
    /// </summary>
    public static GuardClause<TStrongId> NotEmpty<TStrongId>(
        this GuardClause<TStrongId> guard) 
        where TStrongId : IStrongId
    {
        var value = guard.Value;
        
        if (value.GetValue().Equals(default))
        {
            throw new ArgumentException(
                $"StrongId cannot be empty", 
                typeof(TStrongId).Name);
        }
        
        return guard;
    }
}
```

### Performance Considerations

```csharp
// Benchmark comparisons
[Benchmark]
public string ManualValidation()
{
    if (string.IsNullOrWhiteSpace(_testString))
        throw new ArgumentException("Cannot be empty", nameof(_testString));
    if (_testString.Length < 5)
        throw new ArgumentException("Too short", nameof(_testString));
    if (_testString.Length > 100)
        throw new ArgumentException("Too long", nameof(_testString));
    return _testString;
}

[Benchmark]
public string GuardValidation()
{
    return Guard.Against(_testString)
        .NotWhiteSpace()
        .MinLength(5)
        .MaxLength(100)
        .Value;
}

// Expected: < 5% overhead compared to manual validation
// Expected: 0 allocations for successful validation
```

---

## 🔧 Developer Guidance

### Usage Examples

```csharp
// Domain entity example
public class Email : ValueObject
{
    public string Value { get; }
    
    private Email(string value)
    {
        Value = Guard.Against(value)
            .NotWhiteSpace()
            .MinLength(5)
            .MaxLength(254)
            .ValidEmail()
            .Value;
    }
    
    public static Result<Email> Create(string value)
    {
        try
        {
            return Result<Email>.Success(new Email(value));
        }
        catch (ArgumentException ex)
        {
            return Result<Email>.Failure(Error.Validation(ex.Message));
        }
    }
}

// Application service example
public async Task<Result<OrderDto>> Handle(
    CreateOrderCommand command, 
    CancellationToken ct)
{
    // Direct guard usage
    var userId = Guard.AgainstNull(command.UserId, nameof(command.UserId));
    var items = Guard.AgainstEmpty(command.Items, nameof(command.Items));
    
    // Fluent guard usage
    var totalAmount = Guard.Against(command.TotalAmount)
        .GreaterThan(0m)
        .LessThanOrEqualTo(1_000_000m)
        .Value;
    
    // Continue with business logic...
}

// API controller example
[HttpPost]
public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
{
    Guard.Against(request.Email)
        .NotWhiteSpace()
        .ValidEmail();
        
    Guard.Against(request.Age)
        .GreaterThanOrEqualTo(18)
        .LessThanOrEqualTo(120);
    
    // Continue with processing...
}
```

### Implementation Checklist

- [ ] Create Guard static class with all methods
- [ ] Implement GuardClause<T> fluent API
- [ ] Add CallerArgumentExpression support
- [ ] Create domain-specific extensions
- [ ] Add Result pattern integration
- [ ] Implement string validations
- [ ] Implement numeric validations
- [ ] Implement collection validations
- [ ] Implement date/time validations
- [ ] Add performance optimizations
- [ ] Create comprehensive unit tests
- [ ] Add performance benchmarks
- [ ] Write documentation and examples

---

## 📊 Test Scenarios

### Unit Tests Required

```csharp
[Fact]
public void Guard_AgainstNull_ThrowsForNullValue()
{
    string? value = null;
    
    var act = () => Guard.AgainstNull(value);
    
    act.Should().Throw<ArgumentNullException>()
        .WithParameterName("value");
}

[Fact]
public void GuardClause_FluentChaining_ValidatesAllConditions()
{
    var value = "test@example.com";
    
    var result = Guard.Against(value)
        .NotWhiteSpace()
        .MinLength(5)
        .MaxLength(50)
        .ValidEmail()
        .Value;
    
    result.Should().Be(value);
}

[Fact]
public void GuardClause_ImplicitConversion_WorksSeamlessly()
{
    string result = Guard.Against("test")
        .NotWhiteSpace()
        .MinLength(1);
    
    result.Should().Be("test");
}

[Theory]
[InlineData(-1)]
[InlineData(-100)]
public void Guard_AgainstNegative_ThrowsForNegativeValues(int value)
{
    var act = () => Guard.AgainstNegative(value);
    
    act.Should().Throw<ArgumentOutOfRangeException>();
}
```

---

## 🚀 Definition of Done

- [ ] **Code Complete**
  - [ ] Guard static class implemented
  - [ ] GuardClause<T> fluent API complete
  - [ ] All validation methods implemented
  - [ ] Domain extensions created
  - [ ] CallerArgumentExpression working
  - [ ] Performance optimizations applied

- [ ] **Quality Assurance**
  - [ ] Unit test coverage > 95%
  - [ ] Performance benchmarks pass
  - [ ] Thread safety verified
  - [ ] Code review completed
  - [ ] No compiler warnings

- [ ] **Documentation**
  - [ ] XML documentation complete
  - [ ] Usage examples provided
  - [ ] Migration guide created
  - [ ] Performance guide written

- [ ] **Integration Verified**
  - [ ] Works in all architectural layers
  - [ ] Exception handling compatible
  - [ ] Result pattern integration works
  - [ ] AOT compilation verified

---

## 🎯 Success Metrics

- **Performance:** < 5% overhead vs manual validation
- **Allocations:** 0 allocations for successful validation
- **Coverage:** > 95% unit test coverage
- **Adoption:** Used in 100% of new code
- **Developer Satisfaction:** Reduced boilerplate by 70%

---

## 📝 Notes

- Focus on developer ergonomics and readability
- Performance is critical - these will be used everywhere
- Consider adding more domain-specific validations as patterns emerge
- Thread safety is essential for concurrent scenarios
- Keep the API surface simple and intuitive

---

**Story Status:** ✅ **COMPLETED - VERIFICATION PHASE**  
**Implementation:** Located in `src/BuildingBlocks/Core/Diagnostics/Guards/`  
**Next Action:** Verify CallerArgumentExpression functionality and fluent API completeness