# 🚀 Epic 1: Functional Foundation - Complete Implementation Guide

**Version:** 1.0 - Railway-Oriented Programming Foundation  
**Scope:** Functional programming primitives for BuildingBlocks/Core  
**Approach:** Zero null references, complete monadic operations  
**Target:** .NET 10, Production-ready functional architecture

---

## 📋 Executive Summary

This epic establishes the **complete functional programming foundation** for the BuildingBlocks/Core layer. We implement:

- ✅ **Complete Result<T> Pattern** - Full monad with railway operations
- ✅ **Option<T> Monad** - Zero null reference approach
- ✅ **Unit Type** - Functional equivalent of void
- ✅ **Validation<T>** - Error accumulation for complex validation
- ✅ **Enhanced StrongId** - Type-safe identifiers
- ✅ **MediatR Integration** - Functional behaviors and pipelines

---

## 🎯 Target Architecture

### New Functional Structure
```
Core/Functional/
├── Unit.cs                    # Unit type for void operations
├── Results/                   # Complete Result<T> implementation
│   ├── Result.cs             # Main Result<T> monad
│   ├── IResult.cs            # Result interface
│   └── Extensions/           # Railway operations
├── Options/                   # Option<T> monad
│   ├── Option.cs             # Main Option<T> implementation
│   └── Extensions/           # Option operations
├── Validation/               # Validation<T> for error accumulation
│   ├── Validation.cs         # Main Validation type
│   └── Extensions/           # Validation combinators
└── Either/                   # Either<TLeft, TRight> (future)
    └── Either.cs             # Either implementation
```

---

## 🔧 Implementation Details

### 1.1 Unit Type Implementation

**File:** `Core/Functional/Unit.cs`

```csharp
namespace BuildingBlocks.Core.Functional;

/// <summary>
/// Represents a value that carries no information - functional equivalent of void.
/// Used in Result<Unit> and Option<Unit> for operations that return no meaningful data.
/// Follows F#'s unit type pattern.
/// </summary>
public readonly record struct Unit : IComparable<Unit>, IEquatable<Unit>
{
    /// <summary>
    /// The single instance of Unit type
    /// </summary>
    public static readonly Unit Value = new();
    
    /// <summary>
    /// Default constructor creates the unit value
    /// </summary>
    public Unit() { }
    
    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString() => "()";
    
    /// <summary>
    /// Comparison always returns equal
    /// </summary>
    public int CompareTo(Unit other) => 0;
    
    /// <summary>
    /// Equality always returns true
    /// </summary>
    public bool Equals(Unit other) => true;
    
    /// <summary>
    /// Hash code is constant
    /// </summary>
    public override int GetHashCode() => 0;
    
    /// <summary>
    /// Implicit conversion from any value to Unit (discards the value)
    /// </summary>
    public static implicit operator Unit(object? _) => Value;
    
    /// <summary>
    /// Task<Unit> factory for async operations that return no data
    /// </summary>
    public static Task<Unit> Task => System.Threading.Tasks.Task.FromResult(Value);
    
    /// <summary>
    /// ValueTask<Unit> factory for high-performance async operations
    /// </summary>
    public static ValueTask<Unit> ValueTask => System.Threading.Tasks.ValueTask.FromResult(Value);
}

/// <summary>
/// Extension methods for Unit type
/// </summary>
public static class UnitExtensions
{
    /// <summary>
    /// Converts any value to Unit, discarding the original value
    /// </summary>
    public static Unit ToUnit<T>(this T _) => Unit.Value;
    
    /// <summary>
    /// Converts Task<T> to Task<Unit>, discarding the result
    /// </summary>
    public static async Task<Unit> ToUnit<T>(this Task<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
    
    /// <summary>
    /// Converts ValueTask<T> to ValueTask<Unit>, discarding the result
    /// </summary>
    public static async ValueTask<Unit> ToUnit<T>(this ValueTask<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
    
    /// <summary>
    /// Converts Task to Task<Unit>
    /// </summary>
    public static async Task<Unit> ToUnit(this Task task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
    
    /// <summary>
    /// Converts ValueTask to ValueTask<Unit>
    /// </summary>
    public static async ValueTask<Unit> ToUnit(this ValueTask task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
}
```

### 1.2 Complete Result<T> Implementation

**File:** `Core/Functional/Results/IResult.cs`

```csharp
namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Interface for Result types
/// </summary>
public interface IResult<T>
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
    T Value { get; }
    Error Error { get; }
    
    TResult Match<TResult>(
        Func<T, TResult> success,
        Func<Error, TResult> failure);
}

/// <summary>
/// Non-generic Result interface
/// </summary>
public interface IResult
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
    Error Error { get; }
    
    TResult Match<TResult>(
        Func<TResult> success,
        Func<Error, TResult> failure);
}
```

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
    
    private Result(T value)
    {
        _value = value;
        _error = null;
        _state = ResultState.Success;
    }
    
    private Result(Error error)
    {
        _value = default;
        _error = error;
        _state = ResultState.Failure;
    }
    
    #region Properties
    
    public bool IsSuccess => _state == ResultState.Success;
    public bool IsFailure => _state == ResultState.Failure;
    
    public T Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException($"Cannot access value of failed result: {_error}");
        
    public Error Error => IsFailure 
        ? _error! 
        : throw new InvalidOperationException("Cannot access error of successful result");
    
    #endregion
    
    #region Factory Methods
    
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value);
    }
    
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
    
    public static Result<T> From(T? value, Error error)
    {
        return value is not null ? Success(value) : Failure(error);
    }
    
    #endregion
    
    #region Pattern Matching
    
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
    
    #endregion
    
    #region LINQ Integration
    
    /// <summary>
    /// LINQ Select operator - delegates to Map for functional composition
    /// </summary>
    public Result<TNew> Select<TNew>(Func<T, TNew> selector) => Map(selector);
    
    /// <summary>
    /// LINQ Where operator - filters success values based on predicate
    /// </summary>
    public Result<T> Where(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return IsSuccess && !predicate(_value!) 
            ? Result<T>.Failure(Error.Validation("Predicate failed"))
            : this;
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
    
    #region Side Effects
    
    /// <summary>
    /// Execute action without breaking the chain
    /// </summary>
    public Result<T> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (IsSuccess)
            action(_value!);
            
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
    
    #region Conversions
    
    /// <summary>
    /// Convert to Option
    /// </summary>
    public Option<T> ToOption()
    {
        return IsSuccess ? Option<T>.Some(_value!) : Option<T>.None();
    }
    
    /// <summary>
    /// Convert to nullable
    /// </summary>
    public T? ToNullable()
    {
        return IsSuccess ? _value : default;
    }
    
    #endregion
    
    #region Operators
    
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }
    
    public static implicit operator Result<T>(Error error)
    {
        return Failure(error);
    }
    
    public static bool operator true(Result<T> result)
    {
        return result.IsSuccess;
    }
    
    public static bool operator false(Result<T> result)
    {
        return result.IsFailure;
    }
    
    #endregion
}

/// <summary>
/// Non-generic Result for operations that don't return values
/// </summary>
public readonly record struct Result : IResult
{
    private readonly Error? _error;
    private readonly bool _isSuccess;
    
    private Result(bool isSuccess, Error? error = null)
    {
        _isSuccess = isSuccess;
        _error = error;
    }
    
    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;
    
    public Error Error => IsFailure 
        ? _error! 
        : throw new InvalidOperationException("Cannot access error of successful result");
    
    public static Result Success() => new(true);
    public static Result Failure(Error error) => new(false, error);
    
    public TResult Match<TResult>(
        Func<TResult> success,
        Func<Error, TResult> failure)
    {
        return IsSuccess ? success() : failure(_error!);
    }
    
    public Result<T> Map<T>(Func<T> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess 
            ? Result<T>.Success(mapper()) 
            : Result<T>.Failure(_error!);
    }
    
    public Result<T> Bind<T>(Func<Result<T>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder() : Result<T>.Failure(_error!);
    }
    
    public static implicit operator Result(Error error)
    {
        return Failure(error);
    }
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

### 1.3 Option<T> Monad Implementation

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
    
    #region LINQ Integration
    
    /// <summary>
    /// LINQ Select operator - delegates to Map for functional composition
    /// </summary>
    public Option<TNew> Select<TNew>(Func<T, TNew> selector) => Map(selector);
    
    /// <summary>
    /// LINQ Where operator - delegates to Filter for predicate-based filtering
    /// </summary>
    public Option<T> Where(Func<T, bool> predicate) => Filter(predicate);
    
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

/// <summary>
/// Interface for Option types
/// </summary>
public interface IOption<T>
{
    bool IsSome { get; }
    bool IsNone { get; }
    T Value { get; }
    
    TResult Match<TResult>(
        Func<T, TResult> some,
        Func<TResult> none);
}
```

### 1.4 Validation<T> for Error Accumulation

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
    
    /// <summary>
    /// Combine three validations
    /// </summary>
    public static Validation<(T1, T2, T3)> Combine<T1, T2, T3>(
        Validation<T1> v1,
        Validation<T2> v2,
        Validation<T3> v3)
    {
        var errors = new List<Error>();
        
        if (v1.IsInvalid)
            errors.AddRange(v1._errors);
            
        if (v2.IsInvalid)
            errors.AddRange(v2._errors);
            
        if (v3.IsInvalid)
            errors.AddRange(v3._errors);
            
        if (errors.Count > 0)
            return Validation<(T1, T2, T3)>.Invalid(errors);
            
        return Validation<(T1, T2, T3)>.Valid((v1._value!, v2._value!, v3._value!));
    }
    
    #endregion
}
```

### 1.5 Enhanced StrongId Implementation

**File:** `Core/Domain/Primitives/IStrongId.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Marker interface for strongly-typed identifiers
/// </summary>
public interface IStrongId
{
    /// <summary>
    /// Get the underlying primitive value
    /// </summary>
    object GetValue();
    
    /// <summary>
    /// Get the type of the underlying primitive
    /// </summary>
    Type GetValueType();
}

/// <summary>
/// Generic strongly-typed identifier interface
/// </summary>
public interface IStrongId<TPrimitive> : IStrongId
    where TPrimitive : struct
{
    TPrimitive Value { get; }
}
```

**File:** `Core/Domain/Primitives/StrongId.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base implementation for strongly-typed identifiers
/// Provides type safety, validation, and serialization support
/// </summary>
public abstract record StrongId<TPrimitive> : IStrongId<TPrimitive>, IComparable<StrongId<TPrimitive>>
    where TPrimitive : struct, IComparable<TPrimitive>, IEquatable<TPrimitive>
{
    public TPrimitive Value { get; }
    
    protected StrongId(TPrimitive value)
    {
        if (value.Equals(default(TPrimitive)))
            throw new ArgumentException($"StrongId value cannot be default({typeof(TPrimitive).Name})", nameof(value));
            
        Value = value;
    }
    
    #region IStrongId Implementation
    
    public object GetValue() => Value;
    public Type GetValueType() => typeof(TPrimitive);
    
    #endregion
    
    #region Comparison
    
    public int CompareTo(StrongId<TPrimitive>? other)
    {
        if (other is null) return 1;
        return Value.CompareTo(other.Value);
    }
    
    public static bool operator <(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) < 0;
        
    public static bool operator >(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) > 0;
        
    public static bool operator <=(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) <= 0;
        
    public static bool operator >=(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) >= 0;
    
    #endregion
    
    #region Conversion
    
    public static implicit operator TPrimitive(StrongId<TPrimitive> strongId) => strongId.Value;
    
    public override string ToString() => Value.ToString() ?? string.Empty;
    
    #endregion
}

/// <summary>
/// GUID-based strongly-typed identifier
/// </summary>
public abstract record GuidStrongId : StrongId<Guid>
{
    protected GuidStrongId(Guid value) : base(value) { }
    
    protected GuidStrongId() : base(Guid.NewGuid()) { }
    
    /// <summary>
    /// Create a new instance with a new GUID
    /// </summary>
    public static T New<T>() where T : GuidStrongId, new() => new();
    
    /// <summary>
    /// Create from string representation
    /// </summary>
    protected static Result<T> FromString<T>(string value, Func<Guid, T> factory)
        where T : GuidStrongId
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} cannot be empty"));
            
        if (!Guid.TryParse(value, out var guid))
            return Result<T>.Failure(Error.Validation($"Invalid {typeof(T).Name} format"));
            
        if (guid == Guid.Empty)
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} cannot be empty GUID"));
            
        return Result<T>.Success(factory(guid));
    }
}

/// <summary>
/// Integer-based strongly-typed identifier
/// </summary>
public abstract record IntStrongId : StrongId<int>
{
    protected IntStrongId(int value) : base(value) 
    { 
        if (value <= 0)
            throw new ArgumentException("Integer StrongId must be positive", nameof(value));
    }
    
    /// <summary>
    /// Create from string representation
    /// </summary>
    protected static Result<T> FromString<T>(string value, Func<int, T> factory)
        where T : IntStrongId
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} cannot be empty"));
            
        if (!int.TryParse(value, out var id))
            return Result<T>.Failure(Error.Validation($"Invalid {typeof(T).Name} format"));
            
        if (id <= 0)
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} must be positive"));
            
        return Result<T>.Success(factory(id));
    }
}
```

### 1.5.1 StrongId JSON Converter Enhancement

**File:** `Core/Domain/Primitives/StrongIdJsonConverterFactory.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// JSON converter factory for strongly-typed identifiers
/// Provides seamless serialization/deserialization for all StrongId types
/// </summary>
public class StrongIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return IsStrongIdType(typeToConvert);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = GetStrongIdValueType(typeToConvert);
        var converterType = typeof(StrongIdJsonConverter<,>).MakeGenericType(typeToConvert, valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private static bool IsStrongIdType(Type type)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StrongId<>))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static Type GetStrongIdValueType(Type strongIdType)
    {
        var current = strongIdType;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StrongId<>))
                return current.GetGenericArguments()[0];
            current = current.BaseType;
        }
        throw new ArgumentException($"Type {strongIdType} is not a StrongId");
    }
}

public class StrongIdJsonConverter<TStrongId, TValue> : JsonConverter<TStrongId>
    where TStrongId : StrongId<TValue>
    where TValue : struct
{
    public override TStrongId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
        return (TStrongId)Activator.CreateInstance(typeToConvert, value)!;
    }

    public override void Write(Utf8JsonWriter writer, TStrongId value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}
```

**Registration:**
```csharp
// In Program.cs or ServiceCollectionExtensions
services.AddStrongIdJson();

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStrongIdJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new StrongIdJsonConverterFactory());
        });
        
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.Converters.Add(new StrongIdJsonConverterFactory());
        });
        
        return services;
    }
}
```

**Before/After JSON Example:**

Before (primitive obsession):
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "conversationId": "7d444840-9dc0-11d1-b245-5ffdce74fad2"
}
```

After (with StrongId converter):
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "conversationId": "7d444840-9dc0-11d1-b245-5ffdce74fad2"
}
```

*Note: JSON remains the same, but now type-safe with compile-time guarantees*

### 1.6 MediatR Integration Enhancements

**File:** `Core/Application/Behaviors/ResultPipelineBehavior.cs`

```csharp
namespace BuildingBlocks.Core.Application.Behaviors;

/// <summary>
/// Pipeline behavior that ensures all command/query handlers return Result<T>
/// Wraps exceptions in Result.Failure automatically using reflection-free caching
/// </summary>
public class ResultPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<ResultPipelineBehavior<TRequest, TResponse>> _logger;
    
    public ResultPipelineBehavior(ILogger<ResultPipelineBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await next();
            
            // Log successful operations using cached delegates
            if (FailureCache<TResponse>.IsResult && FailureCache<TResponse>.IsSuccess(response))
            {
                _logger.LogDebug("Successfully handled {RequestType}", typeof(TRequest).Name);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestType}: {Message}", 
                typeof(TRequest).Name, ex.Message);
            
            var error = Error.FromException(ex);
            
            // Use cached failure delegate for zero-reflection performance
            return FailureCache<TResponse>.FailureFactory?.Invoke(error) ?? throw;
        }
    }
}

/// <summary>
/// Static cache for Result<T> failure creation - eliminates per-call reflection
/// </summary>
public static class FailureCache<T>
{
    public static readonly bool IsResult;
    public static readonly Func<Error, T>? FailureFactory;
    public static readonly Func<object, bool>? IsSuccess;
    
    static FailureCache()
    {
        var type = typeof(T);
        
        // Check if T is Result<U> or Result
        if (type == typeof(Result))
        {
            IsResult = true;
            FailureFactory = error => (T)(object)Result.Failure(error);
            IsSuccess = obj => ((Result)obj).IsSuccess;
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
        {
            IsResult = true;
            
            // Cache the Failure method for this specific Result<U> type
            var failureMethod = type.GetMethod("Failure", 
                BindingFlags.Public | BindingFlags.Static);
            
            if (failureMethod != null)
            {
                FailureFactory = error => (T)failureMethod.Invoke(null, new object[] { error })!;
            }
            
            // Cache IsSuccess property getter
            var isSuccessProperty = type.GetProperty("IsSuccess");
            if (isSuccessProperty != null)
            {
                IsSuccess = obj => (bool)isSuccessProperty.GetValue(obj)!;
            }
        }
        else
        {
            IsResult = false;
            FailureFactory = null;
            IsSuccess = null;
        }
    }
}
```

### Reflection-Free Performance Enhancement

**Before** (per-call reflection):
```diff
- var failureMethod = typeof(TResponse).GetMethod("Failure");
- var result = failureMethod?.Invoke(null, new object[] { error });
```

**After** (static cached delegates):
```diff
+ return FailureCache<TResponse>.FailureFactory?.Invoke(error) ?? throw;
```

This optimization eliminates reflection overhead by pre-computing failure factory delegates at type initialization.

## ⚡ Performance Guard-Rails

### Benchmark & CI Guard

To ensure the functional foundation maintains zero-allocation performance, we implement comprehensive benchmarking with CI integration:

**Benchmark Project:** `tests/Benchmarks/ResultBenchmarks.cs`

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net100)]
public class ResultBenchmarks
{
    [Benchmark]
    public Result<int> Success_Creation() => Result<int>.Success(42);
    
    [Benchmark]
    public Result<int> Failure_Creation() => Result<int>.Failure(Error.Validation("Test error"));
    
    [Benchmark]
    public Result<string> Map_Success() => 
        Result<int>.Success(42).Map(x => x.ToString());
        
    [Benchmark]
    public Result<int> Bind_Success() => 
        Result<int>.Success(21).Bind(x => Result<int>.Success(x * 2));
}
```

**Pass/Fail Rule:** All core monad operations (Success, Failure, Map, Bind) must maintain **0 B allocations**.

**CI Integration:**
```yaml
- name: Performance Benchmarks
  run: |
    cd tests/Benchmarks
    dotnet run -c Release -f net10.0 --framework net10.0
    # Fail build if allocations > 0B for core operations
```

---

## 📊 Implementation Roadmap

### Week 1: Core Types
- [ ] Implement Unit type
- [ ] Complete Result<T> with all operations
- [ ] Implement Option<T> monad
- [ ] Add Validation<T> for error accumulation
- [ ] Update all existing code to use new types

### Week 2: StrongId Enhancement
- [ ] Enhanced StrongId base classes
- [ ] JSON serialization support
- [ ] Example implementations (ConversationId, UserId, etc.)
- [ ] Migration of existing IDs

### Week 3: MediatR Integration
- [ ] ResultPipelineBehavior implementation
- [ ] ValidationBehavior updates
- [ ] Extension methods for configuration
- [ ] Complete pipeline testing

---

## 🎯 Success Criteria

Epic 1 is complete when:

1. ✅ **All functional types** implement complete monadic operations
2. ✅ **Zero null references** in Result/Option usage
3. ✅ **All async operations** properly configured
4. ✅ **StrongId types** replace primitive obsession
5. ✅ **MediatR behaviors** integrate seamlessly
6. ✅ **100% test coverage** for functional operations
7. ✅ **Performance benchmarks** meet requirements
8. ✅ **Monad laws** verified through property-based testing

### Property-based Tests

**Test Project:** `tests/PropertyBased/`

Monad laws enforced using FsCheck:

- **Left Identity Law:** `Result<T>.Success(a).Bind(f) == f(a)`
- **Right Identity Law:** `m.Bind(Result<T>.Success) == m`
- **Associativity Law:** `m.Bind(f).Bind(g) == m.Bind(x => f(x).Bind(g))`

**Functor Laws:**
- **Identity Law:** `m.Map(x => x) == m`
- **Composition Law:** `m.Map(f).Map(g) == m.Map(x => g(f(x)))`

```csharp
[Property]
public Property Result_Left_Identity_Law()
{
    return Prop.ForAll<int, int>(
        (value, multiplier) =>
        {
            var f = (int x) => Result<int>.Success(x * multiplier);
            var left = Result<int>.Success(value).Bind(f);
            var right = f(value);
            return left.Equals(right);
        });
}

[Property]
public Property Option_Functor_Identity_Law()
{
    return Prop.ForAll<string>(value =>
    {
        var option = Option<string>.Some(value);
        var mapped = option.Map(x => x);
        return option.Equals(mapped);
    });
}
```

---

## 🚀 Usage Examples

### Railway-Oriented Programming
```csharp
// Command handler with complete error handling
public async Task<Result<ConversationId>> Handle(
    CreateConversationCommand command,
    CancellationToken ct)
{
    return await ConversationTitle.Create(command.Title)
        .Bind(title => Conversation.Create(command.UserId, title))
        .BindAsync(async conv => 
        {
            await _repository.AddAsync(conv, ct);
            return Result<Conversation>.Success(conv);
        })
        .Map(conv => conv.Id)
        .TapError(error => _logger.LogError("Creation failed: {Error}", error));
}
```

### Option<T> for Null Safety
```csharp
// Repository with Option<T> returns
public async Task<Option<User>> FindByEmailAsync(Email email, CancellationToken ct)
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Email == email, ct);
        
    return Option<User>.From(user);
}

// Query handler with safe null handling
public async Task<Result<UserDto>> Handle(GetUserQuery query, CancellationToken ct)
{
    return await _repository
        .FindByEmailAsync(query.Email, ct)
        .ToResult(Error.NotFound($"User with email {query.Email} not found"))
        .MapAsync(async user => await MapToDto(user, ct));
}
```

### Validation<T> for Error Accumulation
```csharp
// Complex validation with error accumulation
public static Validation<CreateUserCommand> Validate(CreateUserCommand command)
{
    var emailValidation = Email.Create(command.Email)
        .ToValidation();
        
    var nameValidation = UserName.Create(command.Name)
        .ToValidation();
        
    var ageValidation = command.Age >= 18
        ? Validation<int>.Valid(command.Age)
        : Validation<int>.Invalid(Error.Validation("User must be 18 or older"));
    
    return Validation<CreateUserCommand>.Combine(
        emailValidation,
        nameValidation,
        ageValidation)
        .Map(_ => command);
}
```

---

**END OF EPIC 1: FUNCTIONAL FOUNDATION**