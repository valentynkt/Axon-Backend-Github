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
- ✅ **Error Contract** - Complete error type system
- ✅ **MediatR Integration** - Functional behaviors and pipelines

---

## 🎯 Target Architecture

### New Functional Structure
```
Core/Functional/
├── Unit.cs                    # Unit type for void operations
├── Error.cs                   # Complete error contract
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

### 1.0 Error Contract

**File:** `Core/Functional/Error.cs`

```csharp
namespace BuildingBlocks.Core.Functional;

/// <summary>
/// Immutable error record with comprehensive factory methods.
/// Designed for railway-oriented programming and error accumulation.
/// </summary>
public sealed record Error
{
    public string Code { get; }
    public string Message { get; }
    public string? Details { get; }
    public ErrorType Type { get; }
    public Exception? Exception { get; }
    public Dictionary<string, object> Metadata { get; }

    private Error(
        string code,
        string message,
        ErrorType type,
        string? details = null,
        Exception? exception = null,
        Dictionary<string, object>? metadata = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Type = type;
        Details = details;
        Exception = exception;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    #region Factory Methods

    /// <summary>
    /// Create a validation error
    /// </summary>
    public static Error Validation(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "VALIDATION_FAILED",
            message,
            ErrorType.Validation,
            details);
    }

    /// <summary>
    /// Create a business rule violation error
    /// </summary>
    public static Error BusinessRule(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "BUSINESS_RULE_VIOLATION",
            message,
            ErrorType.BusinessRule,
            details);
    }

    /// <summary>
    /// Create an aggregate/domain error
    /// </summary>
    public static Error Aggregate(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "AGGREGATE_ERROR",
            message,
            ErrorType.Aggregate,
            details);
    }

    /// <summary>
    /// Create a not found error
    /// </summary>
    public static Error NotFound(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "NOT_FOUND",
            message,
            ErrorType.NotFound,
            details);
    }

    /// <summary>
    /// Create a conflict error
    /// </summary>
    public static Error Conflict(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "CONFLICT",
            message,
            ErrorType.Conflict,
            details);
    }

    /// <summary>
    /// Create a cancellation error
    /// </summary>
    public static Error Cancelled(string message = "Operation was cancelled", string? code = null)
    {
        return new Error(
            code ?? "OPERATION_CANCELLED",
            message,
            ErrorType.Cancellation);
    }

    /// <summary>
    /// Create an authorization error
    /// </summary>
    public static Error Unauthorized(string message = "Access denied", string? code = null, string? details = null)
    {
        return new Error(
            code ?? "UNAUTHORIZED",
            message,
            ErrorType.Authorization,
            details);
    }

    /// <summary>
    /// Create a system/infrastructure error
    /// </summary>
    public static Error System(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "SYSTEM_ERROR",
            message,
            ErrorType.System,
            details);
    }

    /// <summary>
    /// Create error from exception with proper categorization
    /// </summary>
    public static Error FromException(Exception exception, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var errorType = exception switch
        {
            ArgumentException or ArgumentNullException => ErrorType.Validation,
            UnauthorizedAccessException => ErrorType.Authorization,
            OperationCanceledException => ErrorType.Cancellation,
            NotImplementedException => ErrorType.System,
            _ => ErrorType.System
        };

        return new Error(
            code ?? exception.GetType().Name.Replace("Exception", "").ToUpperInvariant(),
            exception.Message,
            errorType,
            exception.StackTrace,
            exception);
    }

    /// <summary>
    /// Create an aggregated error from multiple errors
    /// </summary>
    public static Error Aggregate(params Error[] errors)
    {
        if (errors == null || errors.Length == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        if (errors.Length == 1)
            return errors[0];

        var messages = errors.Select(e => e.Message).ToArray();
        var combinedMessage = string.Join("; ", messages);

        var metadata = new Dictionary<string, object>
        {
            ["ErrorCount"] = errors.Length,
            ["Errors"] = errors.Select(e => new { e.Code, e.Message, e.Type }).ToArray()
        };

        return new Error(
            "AGGREGATE_ERROR",
            $"Multiple errors occurred: {combinedMessage}",
            ErrorType.Aggregate,
            string.Join("\n", errors.Select(e => $"- {e.Code}: {e.Message}")),
            metadata: metadata);
    }

    /// <summary>
    /// Create error with custom metadata
    /// </summary>
    public static Error WithMetadata(
        string code,
        string message,
        ErrorType type,
        Dictionary<string, object> metadata)
    {
        return new Error(code, message, type, metadata: metadata);
    }

    #endregion

    #region Fluent Configuration

    /// <summary>
    /// Add details to the error
    /// </summary>
    public Error WithDetails(string details)
    {
        return this with { Details = details };
    }

    /// <summary>
    /// Add metadata to the error
    /// </summary>
    public Error WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }

    /// <summary>
    /// Add exception context to the error
    /// </summary>
    public Error WithException(Exception exception)
    {
        return this with { Exception = exception };
    }

    #endregion

    public override string ToString()
    {
        var result = $"[{Type}] {Code}: {Message}";
        if (!string.IsNullOrEmpty(Details))
            result += $" (Details: {Details})";
        return result;
    }
}

/// <summary>
/// Error type enumeration for categorization and handling
/// </summary>
public enum ErrorType
{
    Validation = 1,
    BusinessRule = 2,
    Aggregate = 3,
    NotFound = 4,
    Conflict = 5,
    Authorization = 6,
    Cancellation = 7,
    System = 8
}
```

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
    /// LINQ Where operator - filters success values based on predicate.
    /// Preserves original error context or allows custom error specification.
    /// </summary>
    public Result<T> Where(Func<T, bool> predicate, Error? customError = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        if (IsFailure)
            return this; // Preserve original error
            
        return predicate(_value!) 
            ? this 
            : Result<T>.Failure(customError ?? Error.Validation("Predicate condition not met"));
    }
    
    /// <summary>
    /// LINQ Where operator overload with custom error message
    /// </summary>
    public Result<T> Where(Func<T, bool> predicate, string errorMessage)
    {
        return Where(predicate, Error.Validation(errorMessage));
    }
    
    /// <summary>
    /// LINQ Where operator overload with error factory
    /// </summary>
    public Result<T> Where(Func<T, bool> predicate, Func<T, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        
        if (IsFailure)
            return this; // Preserve original error
            
        return predicate(_value!) 
            ? this 
            : Result<T>.Failure(errorFactory(_value!));
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
    /// Convert to Option - Success becomes Some, Failure becomes None
    /// </summary>
    public Option<T> ToOption()
    {
        return IsSuccess ? Option<T>.Some(_value!) : Option<T>.None();
    }
    
    /// <summary>
    /// Convert to Option with error handling callback
    /// </summary>
    public Option<T> ToOption(Action<Error> onError)
    {
        ArgumentNullException.ThrowIfNull(onError);
        
        if (IsFailure)
            onError(_error!);
            
        return ToOption();
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
    
    public async Task<TResult> MatchAsync<TResult>(
        Func<Task<TResult>> success,
        Func<Error, Task<TResult>> failure,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        
        return IsSuccess 
            ? await success().ConfigureAwait(false)
            : await failure(_error!).ConfigureAwait(false);
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
    /// Convert to Result with default error
    /// </summary>
    public Result<T> ToResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return _hasValue 
            ? Result<T>.Success(_value!) 
            : Result<T>.Failure(error);
    }
    
    /// <summary>
    /// Convert to Result with error message
    /// </summary>
    public Result<T> ToResult(string errorMessage)
    {
        return ToResult(Error.Validation(errorMessage));
    }
    
    /// <summary>
    /// Convert to Result with error factory
    /// </summary>
    public Result<T> ToResult(Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return _hasValue 
            ? Result<T>.Success(_value!) 
            : Result<T>.Failure(errorFactory());
    }
    
    /// <summary>
    /// Convert to Result with conditional error
    /// </summary>
    public Result<T> ToResult<TContext>(TContext context, Func<TContext, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return _hasValue 
            ? Result<T>.Success(_value!) 
            : Result<T>.Failure(errorFactory(context));
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
        return _hasValue ? [_value!] : [];
    }
    
    /// <summary>
    /// Convert to list (0 or 1 element)
    /// </summary>
    public List<T> ToList()
    {
        return _hasValue ? [_value!] : [];
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

### 1.4 Result ⇄ Option Bridge Extensions

**File:** `Core/Functional/Extensions/ConversionExtensions.cs`

```csharp
namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Bridge extensions for seamless Result ⇄ Option conversions
/// </summary>
public static class ConversionExtensions
{
    #region Result<T> Extensions

    /// <summary>
    /// Convert Result<T> to Option<T> (Success → Some, Failure → None)
    /// </summary>
    public static Option<T> ToOption<T>(this Result<T> result)
    {
        return result.IsSuccess ? Option<T>.Some(result.Value) : Option<T>.None();
    }

    /// <summary>
    /// Convert Result<T> to Option<T> with error callback
    /// </summary>
    public static Option<T> ToOption<T>(this Result<T> result, Action<Error> onError)
    {
        ArgumentNullException.ThrowIfNull(onError);
        
        if (result.IsFailure)
            onError(result.Error);
            
        return result.ToOption();
    }

    /// <summary>
    /// Convert Result<T> to Option<T> with error logging
    /// </summary>
    public static Option<T> ToOption<T>(this Result<T> result, ILogger logger, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        
        if (result.IsFailure)
            logger.LogWarning("Result conversion to Option failed: {Message} - {Error}", 
                message ?? "Conversion", result.Error);
            
        return result.ToOption();
    }

    #endregion

    #region Option<T> Extensions

    /// <summary>
    /// Convert Option<T> to Result<T> with default error
    /// </summary>
    public static Result<T> ToResult<T>(this Option<T> option, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return option.IsSome 
            ? Result<T>.Success(option.Value) 
            : Result<T>.Failure(error);
    }

    /// <summary>
    /// Convert Option<T> to Result<T> with error message
    /// </summary>
    public static Result<T> ToResult<T>(this Option<T> option, string errorMessage)
    {
        return option.ToResult(Error.Validation(errorMessage));
    }

    /// <summary>
    /// Convert Option<T> to Result<T> with error factory
    /// </summary>
    public static Result<T> ToResult<T>(this Option<T> option, Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return option.IsSome 
            ? Result<T>.Success(option.Value) 
            : Result<T>.Failure(errorFactory());
    }

    /// <summary>
    /// Convert Option<T> to Result<T> with contextual error
    /// </summary>
    public static Result<T> ToResult<T, TContext>(
        this Option<T> option, 
        TContext context, 
        Func<TContext, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return option.IsSome 
            ? Result<T>.Success(option.Value) 
            : Result<T>.Failure(errorFactory(context));
    }

    /// <summary>
    /// Convert Option<T> to Result<T> with NotFound error for entities
    /// </summary>
    public static Result<T> ToResultNotFound<T>(
        this Option<T> option, 
        string entityName, 
        object? identifier = null)
    {
        var message = identifier != null 
            ? $"{entityName} with identifier '{identifier}' was not found"
            : $"{entityName} was not found";
            
        return option.ToResult(Error.NotFound(message));
    }

    #endregion

    #region Async Extensions

    /// <summary>
    /// Convert Task<Result<T>> to Task<Option<T>>
    /// </summary>
    public static async Task<Option<T>> ToOptionAsync<T>(this Task<Result<T>> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        var result = await resultTask.ConfigureAwait(false);
        return result.ToOption();
    }

    /// <summary>
    /// Convert Task<Option<T>> to Task<Result<T>>
    /// </summary>
    public static async Task<Result<T>> ToResultAsync<T>(this Task<Option<T>> optionTask, Error error)
    {
        ArgumentNullException.ThrowIfNull(optionTask);
        ArgumentNullException.ThrowIfNull(error);
        
        var option = await optionTask.ConfigureAwait(false);
        return option.ToResult(error);
    }

    /// <summary>
    /// Convert Task<Option<T>> to Task<Result<T>> with error factory
    /// </summary>
    public static async Task<Result<T>> ToResultAsync<T>(
        this Task<Option<T>> optionTask, 
        Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(optionTask);
        ArgumentNullException.ThrowIfNull(errorFactory);
        
        var option = await optionTask.ConfigureAwait(false);
        return option.ToResult(errorFactory);
    }

    #endregion

    #region Validation Extensions

    /// <summary>
    /// Convert Result<T> to Validation<T>
    /// </summary>
    public static Validation<T> ToValidation<T>(this Result<T> result)
    {
        return result.IsSuccess 
            ? Validation<T>.Valid(result.Value)
            : Validation<T>.Invalid(result.Error);
    }

    /// <summary>
    /// Convert Option<T> to Validation<T>
    /// </summary>
    public static Validation<T> ToValidation<T>(this Option<T> option, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return option.IsSome 
            ? Validation<T>.Valid(option.Value)
            : Validation<T>.Invalid(error);
    }

    #endregion

    #region Collection Extensions

    /// <summary>
    /// Convert sequence of Results to Option of sequence (fail-fast)
    /// </summary>
    public static Option<IEnumerable<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        var list = new List<T>();
        foreach (var result in results)
        {
            if (result.IsFailure)
                return Option<IEnumerable<T>>.None();
            list.Add(result.Value);
        }
        return Option<IEnumerable<T>>.Some(list);
    }

    /// <summary>
    /// Convert sequence of Options to Option of sequence (fail-fast)
    /// </summary>
    public static Option<IEnumerable<T>> Sequence<T>(this IEnumerable<Option<T>> options)
    {
        var list = new List<T>();
        foreach (var option in options)
        {
            if (option.IsNone)
                return Option<IEnumerable<T>>.None();
            list.Add(option.Value);
        }
        return Option<IEnumerable<T>>.Some(list);
    }

    /// <summary>
    /// Traverse with Result (map then sequence)
    /// </summary>
    public static Option<IEnumerable<TResult>> Traverse<T, TResult>(
        this IEnumerable<T> source, 
        Func<T, Result<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return source.Select(mapper).Sequence();
    }

    /// <summary>
    /// Traverse with Option (map then sequence)
    /// </summary>
    public static Option<IEnumerable<TResult>> Traverse<T, TResult>(
        this IEnumerable<T> source, 
        Func<T, Option<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return source.Select(mapper).Sequence();
    }

    #endregion
}
```

### 1.5 Validation<T> for Error Accumulation

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

### 1.6 Enhanced StrongId Implementation

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

### 1.6.1 Enhanced StrongId JSON Converter

**File:** `Core/Domain/Primitives/StrongIdJsonConverterFactory.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Robust JSON converter factory for strongly-typed identifiers.
/// Handles null values, complex constructors, and provides comprehensive error handling.
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
    private static readonly ConcurrentDictionary<Type, Func<TValue, TStrongId>> _factoryCache = new();
    
    public override TStrongId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle null values gracefully
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException($"Cannot deserialize null value to StrongId type {typeToConvert.Name}");
        }
        
        try
        {
            var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            return CreateInstance(typeToConvert, value);
        }
        catch (JsonException)
        {
            throw; // Re-throw JSON exceptions
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to deserialize {typeToConvert.Name}: {ex.Message}", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, TStrongId value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        
        JsonSerializer.Serialize(writer, value.Value, options);
    }

    /// <summary>
    /// Creates StrongId instance using cached factory with robust constructor resolution
    /// </summary>
    private static TStrongId CreateInstance(Type strongIdType, TValue value)
    {
        var factory = _factoryCache.GetOrAdd(strongIdType, type =>
        {
            // Try multiple constructor resolution strategies
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(c => c.GetParameters().Length == 1)
                .Where(c => c.GetParameters()[0].ParameterType == typeof(TValue))
                .OrderBy(c => c.IsPublic ? 0 : 1) // Prefer public constructors
                .ToArray();

            if (constructors.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for {type.Name} that accepts {typeof(TValue).Name}");
            }

            var constructor = constructors[0];
            
            // Create compiled factory for performance
            var parameter = Expression.Parameter(typeof(TValue), "value");
            var newExpression = Expression.New(constructor, parameter);
            var lambda = Expression.Lambda<Func<TValue, TStrongId>>(newExpression, parameter);
            
            return lambda.Compile();
        });

        return factory(value);
    }
}

/// <summary>
/// Extension methods for StrongId JSON configuration
/// </summary>
public static class StrongIdJsonExtensions
{
    /// <summary>
    /// Configure JSON options to use StrongId converters
    /// </summary>
    public static JsonSerializerOptions AddStrongIdSupport(this JsonSerializerOptions options)
    {
        options.Converters.Add(new StrongIdJsonConverterFactory());
        return options;
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
            options.SerializerOptions.AddStrongIdSupport();
        });
        
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.AddStrongIdSupport();
        });
        
        return services;
    }
}
```

### 1.7 MediatR Integration Enhancements

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
            var response = await next().ConfigureAwait(false);
            
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
                FailureFactory = error => (T)failureMethod.Invoke(null, [error])!;
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
- [ ] Implement Error contract with all factory methods
- [ ] Implement Unit type
- [ ] Complete Result<T> with enhanced Where logic and all operations
- [ ] Implement Option<T> monad
- [ ] Add comprehensive Option ⇄ Result bridge helpers
- [ ] Add Validation<T> for error accumulation
- [ ] Update all existing code to use new types

### Week 2: StrongId Enhancement
- [ ] Enhanced StrongId base classes
- [ ] Robust JSON serialization support with null handling
- [ ] Constructor resolution improvements
- [ ] Example implementations (ConversationId, UserId, etc.)
- [ ] Migration of existing IDs

### Week 3: MediatR Integration
- [ ] ResultPipelineBehavior implementation with ConfigureAwait(false)
- [ ] ValidationBehavior updates
- [ ] Extension methods for configuration
- [ ] Complete pipeline testing

---

## 🎯 Success Criteria

Epic 1 is complete when:

1. ✅ **All functional types** implement complete monadic operations with ConfigureAwait(false)
2. ✅ **Zero null references** in Result/Option usage
3. ✅ **Complete Error contract** with comprehensive factory methods
4. ✅ **Enhanced Where logic** preserving error context
5. ✅ **Complete Option ⇄ Result bridge** helpers implemented
6. ✅ **StrongId types** replace primitive obsession with robust JSON handling
7. ✅ **MediatR behaviors** integrate seamlessly
8. ✅ **100% test coverage** for functional operations
9. ✅ **Performance benchmarks** meet requirements
10. ✅ **Monad laws** verified through property-based testing

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

[Property]
public Property Result_Where_Error_Preservation()
{
    return Prop.ForAll<string>(errorMessage =>
    {
        var originalError = Error.Validation(errorMessage);
        var result = Result<int>.Failure(originalError);
        var filtered = result.Where(x => x > 0);
        
        return filtered.IsFailure && filtered.Error.Equals(originalError);
    });
}
```

---

## 🚀 Usage Examples

### Railway-Oriented Programming with Enhanced Error Handling
```csharp
// Command handler with complete error handling and proper async patterns
public async Task<Result<ConversationId>> Handle(
    CreateConversationCommand command,
    CancellationToken ct)
{
    return await ConversationTitle.Create(command.Title)
        .Bind(title => Conversation.Create(command.UserId, title))
        .BindAsync(async conv => 
        {
            await _repository.AddAsync(conv, ct).ConfigureAwait(false);
            return Result<Conversation>.Success(conv);
        }, ct)
        .Map(conv => conv.Id)
        .TapError(error => _logger.LogError("Creation failed: {Error}", error));
}
```

### Option<T> for Null Safety with Enhanced Conversions
```csharp
// Repository with Option<T> returns
public async Task<Option<User>> FindByEmailAsync(Email email, CancellationToken ct)
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Email == email, ct)
        .ConfigureAwait(false);
        
    return Option<User>.From(user);
}

// Query handler with safe null handling and enhanced conversion
public async Task<Result<UserDto>> Handle(GetUserQuery query, CancellationToken ct)
{
    return await _repository
        .FindByEmailAsync(query.Email, ct)
        .ToResultNotFound("User", query.Email.Value)
        .MapAsync(async user => await MapToDto(user, ct).ConfigureAwait(false), ct);
}
```

### Enhanced Where Logic with Error Context
```csharp
// Using enhanced Where with custom error context
public Result<User> ValidateUserAge(User user)
{
    return Result<User>.Success(user)
        .Where(
            u => u.Age >= 18, 
            user => Error.BusinessRule(
                $"User {user.Name} is {user.Age} years old, must be 18+",
                "MINIMUM_AGE_VIOLATION"));
}

// Preserving original error context
public Result<string> ProcessData(Result<string> input)
{
    return input
        .Where(data => data.Length > 0) // Uses default validation error
        .Where(data => !data.Contains("invalid")); // Chain multiple conditions
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
        : Validation<int>.Invalid(Error.BusinessRule("User must be 18 or older"));
    
    return Validation<CreateUserCommand>.Combine(
        emailValidation,
        nameValidation,
        ageValidation)
        .Map(_ => command);
}
```

### Complete Error Contract Usage
```csharp
// Comprehensive error handling with typed error contract
public async Task<Result<Order>> ProcessOrderAsync(ProcessOrderCommand command)
{
    try
    {
        var validationResult = await ValidateOrderAsync(command);
        if (validationResult.IsFailure)
            return validationResult.Error; // Validation errors
            
        var order = await _repository.GetByIdAsync(command.OrderId);
        if (order is null)
            return Error.NotFound($"Order {command.OrderId} not found");
            
        if (order.Status != OrderStatus.Pending)
            return Error.BusinessRule(
                "Order cannot be processed", 
                "ORDER_INVALID_STATE",
                $"Order is in {order.Status} state");
            
        await ProcessPaymentAsync(order);
        return Result<Order>.Success(order);
    }
    catch (OperationCanceledException)
    {
        return Error.Cancelled("Order processing was cancelled");
    }
    catch (UnauthorizedAccessException ex)
    {
        return Error.Unauthorized("Insufficient permissions", details: ex.Message);
    }
    catch (Exception ex)
    {
        return Error.FromException(ex)
            .WithMetadata("OrderId", command.OrderId)
            .WithMetadata("UserId", command.UserId);
    }
}
```

---

**END OF EPIC 1: FUNCTIONAL FOUNDATION**