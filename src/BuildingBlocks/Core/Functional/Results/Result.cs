global using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Options;

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Complete Result monad for railway-oriented programming.
/// Thread-safe, immutable, and performance-optimized.
/// </summary>
public readonly record struct Result<T> : IResult<T>, IResult
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
    
    /// <summary>
    /// Explicit implementation of IResult.Match for non-generic interface compatibility
    /// </summary>
    TResult IResult.Match<TResult>(Func<TResult> success, Func<Error, TResult> failure)
    {
        return _state switch
        {
            ResultState.Success => success(),
            ResultState.Failure => failure(_error!),
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
    /// Chains operations that return Result&lt;T&gt;
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