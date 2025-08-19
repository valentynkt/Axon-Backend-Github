using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Result&lt;T&gt;: immutable success/failure container with comprehensive error handling.
/// Static factories are in <see cref="Results"/> to satisfy CA1000.
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

    // Implements IResult.Match (non-generic)
    public TResult Match<TResult>(Func<TResult> success, Func<Error, TResult> failure)
    {
        ArgumentNullException.ThrowIfNull(success);
        ArgumentNullException.ThrowIfNull(failure);
        return IsSuccess ? success() : failure(_error!);
    }

    #region Matching

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

    #region Mapping and Filtering

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess ? Result<TNew>.CreateSuccess(mapper(_value!)) : Result<TNew>.CreateFailure(_error!);
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (!IsSuccess) return Result<TNew>.CreateFailure(_error!);

        try
        {
            var v = await mapper(_value!).ConfigureAwait(false);
            return Result<TNew>.CreateSuccess(v);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.CreateFailure(Error.Cancelled());
        }
        catch (Exception ex)
        {
            return Result<TNew>.CreateFailure(Error.FromException(ex));
        }
    }

    public Result<T> Where(Func<T, bool> predicate, Error? customError = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (IsFailure) return this;
        return predicate(_value!) ? this : CreateFailure(customError ?? Error.Validation("Predicate condition not met"));
    }

    #endregion

    #region Bind

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder(_value!) : Result<TNew>.CreateFailure(_error!);
    }

    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (!IsSuccess) return Result<TNew>.CreateFailure(_error!);

        try { return await binder(_value!).ConfigureAwait(false); }
        catch (OperationCanceledException) { return Result<TNew>.CreateFailure(Error.Cancelled()); }
        catch (Exception ex) { return Result<TNew>.CreateFailure(Error.FromException(ex)); }
    }

    #endregion

    #region Recovery / Defaults

    public Result<T> Recover(Func<Error, T> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return IsFailure ? CreateSuccess(recovery(_error!)) : this;
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

    #region Conversions

    public Option<T> ToOption() => IsSuccess ? Option<T>.CreateSome(_value!) : Option<T>.CreateNone();

    public Option<T> ToOption(Action<Error> onError)
    {
        ArgumentNullException.ThrowIfNull(onError);
        if (IsFailure) onError(_error!);
        return ToOption();
    }

    public T? ToNullable() => IsSuccess ? _value : default;

    #endregion

    #region LINQ Support

    public Result<TNew> Select<TNew>(Func<T, TNew> selector) => Map(selector);

    public Result<TResult> SelectMany<TMid, TResult>(
        Func<T, Result<TMid>> binder,
        Func<T, TMid, TResult> projector)
    {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(projector);

        if (IsFailure) return Result<TResult>.CreateFailure(_error!);
        var mid = binder(_value!);
        return mid.IsFailure ? Result<TResult>.CreateFailure(mid.Error) : Result<TResult>.CreateSuccess(projector(_value!, mid.Value));
    }

    public Result<TNew> SelectMany<TNew>(Func<T, Result<TNew>> binder) => Bind(binder);

    #endregion

    #region Side Effects

    public Result<T> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsSuccess)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try { action(_value!); } catch { /* ignore side-effect failures */ }
#pragma warning restore CA1031
        }
        return this;
    }

    public Result<T> TapError(Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsFailure)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try { action(_error!); } catch { /* ignore side-effect failures */ }
#pragma warning restore CA1031
        }
        return this;
    }

    #endregion

    #region Flow Helpers

    public bool TryGetValue([NotNullWhen(true)] out T? value, out Error? error)
    {
        if (IsSuccess)
        {
            value = _value!;
            error = null;
            return true;
        }
        value = default;
        error = _error!;
        return false;
    }

    public void Deconstruct(out bool isSuccess, out T? value, out Error? error)
        => (isSuccess, value, error) = (IsSuccess, IsSuccess ? _value : default, _error);

    #endregion

    #region Internal Factories

    internal static Result<T> CreateSuccess(T value) => new(value);
    internal static Result<T> CreateFailure(Error error) => new(error);

    #region Public Static Factories



    /// <summary>
    /// Creates a successful Result with the provided value.
    /// </summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed Result with the provided error.
    /// </summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
    public static Result<T> Failure(Error error) => new(error);

    #endregion

    #endregion

    #region Operators

    public static implicit operator Result<T>(T value) => CreateSuccess(value);
    public static implicit operator Result<T>(Error error) => CreateFailure(error);
    public static bool operator true(Result<T> r) => r.IsSuccess;
    public static bool operator false(Result<T> r) => r.IsFailure;

    #endregion
}

/// <summary>Non-generic Result for operations without return values.</summary>
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

    public static Result<T> Map<T>(Func<T> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return Result<T>.CreateSuccess(mapper());
    }

    public Result<T> Bind<T>(Func<Result<T>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder() : Result<T>.CreateFailure(_error!);
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

/// <summary>Internal state enumeration to avoid duplication.</summary>
internal enum ResultState : byte
{
    Success = 1,
    Failure = 2
}
