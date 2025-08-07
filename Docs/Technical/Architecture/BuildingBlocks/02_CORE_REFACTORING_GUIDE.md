# 🏗️ BuildingBlocks Core - Complete Refactoring Implementation Guide

**Version:** 1.0 - Brutal Refactoring Edition  
**Scope:** `/src/BuildingBlocks/Core` folder complete replacement  
**Approach:** Zero backward compatibility, complete functional transformation  
**Target:** .NET 10, Railway-Oriented Programming, Tactical DDD (No Event Sourcing in MVP)

---

## 📋 Executive Summary

This guide provides the **exact implementation blueprint** for refactoring the BuildingBlocks/Core folder from its current state to a production-ready functional architecture. Following the PRD v3.1 requirements, we focus on:

- ✅ **Complete Result<T> Pattern** - Full monad implementation with railway operations
- ✅ **Option<T> Monad** - Zero null references approach
- ✅ **Traditional Aggregates** - Rich domain models WITHOUT event sourcing (MVP focus)
- ✅ **Tactical DDD** - Value objects, specifications, domain services
- ✅ **CQRS Foundation** - Command/Query segregation with proper abstractions

---

## 🚨 Current State Analysis

### Existing Structure (To Be Replaced)
```
Core/
├── Abstractions/       # Basic CQRS, Events, Pagination
├── Constants/          # Simple constants
├── Diagnostics/        # Error definitions
├── Domain/            # Basic aggregates and entities
├── Functional/        # Incomplete Result<T> implementation
└── Utils/             # Utility classes
```

### Critical Gaps Identified
1. **Result<T> Pattern**: Missing async operations, applicative functors, error recovery
2. **Option<T> Monad**: Completely absent - using nullables instead
3. **Domain Models**: Anemic models without proper encapsulation
4. **Value Objects**: Missing or incorrectly implemented
5. **Specifications**: No specification pattern implementation
6. **Business Rules**: No formal business rule validation system

---

## 🎯 Target Architecture

### New Core Structure
```
Core/
├── Functional/           # Complete functional programming foundation
│   ├── Results/         # Result<T>, Error, Extensions
│   ├── Options/         # Option<T> monad implementation
│   ├── Either/          # Either<TLeft, TRight> (future)
│   └── Validation/      # Validation<T> for error accumulation
├── Domain/              # Rich domain layer
│   ├── Primitives/      # Base types and interfaces
│   ├── Model/           # Aggregates, Entities, Value Objects
│   ├── Events/          # Domain events (no event sourcing)
│   ├── Rules/           # Business rule engine
│   ├── Specifications/  # Specification pattern
│   └── Services/        # Domain service interfaces
├── Abstractions/        # Core abstractions
│   ├── CQRS/           # Command/Query interfaces
│   ├── Messaging/       # Integration events
│   └── Pagination/      # Pagination contracts
└── Diagnostics/         # Error handling and diagnostics
    ├── Errors/          # Error definitions
    └── Guards/          # Guard clauses
```

---

## 📦 Implementation Roadmap

## Phase 1: Functional Foundation (Priority 1)

### 1.1 Complete Result<T> Implementation

**File:** `Core/Functional/Results/Result.cs`

```csharp
namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Complete Result monad for railway-oriented programming.
/// Thread-safe, immutable, and performance-optimized.
/// </summary>
public readonly record struct Result<T> : IResult<T>
{
    private readonly T? _value;
    private readonly Error? _error;
    private readonly ResultState _state;
    
    // Performance: Use `record struct` for payloads < 64 B; otherwise switch to `record` (class) to avoid boxing.
    
    #region Core Operations
    
    /// <summary>
    /// Pattern matching with exhaustive checking
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> success,
        Func<Error, TResult> failure) => _state switch
    {
        ResultState.Success => success(_value!),
        ResultState.Failure => failure(_error!),
        _ => throw new InvalidOperationException($"Invalid state: {_state}")
    };
    
    /// <summary>
    /// Async pattern matching with cancellation support
    /// </summary>
    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> success,
        Func<Error, Task<TResult>> failure,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        
        return _state switch
        {
            ResultState.Success => await success(_value!).ConfigureAwait(false),
            ResultState.Failure => await failure(_error!).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Invalid state: {_state}")
        };
    }
    
    #endregion
    
    #region Functor Operations (Map)
    
    /// <summary>
    /// Transforms the success value while preserving the error
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess 
            ? Result<TNew>.Success(mapper(_value!)) 
            : Result<TNew>.Failure(_error!);
    }
    
    /// <summary>
    /// Async map with proper exception handling
    /// </summary>
    public async Task<Result<TNew>> MapAsync<TNew>(
        Func<T, Task<TNew>> mapper,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        
        if (!IsSuccess)
            return Result<TNew>.Failure(_error!);
            
        try
        {
            var result = await mapper(_value!).ConfigureAwait(false);
            return Result<TNew>.Success(result);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure(Error.Cancelled());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(Error.FromException(ex));
        }
    }
    
    // ValueTask variant for high-performance scenarios
    public async ValueTask<Result<TNew>> MapValueTaskAsync<TNew>(
        Func<T, ValueTask<TNew>> mapper,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        
        if (!IsSuccess)
            return Result<TNew>.Failure(_error!);
            
        try
        {
            var result = await mapper(_value!).ConfigureAwait(false);
            return Result<TNew>.Success(result);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure(Error.Cancelled());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(Error.FromException(ex));
        }
    }
    
    #endregion
    
    #region Monad Operations (Bind/FlatMap)
    
    /// <summary>
    /// Chains operations that return Result<T>
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder(_value!) : Result<TNew>.Failure(_error!);
    }
    
    /// <summary>
    /// Async bind with cancellation and exception handling
    /// </summary>
    public async Task<Result<TNew>> BindAsync<TNew>(
        Func<T, Task<Result<TNew>>> binder,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binder);
        
        if (!IsSuccess)
            return Result<TNew>.Failure(_error!);
            
        try
        {
            return await binder(_value!).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure(Error.Cancelled());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(Error.FromException(ex));
        }
    }
    
    #endregion
    
    #region Applicative Operations
    
    /// <summary>
    /// Applies a wrapped function to a wrapped value
    /// </summary>
    public Result<TResult> Apply<TResult>(Result<Func<T, TResult>> fn)
    {
        if (fn.IsFailure)
            return Result<TResult>.Failure(fn._error!);
            
        if (IsFailure)
            return Result<TResult>.Failure(_error!);
            
        return Result<TResult>.Success(fn._value!(_value!));
    }
    
    #endregion
    
    #region Side Effects
    
    /// <summary>
    /// Executes side effect without breaking the chain
    /// </summary>
    public Result<T> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (IsSuccess)
            action(_value!);
            
        return this;
    }
    
    /// <summary>
    /// Async tap for side effects
    /// </summary>
    public async Task<Result<T>> TapAsync(
        Func<T, Task> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (IsSuccess)
            await action(_value!).ConfigureAwait(false);
            
        return this;
    }
    
    /// <summary>
    /// Execute action on failure
    /// </summary>
    public Result<T> TapError(Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (IsFailure)
            action(_error!);
            
        return this;
    }
    
    #endregion
    
    #region Error Recovery
    
    /// <summary>
    /// Recover from failure with a fallback value
    /// </summary>
    public Result<T> Recover(Func<Error, T> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return IsFailure 
            ? Result<T>.Success(recovery(_error!)) 
            : this;
    }
    
    /// <summary>
    /// Recover with another Result
    /// </summary>
    public Result<T> RecoverWith(Func<Error, Result<T>> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return IsFailure ? recovery(_error!) : this;
    }
    
    /// <summary>
    /// Provide default value on failure
    /// </summary>
    public T GetOrElse(T defaultValue)
    {
        return IsSuccess ? _value! : defaultValue;
    }
    
    /// <summary>
    /// Provide default value from factory on failure
    /// </summary>
    public T GetOrElse(Func<Error, T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return IsSuccess ? _value! : defaultFactory(_error!);
    }
    
    #endregion
    
    #region Combinators
    
    /// <summary>
    /// Combine two results into a tuple
    /// </summary>
    public static Result<(T1, T2)> Combine<T1, T2>(
        Result<T1> r1, 
        Result<T2> r2)
    {
        if (r1.IsFailure)
            return Result<(T1, T2)>.Failure(r1._error!);
            
        if (r2.IsFailure)
            return Result<(T1, T2)>.Failure(r2._error!);
            
        return Result<(T1, T2)>.Success((r1._value!, r2._value!));
    }
    
    /// <summary>
    /// Combine multiple results, collecting all errors
    /// </summary>
    public static Result<T[]> Sequence(params Result<T>[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToList();
        
        if (failures.Any())
        {
            // Aggregate errors for better diagnostics
            var aggregatedError = Error.Aggregate(
                failures.Select(f => f._error!).ToArray());
            return Result<T[]>.Failure(aggregatedError);
        }
        
        var values = results.Select(r => r._value!).ToArray();
        return Result<T[]>.Success(values);
    }
    
    /// <summary>
    /// Traverse a collection with a Result-returning function
    /// </summary>
    public static async Task<Result<TResult[]>> Traverse<TSource, TResult>(
        IEnumerable<TSource> source,
        Func<TSource, Task<Result<TResult>>> selector)
    {
        var results = new List<Result<TResult>>();
        
        foreach (var item in source)
        {
            var result = await selector(item).ConfigureAwait(false);
            results.Add(result);
        }
        
        return Sequence(results.ToArray());
    }
    
    #endregion
    
    #region Filters and Predicates
    
    /// <summary>
    /// Filter success value based on predicate
    /// </summary>
    public Result<T> Filter(
        Func<T, bool> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        
        if (IsFailure)
            return this;
            
        return predicate(_value!) ? this : Result<T>.Failure(error);
    }
    
    /// <summary>
    /// Ensure a condition is met
    /// </summary>
    public Result<T> Ensure(
        Func<T, bool> predicate,
        Func<T, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        
        if (IsFailure)
            return this;
            
        return predicate(_value!) 
            ? this 
            : Result<T>.Failure(errorFactory(_value!));
    }
    
    #endregion
}

/// <summary>
/// Result state enumeration for pattern matching
/// </summary>
internal enum ResultState : byte
{
    Success = 1,
    Failure = 2
}
```

### 1.2 Option<T> Monad Implementation

**File:** `Core/Functional/Options/Option.cs`

```csharp
namespace BuildingBlocks.Core.Functional.Options;

/// <summary>
/// Option monad for explicit nullable handling.
/// Eliminates null reference exceptions through type safety.
/// </summary>
public readonly record struct Option<T> : IOption<T>
{
    private readonly T? _value;
    private readonly bool _hasValue;
    
    private Option(T? value, bool hasValue)
    {
        _value = value;
        _hasValue = hasValue;
    }
    
    #region Factory Methods
    
    /// <summary>
    /// Creates an Option with a value
    /// </summary>
    public static Option<T> Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Option<T>(value, true);
    }
    
    /// <summary>
    /// Creates an empty Option
    /// </summary>
    public static Option<T> None() => new(default, false);
    
    /// <summary>
    /// Creates an Option from a nullable value
    /// </summary>
    public static Option<T> From(T? value)
    {
        return value is null ? None() : Some(value);
    }
    
    /// <summary>
    /// Conditionally creates an Option
    /// </summary>
    public static Option<T> When(bool condition, T value)
    {
        return condition ? Some(value) : None();
    }
    
    /// <summary>
    /// Conditionally creates an Option with factory
    /// </summary>
    public static Option<T> When(bool condition, Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return condition ? Some(factory()) : None();
    }
    
    #endregion
    
    #region Properties
    
    public bool IsSome => _hasValue;
    public bool IsNone => !_hasValue;
    
    /// <summary>
    /// Gets the value or throws if None
    /// </summary>
    public T Value => _hasValue 
        ? _value! 
        : throw new InvalidOperationException("Cannot access value of None");
    
    #endregion
    
    #region Pattern Matching
    
    /// <summary>
    /// Pattern match with Some and None cases
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> some,
        Func<TResult> none)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);
        
        return _hasValue ? some(_value!) : none();
    }
    
    /// <summary>
    /// Async pattern matching
    /// </summary>
    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> some,
        Func<Task<TResult>> none,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);
        
        ct.ThrowIfCancellationRequested();
        
        return _hasValue 
            ? await some(_value!).ConfigureAwait(false)
            : await none().ConfigureAwait(false);
    }
    
    #endregion
    
    #region Functor Operations
    
    /// <summary>
    /// Map the value if present
    /// </summary>
    public Option<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _hasValue 
            ? Option<TNew>.Some(mapper(_value!)) 
            : Option<TNew>.None();
    }
    
    /// <summary>
    /// Async map operation
    /// </summary>
    public async Task<Option<TNew>> MapAsync<TNew>(
        Func<T, Task<TNew>> mapper,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        
        if (!_hasValue)
            return Option<TNew>.None();
            
        var result = await mapper(_value!).ConfigureAwait(false);
        return Option<TNew>.Some(result);
    }
    
    #endregion
    
    #region Monad Operations
    
    /// <summary>
    /// Bind/FlatMap operation
    /// </summary>
    public Option<TNew> Bind<TNew>(Func<T, Option<TNew>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _hasValue ? binder(_value!) : Option<TNew>.None();
    }
    
    /// <summary>
    /// Async bind operation
    /// </summary>
    public async Task<Option<TNew>> BindAsync<TNew>(
        Func<T, Task<Option<TNew>>> binder,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binder);
        
        if (!_hasValue)
            return Option<TNew>.None();
            
        return await binder(_value!).ConfigureAwait(false);
    }
    
    #endregion
    
    #region Filters and Defaults
    
    /// <summary>
    /// Filter based on predicate
    /// </summary>
    public Option<T> Filter(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        if (!_hasValue)
            return this;
            
        return predicate(_value!) ? this : None();
    }
    
    /// <summary>
    /// Get value or default
    /// </summary>
    public T GetOrElse(T defaultValue)
    {
        return _hasValue ? _value! : defaultValue;
    }
    
    /// <summary>
    /// Get value or default from factory
    /// </summary>
    public T GetOrElse(Func<T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return _hasValue ? _value! : defaultFactory();
    }
    
    /// <summary>
    /// Get value or throw custom exception
    /// </summary>
    public T GetOrThrow(Func<Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);
        return _hasValue ? _value! : throw exceptionFactory();
    }
    
    #endregion
    
    #region Conversions
    
    /// <summary>
    /// Convert to Result
    /// </summary>
    public Result<T> ToResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return _hasValue 
            ? Result<T>.Success(_value!) 
            : Result<T>.Failure(error);
    }
    
    /// <summary>
    /// Convert to nullable
    /// </summary>
    public T? ToNullable()
    {
        return _hasValue ? _value : default;
    }
    
    /// <summary>
    /// Convert to array (0 or 1 element)
    /// </summary>
    public T[] ToArray()
    {
        return _hasValue ? new[] { _value! } : Array.Empty<T>();
    }
    
    /// <summary>
    /// Convert to list (0 or 1 element)
    /// </summary>
    public List<T> ToList()
    {
        return _hasValue ? new List<T> { _value! } : new List<T>();
    }
    
    #endregion
    
    #region Side Effects
    
    /// <summary>
    /// Execute action if Some
    /// </summary>
    public Option<T> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (_hasValue)
            action(_value!);
            
        return this;
    }
    
    /// <summary>
    /// Execute action if None
    /// </summary>
    public Option<T> TapNone(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (!_hasValue)
            action();
            
        return this;
    }
    
    #endregion
    
    #region Operators
    
    public static implicit operator Option<T>(T? value)
    {
        return From(value);
    }
    
    public static bool operator true(Option<T> option)
    {
        return option._hasValue;
    }
    
    public static bool operator false(Option<T> option)
    {
        return !option._hasValue;
    }
    
    #endregion
}
```

### 1.3 Validation<T> for Error Accumulation

**File:** `Core/Functional/Validation/Validation.cs`

```csharp
namespace BuildingBlocks.Core.Functional.Validation;

/// <summary>
/// Validation applicative for accumulating errors.
/// Unlike Result, Validation collects all errors instead of short-circuiting.
/// </summary>
public readonly record struct Validation<T>
{
    private readonly T? _value;
    private readonly List<Error> _errors;
    
    private Validation(T? value, List<Error> errors)
    {
        _value = value;
        _errors = errors ?? new List<Error>();
    }
    
    public bool IsValid => _errors.Count == 0;
    public bool IsInvalid => _errors.Count > 0;
    
    public T Value => IsValid 
        ? _value! 
        : throw new InvalidOperationException($"Cannot access value of invalid Validation. Errors: {string.Join(", ", _errors)}");
    
    public IReadOnlyList<Error> Errors => _errors.AsReadOnly();
    
    #region Factory Methods
    
    public static Validation<T> Valid(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Validation<T>(value, new List<Error>());
    }
    
    public static Validation<T> Invalid(params Error[] errors)
    {
        if (errors == null || errors.Length == 0)
            throw new ArgumentException("At least one error is required");
            
        return new Validation<T>(default, errors.ToList());
    }
    
    public static Validation<T> Invalid(IEnumerable<Error> errors)
    {
        var errorList = errors?.ToList() ?? throw new ArgumentNullException(nameof(errors));
        
        if (errorList.Count == 0)
            throw new ArgumentException("At least one error is required");
            
        return new Validation<T>(default, errorList);
    }
    
    #endregion
    
    #region Functor Operations
    
    public Validation<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        
        return IsValid 
            ? Validation<TNew>.Valid(mapper(_value!))
            : Validation<TNew>.Invalid(_errors);
    }
    
    #endregion
    
    #region Applicative Operations
    
    /// <summary>
    /// Apply a wrapped function, accumulating errors
    /// </summary>
    public Validation<TResult> Apply<TResult>(Validation<Func<T, TResult>> fn)
    {
        if (fn.IsInvalid && IsInvalid)
        {
            // Combine all errors
            var allErrors = fn._errors.Concat(_errors).ToList();
            return new Validation<TResult>(default, allErrors);
        }
        
        if (fn.IsInvalid)
            return new Validation<TResult>(default, fn._errors);
            
        if (IsInvalid)
            return new Validation<TResult>(default, _errors);
            
        return Validation<TResult>.Valid(fn._value!(_value!));
    }
    
    #endregion
    
    #region Conversions
    
    /// <summary>
    /// Convert to Result (first error only)
    /// </summary>
    public Result<T> ToResult()
    {
        return IsValid 
            ? Result<T>.Success(_value!)
            : Result<T>.Failure(_errors.First());
    }
    
    /// <summary>
    /// Convert to Result with aggregated error
    /// </summary>
    public Result<T> ToResultWithAggregatedError()
    {
        return IsValid 
            ? Result<T>.Success(_value!)
            : Result<T>.Failure(Error.Aggregate(_errors.ToArray()));
    }
    
    #endregion
    
    #region Combinators
    
    /// <summary>
    /// Combine multiple validations
    /// </summary>
    public static Validation<(T1, T2)> Combine<T1, T2>(
        Validation<T1> v1,
        Validation<T2> v2)
    {
        var errors = new List<Error>();
        
        if (v1.IsInvalid)
            errors.AddRange(v1._errors);
            
        if (v2.IsInvalid)
            errors.AddRange(v2._errors);
            
        if (errors.Count > 0)
            return Validation<(T1, T2)>.Invalid(errors);
            
        return Validation<(T1, T2)>.Valid((v1._value!, v2._value!));
    }
    
    #endregion
}
```

---

## Phase 2: Domain Layer Enhancement

### 2.1 Rich Aggregate Base (Without Event Sourcing)

**File:** `Core/Domain/Model/AggregateRoot.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Model;

/// <summary>
/// Base class for aggregate roots following tactical DDD.
/// Provides domain event support without event sourcing (MVP approach).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot<TId>
    where TId : IStrongId
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<IBusinessRule> _brokenRules = new();
    
    protected AggregateRoot(TId id) : base(id)
    {
    }
    
    protected AggregateRoot() : base()
    {
    }
    
    #region Domain Events (No Event Sourcing)
    
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    /// <summary>
    /// Raise a domain event to be dispatched after save
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
    
    /// <summary>
    /// Clear all domain events (called after dispatch)
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
    
    #endregion
    
    #region Business Rules
    
    /// <summary>
    /// Check business rule and record if broken
    /// </summary>
    protected Result<Unit> CheckRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        
        if (rule.IsBroken())
        {
            _brokenRules.Add(rule);
            return Result<Unit>.Failure(
                Error.BusinessRule(rule.Message, rule.Code));
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Check multiple business rules
    /// </summary>
    protected Result<Unit> CheckRules(params IBusinessRule[] rules)
    {
        var errors = new List<Error>();
        
        foreach (var rule in rules)
        {
            if (rule.IsBroken())
            {
                _brokenRules.Add(rule);
                errors.Add(Error.BusinessRule(rule.Message, rule.Code));
            }
        }
        
        if (errors.Any())
        {
            return Result<Unit>.Failure(Error.Aggregate(errors.ToArray()));
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Validate aggregate state
    /// </summary>
    public virtual Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        // Check all invariants
        var invariants = GetInvariants();
        foreach (var invariant in invariants)
        {
            if (invariant.IsBroken())
            {
                errors.Add(Error.BusinessRule(invariant.Message, invariant.Code));
            }
        }
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Override to provide aggregate invariants
    /// </summary>
    protected virtual IEnumerable<IBusinessRule> GetInvariants()
    {
        return Enumerable.Empty<IBusinessRule>();
    }
    
    #endregion
    
    #region State Management
    
    /// <summary>
    /// Touch the aggregate to update modification timestamp
    /// </summary>
    protected void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Mark aggregate as deleted (soft delete)
    /// </summary>
    protected virtual void MarkAsDeleted()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        Touch();
    }
    
    #endregion
}
```

### 2.2 Value Object Base

**File:** `Core/Domain/Primitives/ValueObject.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base class for value objects following DDD principles.
/// Immutable and compared by value equality.
/// </summary>
public abstract record ValueObject
{
    /// <summary>
    /// Get components for equality comparison
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();
    
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }
    
    public virtual bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;
            
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }
    
    /// <summary>
    /// Validate the value object state
    /// </summary>
    public abstract Validation<Unit> Validate();
}

/// <summary>
/// Base class for single-value value objects
/// </summary>
public abstract record SingleValueObject<T> : ValueObject
{
    public T Value { get; }
    
    protected SingleValueObject(T value)
    {
        Value = value;
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public override string ToString() => Value?.ToString() ?? string.Empty;
    
    public static implicit operator T(SingleValueObject<T> valueObject)
    {
        return valueObject.Value;
    }
}

/// <summary>
/// Example: Email value object
/// </summary>
public sealed record Email : SingleValueObject<string>
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private Email(string value) : base(value)
    {
    }
    
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Email>.Failure(Error.Validation("Email cannot be empty"));
            
        if (!EmailRegex.IsMatch(value))
            return Result<Email>.Failure(Error.Validation("Invalid email format"));
            
        return Result<Email>.Success(new Email(value.ToLowerInvariant()));
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(Value))
            errors.Add(Error.Validation("Email cannot be empty"));
            
        if (!EmailRegex.IsMatch(Value))
            errors.Add(Error.Validation("Invalid email format"));
            
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
}
```

### 2.3 Business Rules Engine

**File:** `Core/Domain/Rules/IBusinessRule.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Represents a business rule that can be broken
/// </summary>
public interface IBusinessRule
{
    string Code { get; }
    string Message { get; }
    bool IsBroken();
}

/// <summary>
/// Base implementation for business rules
/// </summary>
public abstract record BusinessRule : IBusinessRule
{
    public abstract string Code { get; }
    public abstract string Message { get; }
    public abstract bool IsBroken();
    
    /// <summary>
    /// Convert to Result
    /// </summary>
    public Result<Unit> ToResult()
    {
        return IsBroken() 
            ? Result<Unit>.Failure(Error.BusinessRule(Message, Code))
            : Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Convert to Validation
    /// </summary>
    public Validation<Unit> ToValidation()
    {
        return IsBroken()
            ? Validation<Unit>.Invalid(Error.BusinessRule(Message, Code))
            : Validation<Unit>.Valid(Unit.Value);
    }
}

/// <summary>
/// Composite rule for combining multiple rules
/// </summary>
public sealed record CompositeRule : BusinessRule
{
    private readonly IBusinessRule[] _rules;
    private readonly string _code;
    private readonly string _message;
    
    public CompositeRule(string code, string message, params IBusinessRule[] rules)
    {
        _code = code;
        _message = message;
        _rules = rules ?? Array.Empty<IBusinessRule>();
    }
    
    public override string Code => _code;
    public override string Message => _message;
    
    public override bool IsBroken()
    {
        return _rules.Any(r => r.IsBroken());
    }
    
    /// <summary>
    /// Get all broken rules
    /// </summary>
    public IEnumerable<IBusinessRule> GetBrokenRules()
    {
        return _rules.Where(r => r.IsBroken());
    }
}

/// <summary>
/// Rule builder for fluent rule creation
/// </summary>
public class RuleBuilder
{
    private readonly List<IBusinessRule> _rules = new();
    
    public RuleBuilder Must(bool condition, string code, string message)
    {
        _rules.Add(new SimpleRule(code, message, () => !condition));
        return this;
    }
    
    public RuleBuilder Must(Func<bool> predicate, string code, string message)
    {
        _rules.Add(new SimpleRule(code, message, () => !predicate()));
        return this;
    }
    
    public RuleBuilder MustNot(bool condition, string code, string message)
    {
        _rules.Add(new SimpleRule(code, message, () => condition));
        return this;
    }
    
    public RuleBuilder MustNot(Func<bool> predicate, string code, string message)
    {
        _rules.Add(new SimpleRule(code, message, predicate));
        return this;
    }
    
    public Result<Unit> Build()
    {
        var brokenRules = _rules.Where(r => r.IsBroken()).ToList();
        
        if (brokenRules.Any())
        {
            var errors = brokenRules.Select(r => 
                Error.BusinessRule(r.Message, r.Code)).ToArray();
            return Result<Unit>.Failure(Error.Aggregate(errors));
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Validation<Unit> BuildValidation()
    {
        var brokenRules = _rules.Where(r => r.IsBroken()).ToList();
        
        if (brokenRules.Any())
        {
            var errors = brokenRules.Select(r => 
                Error.BusinessRule(r.Message, r.Code)).ToArray();
            return Validation<Unit>.Invalid(errors);
        }
        
        return Validation<Unit>.Valid(Unit.Value);
    }
    
    private sealed record SimpleRule(
        string Code, 
        string Message, 
        Func<bool> Predicate) : BusinessRule
    {
        public override bool IsBroken() => Predicate();
    }
}
```

### 2.4 Specification Pattern

**File:** `Core/Domain/Specifications/Specification.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Specifications;

/// <summary>
/// Base class for specifications following the specification pattern
/// </summary>
public abstract class Specification<T>
{
    /// <summary>
    /// Convert specification to expression for LINQ queries
    /// </summary>
    public abstract Expression<Func<T, bool>> ToExpression();
    
    /// <summary>
    /// Check if entity satisfies the specification
    /// </summary>
    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }
    
    /// <summary>
    /// Combine with another specification using AND
    /// </summary>
    public Specification<T> And(Specification<T> specification)
    {
        return new AndSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Combine with another specification using OR
    /// </summary>
    public Specification<T> Or(Specification<T> specification)
    {
        return new OrSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Negate the specification
    /// </summary>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
}

/// <summary>
/// AND specification combinator
/// </summary>
internal class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;
    
    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(leftExpression, parameter),
            Expression.Invoke(rightExpression, parameter));
            
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// OR specification combinator
/// </summary>
internal class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;
    
    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.OrElse(
            Expression.Invoke(leftExpression, parameter),
            Expression.Invoke(rightExpression, parameter));
            
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// NOT specification combinator
/// </summary>
internal class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _specification;
    
    public NotSpecification(Specification<T> specification)
    {
        _specification = specification;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _specification.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.Not(Expression.Invoke(expression, parameter));
        
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// Example: Active entities specification
/// </summary>
public class ActiveSpecification<T> : Specification<T>
    where T : IEntity
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => !entity.IsDeleted;
    }
}

/// <summary>
/// Example: Created date range specification
/// </summary>
public class CreatedBetweenSpecification<T> : Specification<T>
    where T : IAuditable
{
    private readonly DateTime _from;
    private readonly DateTime _to;
    
    public CreatedBetweenSpecification(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.CreatedAt >= _from && entity.CreatedAt <= _to;
    }
}
```

---

## Phase 3: Enhanced Error System

### 3.1 Comprehensive Error Implementation

**File:** `Core/Diagnostics/Errors/Error.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Comprehensive error model with categorization and metadata
/// </summary>
public sealed record Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public ErrorSeverity Severity { get; }
    public Exception? InnerException { get; }
    public IReadOnlyDictionary<string, object>? Metadata { get; }
    public string? StackTrace { get; }
    public DateTime OccurredAt { get; }
    public string? CorrelationId { get; }
    
    private Error(
        string code,
        string message,
        ErrorType type,
        ErrorSeverity severity = ErrorSeverity.Error,
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        string? stackTrace = null,
        string? correlationId = null)
    {
        Code = code;
        Message = message;
        Type = type;
        Severity = severity;
        InnerException = innerException;
        Metadata = metadata;
        StackTrace = stackTrace;
        OccurredAt = DateTime.UtcNow;
        CorrelationId = correlationId;
    }
    
    #region Factory Methods
    
    public static Error Validation(
        string message,
        string code = "VALIDATION_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Validation, 
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    public static Error NotFound(
        string message,
        string code = "NOT_FOUND",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.NotFound,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    public static Error Conflict(
        string message,
        string code = "CONFLICT",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Conflict,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    public static Error BusinessRule(
        string message,
        string code = "BUSINESS_RULE",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.BusinessRule,
            ErrorSeverity.Error, metadata: metadata);
    }
    
    public static Error Unauthorized(
        string message = "Unauthorized access",
        string code = "UNAUTHORIZED",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Unauthorized,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    public static Error Forbidden(
        string message = "Access forbidden",
        string code = "FORBIDDEN",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Forbidden,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    public static Error Internal(
        string message,
        string code = "INTERNAL_ERROR",
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        string? stackTrace = null;
        
        #if DEBUG
        // Only capture stack trace in debug mode (performance consideration)
        stackTrace = innerException?.StackTrace ?? Environment.StackTrace;
        #endif
        
        return new Error(code, message, ErrorType.Internal,
            ErrorSeverity.Critical, innerException, metadata, stackTrace);
    }
    
    public static Error External(
        string message,
        string code = "EXTERNAL_ERROR",
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.External,
            ErrorSeverity.Error, innerException, metadata);
    }
    
    public static Error Timeout(
        string message = "Operation timed out",
        string code = "TIMEOUT",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Timeout,
            ErrorSeverity.Error, metadata: metadata);
    }
    
    public static Error Cancelled(
        string message = "Operation was cancelled",
        string code = "CANCELLED",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Cancelled,
            ErrorSeverity.Info, metadata: metadata);
    }
    
    public static Error RateLimit(
        string message = "Rate limit exceeded",
        string code = "RATE_LIMIT",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.RateLimit,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    public static Error FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        
        var metadata = new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["Source"] = exception.Source ?? "Unknown"
        };
        
        if (exception.Data.Count > 0)
        {
            foreach (DictionaryEntry entry in exception.Data)
            {
                if (entry.Key != null && entry.Value != null)
                {
                    metadata[$"Data_{entry.Key}"] = entry.Value;
                }
            }
        }
        
        return Internal(
            exception.Message,
            exception.GetType().Name.ToUpperInvariant(),
            exception,
            metadata);
    }
    
    /// <summary>
    /// Aggregate multiple errors into a composite error
    /// </summary>
    public static Error Aggregate(params Error[] errors)
    {
        if (errors == null || errors.Length == 0)
            throw new ArgumentException("At least one error is required");
            
        if (errors.Length == 1)
            return errors[0];
            
        var messages = string.Join("; ", errors.Select(e => e.Message));
        var codes = string.Join(",", errors.Select(e => e.Code));
        var highestSeverity = errors.Max(e => e.Severity);
        
        var metadata = new Dictionary<string, object>
        {
            ["ErrorCount"] = errors.Length,
            ["ErrorCodes"] = codes,
            ["Errors"] = errors
        };
        
        return new Error(
            "MULTIPLE_ERRORS",
            messages,
            ErrorType.Aggregate,
            highestSeverity,
            metadata: metadata);
    }
    
    #endregion
    
    #region Builder Methods
    
    /// <summary>
    /// Add metadata to the error
    /// </summary>
    public Error WithMetadata(string key, object value)
    {
        var newMetadata = Metadata != null 
            ? new Dictionary<string, object>(Metadata) 
            : new Dictionary<string, object>();
            
        newMetadata[key] = value;
        
        return this with { Metadata = newMetadata };
    }
    
    /// <summary>
    /// Add correlation ID for tracing
    /// </summary>
    public Error WithCorrelationId(string correlationId)
    {
        return this with { CorrelationId = correlationId };
    }
    
    /// <summary>
    /// Change severity level
    /// </summary>
    public Error WithSeverity(ErrorSeverity severity)
    {
        return this with { Severity = severity };
    }
    
    #endregion
    
    #region Conversion Methods
    
    /// <summary>
    /// Convert to HTTP status code
    /// </summary>
    public int ToHttpStatusCode() => Type switch
    {
        ErrorType.Validation => 400,
        ErrorType.Unauthorized => 401,
        ErrorType.Forbidden => 403,
        ErrorType.NotFound => 404,
        ErrorType.Conflict => 409,
        ErrorType.RateLimit => 429,
        ErrorType.Internal => 500,
        ErrorType.External => 502,
        ErrorType.Timeout => 504,
        _ => 500
    };
    
    /// <summary>
    /// Convert to problem details
    /// </summary>
    public ProblemDetails ToProblemDetails()
    {
        return new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{ToHttpStatusCode()}",
            Title = Type.ToString(),
            Status = ToHttpStatusCode(),
            Detail = Message,
            Instance = CorrelationId,
            Extensions = Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }
    
    #endregion
}

/// <summary>
/// Error type categorization
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    BusinessRule,
    Unauthorized,
    Forbidden,
    Internal,
    External,
    Timeout,
    Cancelled,
    RateLimit,
    Persistence,
    Aggregate
}

/// <summary>
/// Error severity levels
/// </summary>
public enum ErrorSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
```

---

## 📊 Migration Strategy

### Step-by-Step Refactoring Plan

1. **Week 1: Functional Foundation**
   - Replace current Result<T> with complete implementation
   - Add Option<T> monad
   - Add Validation<T> for error accumulation
   - Update all existing code to use new types

2. **Week 2: Domain Enhancement**
   - Replace basic aggregates with rich domain models
   - Implement value objects properly
   - Add business rules engine
   - Implement specification pattern

3. **Week 3: Testing & Validation**
   - Comprehensive unit tests for all new types
   - Integration tests for domain operations
   - Performance benchmarks
   - Documentation updates

### Breaking Changes Mitigation

Since this is a **brutal refactoring** with no backward compatibility:

1. **Complete Replacement**: Delete old implementations entirely
2. **No Feature Flags**: Direct replacement approach
3. **New Database**: No migration complexity
4. **Clean Deployment**: Deploy as new version

---

## 🎯 Verification Checklist

### Phase 1 Completion Gate
- [ ] Result<T> fully implemented with all operations
- [ ] Option<T> monad complete
- [ ] Validation<T> working with error accumulation
- [ ] All async operations properly configured
- [ ] Performance optimizations applied
- [ ] All existing code updated to new patterns

### Phase 2 Completion Gate
- [ ] Rich aggregate roots implemented
- [ ] Value objects with validation
- [ ] Business rules engine operational
- [ ] Specification pattern working
- [ ] Domain services defined
- [ ] All domain invariants protected

### Phase 3 Completion Gate
- [ ] Comprehensive error system
- [ ] Guard clauses implemented
- [ ] All diagnostics operational
- [ ] Logging integration complete
- [ ] Metrics collection working
- [ ] Full test coverage achieved

---

## 🚀 Quick Start Examples

### Using Result<T> in Practice

```csharp
// Command handler with railway-oriented programming
public async Task<Result<ConversationId>> Handle(
    CreateConversationCommand command,
    CancellationToken ct)
{
    // Validate input
    return await ConversationTitle.Create(command.Title)
        // Create aggregate
        .Bind(title => Conversation.Create(command.UserId, title))
        // Check business rules
        .Ensure(
            conv => conv.Participants.Count <= 10,
            _ => Error.BusinessRule("Too many participants"))
        // Save to repository
        .BindAsync(async conv => 
        {
            await _repository.AddAsync(conv, ct);
            return Result<Conversation>.Success(conv);
        })
        // Map to response
        .Map(conv => conv.Id)
        // Handle errors
        .TapError(error => _logger.LogError("Failed to create conversation: {Error}", error));
}
```

### Using Option<T> for Nullable Handling

```csharp
// Repository with Option<T> return
public async Task<Option<Conversation>> FindByIdAsync(
    ConversationId id,
    CancellationToken ct)
{
    var entity = await _context.Conversations
        .FirstOrDefaultAsync(c => c.Id == id, ct);
        
    return Option<Conversation>.From(entity);
}

// Usage in query handler
public async Task<Result<ConversationDto>> Handle(
    GetConversationQuery query,
    CancellationToken ct)
{
    return await _repository
        .FindByIdAsync(query.Id, ct)
        .ToResult(Error.NotFound($"Conversation {query.Id} not found"))
        .MapAsync(async conv => await MapToDto(conv, ct));
}
```

### Rich Domain Model Example

```csharp
// Rich aggregate with business rules
public sealed class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = new();
    private readonly List<Participant> _participants = new();
    
    public ConversationTitle Title { get; private set; }
    public ConversationState State { get; private set; }
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();
    
    private Conversation() { } // EF Core
    
    public static Result<Conversation> Create(
        UserId ownerId,
        ConversationTitle title)
    {
        var conversation = new Conversation
        {
            Id = ConversationId.New(),
            Title = title,
            State = ConversationState.Active
        };
        
        // Add owner as first participant
        var ownerResult = conversation.AddParticipant(ownerId, ParticipantRole.Owner);
        
        if (ownerResult.IsFailure)
            return Result<Conversation>.Failure(ownerResult.Error);
            
        conversation.RaiseDomainEvent(new ConversationCreatedEvent(
            conversation.Id,
            ownerId,
            title.Value));
            
        return Result<Conversation>.Success(conversation);
    }
    
    public Result<MessageId> AddMessage(
        MessageContent content,
        UserId authorId)
    {
        // Check business rules
        return CheckRules(
            new ConversationMustBeActive(State),
            new UserMustBeParticipant(authorId, _participants),
            new MessageLimitNotExceeded(_messages.Count))
            .Bind(() =>
            {
                var message = Message.Create(content, authorId);
                _messages.Add(message);
                Touch();
                
                RaiseDomainEvent(new MessageAddedEvent(
                    Id,
                    message.Id,
                    authorId));
                    
                return Result<MessageId>.Success(message.Id);
            });
    }
    
    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        yield return new MustHaveTitle(Title);
        yield return new MustHaveAtLeastOneParticipant(_participants);
        yield return new ParticipantLimitNotExceeded(_participants.Count);
    }
}
```

---

## 📚 References & Resources

### Documentation
- [Railway-Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [Domain-Driven Design Tactical Patterns](https://martinfowler.com/bliki/DomainDrivenDesign.html)
- [Functional Programming in C#](https://www.manning.com/books/functional-programming-in-c-sharp)

### Performance Considerations
- Use `record struct` for small, frequently-used types
- Consider `record` (class) for large payloads to avoid boxing
- Implement `ValueTask` variants for hot paths
- Use `ConfigureAwait(false)` consistently in library code
- Wrap expensive operations (like stack trace capture) in DEBUG directives

### Testing Strategy
- Unit test all functional operations
- Property-based testing for monadic laws
- Benchmark critical paths
- Integration tests for domain operations
- Mutation testing for business rules

---

## ✅ Success Criteria

The refactoring is complete when:

1. **All functional types** properly implement monadic operations
2. **Zero null references** throughout the Core layer
3. **All operations** return Result<T> or Option<T>
4. **Domain models** are rich with proper encapsulation
5. **Business rules** are explicit and testable
6. **100% test coverage** for critical paths
7. **Performance benchmarks** meet targets
8. **Documentation** is complete and accurate

---

**END OF IMPLEMENTATION GUIDE**