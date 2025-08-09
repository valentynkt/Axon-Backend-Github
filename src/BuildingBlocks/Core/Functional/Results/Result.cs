using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Result<T>: immutable, allocation-light success/failure container.
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

    public bool IsSuccess => _state == ResultState.Success;
    public bool IsFailure => _state == ResultState.Failure;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access value of failed result: {_error}");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access error of successful result");

    public TResult Match<TResult>(Func<TResult> success, Func<Error, TResult> failure)
    {
        throw new NotImplementedException();
    }

    #region Factories
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
        => value is not null ? Success(value) : Failure(error);
    #endregion

    #region Match
    public TResult Match<TResult>(Func<T, TResult> success, Func<Error, TResult> failure) => _state switch
    {
        ResultState.Success => success(_value!),
        ResultState.Failure => failure(_error!),
        _ => throw new InvalidOperationException($"Invalid state: {_state}")
    };

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

    #region Map / Where
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess ? Result<TNew>.Success(mapper(_value!)) : Result<TNew>.Failure(_error!);
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (!IsSuccess) return Result<TNew>.Failure(_error!);

        try
        {
            var v = await mapper(_value!).ConfigureAwait(false);
            return Result<TNew>.Success(v);
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

    public Result<T> Where(Func<T, bool> predicate, Error? customError = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (IsFailure) return this;
        return predicate(_value!) ? this : Result<T>.Failure(customError ?? Error.Validation("Predicate condition not met"));
    }
    #endregion

    #region Bind
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder(_value!) : Result<TNew>.Failure(_error!);
    }

    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (!IsSuccess) return Result<TNew>.Failure(_error!);

        try { return await binder(_value!).ConfigureAwait(false); }
        catch (OperationCanceledException) { return Result<TNew>.Failure(Error.Cancelled()); }
        catch (Exception ex) { return Result<TNew>.Failure(Error.FromException(ex)); }
    }
    #endregion

    #region Recovery / Defaults
    public Result<T> Recover(Func<Error, T> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return IsFailure ? Result<T>.Success(recovery(_error!)) : this;
    }

    public Result<T> RecoverWith(Func<Error, Result<T>> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return IsFailure ? recovery(_error!) : this;
    }

    public T GetOrElse(T defaultValue) => IsSuccess ? _value! : defaultValue;

    public T GetOrElse(Func<Error, T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return IsSuccess ? _value! : defaultFactory(_error!);
    }
    #endregion

    #region Side effects
    public Result<T> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsSuccess) action(_value!);
        return this;
    }

    public Result<T> TapError(Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsFailure) action(_error!);
        return this;
    }
    #endregion

    #region Conversions / LINQ
    public Option<T> ToOption() => IsSuccess ? Option<T>.Some(_value!) : Option<T>.None();

    public Option<T> ToOption(Action<Error> onError)
    {
        ArgumentNullException.ThrowIfNull(onError);
        if (IsFailure) onError(_error!);
        return ToOption();
    }

    public T? ToNullable() => IsSuccess ? _value : default;

    public Result<TNew> Select<TNew>(Func<T, TNew> selector) => Map(selector);
    #endregion

    #region Operators
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
    public static bool operator true(Result<T> r) => r.IsSuccess;
    public static bool operator false(Result<T> r) => r.IsFailure;
    #endregion
}

/// <summary>Non-generic Result for operations without values.</summary>
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

    public Error Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access error of successful result");

    public static Result Success() => new(ResultState.Success);
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(ResultState.Failure, error);
    }

    public TResult Match<TResult>(Func<TResult> success, Func<Error, TResult> failure) => _state switch
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
        return IsSuccess ? Result<T>.Success(mapper()) : Result<T>.Failure(_error!);
    }

    public Result<T> Bind<T>(Func<Result<T>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder() : Result<T>.Failure(_error!);
    }

    public Result Tap(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsSuccess) action();
        return this;
    }

    public Result TapError(Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsFailure) action(_error!);
        return this;
    }

    public Result Recover() => IsFailure ? Success() : this;
    public Result RecoverWith(Func<Error, Result> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return IsFailure ? recovery(_error!) : this;
    }

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Internal state — centralized here to avoid file duplication.</summary>
internal enum ResultState : byte
{
    Success = 1,
    Failure = 2
}
