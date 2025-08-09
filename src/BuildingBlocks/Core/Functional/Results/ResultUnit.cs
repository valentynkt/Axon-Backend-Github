global using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Options;

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Non-generic Result for operations that don't return values
/// </summary>
public readonly record struct Result : IResult
{
    private readonly Error? _error;
    private readonly ResultState _state;
    
    private Result(ResultState state, Error? error = null)
    {
        _state = state;
        _error = error;
    }
    
    public bool IsSuccess => _state == ResultState.Success;
    public bool IsFailure => _state == ResultState.Failure;
    
    public Error Error => IsFailure 
        ? _error! 
        : throw new InvalidOperationException("Cannot access error of successful result");
    
    public static Result Success() => new(ResultState.Success);
    public static Result Failure(Error error) => new(ResultState.Failure, error);
    
    public TResult Match<TResult>(
        Func<TResult> success,
        Func<Error, TResult> failure) => _state switch
    {
        ResultState.Success => success(),
        ResultState.Failure => failure(_error!),
        _ => throw new InvalidOperationException($"Invalid state: {_state}")
    };
    
    public async Task<TResult> MatchAsync<TResult>(
        Func<Task<TResult>> success,
        Func<Error, Task<TResult>> failure,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        
        return _state switch
        {
            ResultState.Success => await success().ConfigureAwait(false),
            ResultState.Failure => await failure(_error!).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Invalid state: {_state}")
        };
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
    
    /// <summary>
    /// Execute action on failure without breaking the chain
    /// </summary>
    public Result TapError(Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (IsFailure)
            action(_error!);
            
        return this;
    }
    
    /// <summary>
    /// Execute action without breaking the chain
    /// </summary>
    public Result Tap(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (IsSuccess)
            action();
            
        return this;
    }
    
    /// <summary>
    /// Recover from failure with success
    /// </summary>
    public Result Recover()
    {
        return IsFailure ? Result.Success() : this;
    }
    
    /// <summary>
    /// Recover with another Result based on the error
    /// </summary>
    public Result RecoverWith(Func<Error, Result> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return IsFailure ? recovery(_error!) : this;
    }
    
    public static implicit operator Result(Error error)
    {
        return Failure(error);
    }
}