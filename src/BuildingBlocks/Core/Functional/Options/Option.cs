using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Options;

/// <summary>Option monad (Some/None) with LINQ support.</summary>
public readonly record struct Option<T> : IOption<T>
{
    private readonly T? _value;
    private readonly bool _hasValue;

    private Option(T? value, bool hasValue)
    {
        _value = value;
        _hasValue = hasValue;
    }

    #region Factories
    public static Option<T> Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Option<T>(value, true);
    }

    public static Option<T> None() => new(default, false);
    public static Option<T> From(T? value) => value is null ? None() : Some(value);
    public static Option<T> When(bool condition, T value) => condition ? Some(value) : None();
    public static Option<T> When(bool condition, Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return condition ? Some(factory()) : None();
    }
    #endregion

    public bool IsSome => _hasValue;
    public bool IsNone => !_hasValue;
    public T Value => _hasValue ? _value! : throw new InvalidOperationException("Cannot access value of None");

    #region Match
    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);
        return _hasValue ? some(_value!) : none();
    }

    public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> some, Func<Task<TResult>> none, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);
        ct.ThrowIfCancellationRequested();
        return _hasValue ? await some(_value!).ConfigureAwait(false) : await none().ConfigureAwait(false);
    }
    #endregion

    #region Map / Bind / Filter
    public Option<TNew> Map<TNew>(Func<T, TNew> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return _hasValue ? Option<TNew>.Some(map(_value!)) : Option<TNew>.None();
    }

    public async Task<Option<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> map, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!_hasValue) return Option<TNew>.None();
        return Option<TNew>.Some(await map(_value!).ConfigureAwait(false));
    }

    public Option<TNew> Bind<TNew>(Func<T, Option<TNew>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _hasValue ? bind(_value!) : Option<TNew>.None();
    }

    public async Task<Option<TNew>> BindAsync<TNew>(Func<T, Task<Option<TNew>>> bind, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bind);
        if (!_hasValue) return Option<TNew>.None();
        return await bind(_value!).ConfigureAwait(false);
    }

    public Option<T> Filter(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return !_hasValue ? this : (predicate(_value!) ? this : None());
    }
    #endregion

    #region Defaults
    public T GetOrElse(T defaultValue) => _hasValue ? _value! : defaultValue;
    public T GetOrElse(Func<T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return _hasValue ? _value! : defaultFactory();
    }
    public T GetOrThrow(Func<Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);
        return _hasValue ? _value! : throw exceptionFactory();
    }
    #endregion

    #region Conversions
    public Result<T> ToResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return _hasValue ? Result<T>.Success(_value!) : Result<T>.Failure(error);
    }
    public Result<T> ToResult(string message) => ToResult(Error.Validation(message));
    public Result<T> ToResult(Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return _hasValue ? Result<T>.Success(_value!) : Result<T>.Failure(errorFactory());
    }
    public Result<T> ToResult<TCtx>(TCtx ctx, Func<TCtx, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return _hasValue ? Result<T>.Success(_value!) : Result<T>.Failure(errorFactory(ctx));
    }

    public T? ToNullable() => _hasValue ? _value : default;
    public T[] ToArray() => _hasValue ? new[] { _value! } : Array.Empty<T>();
    public List<T> ToList() => _hasValue ? new List<T> { _value! } : new List<T>();
    #endregion

    #region LINQ support
    public Option<TNew> Select<TNew>(Func<T, TNew> selector) => Map(selector);
    public Option<T> Where(Func<T, bool> predicate) => Filter(predicate);

    /// <summary>
    /// LINQ SelectMany (monadic bind) with projector: from x in ... from y in ... select f(x,y)
    /// </summary>
    public Option<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Option<TIntermediate>> binder,
        Func<T, TIntermediate, TResult> projector)
    {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(projector);

        if (!_hasValue) return Option<TResult>.None();
        var mid = binder(_value!);
        return mid.IsSome ? Option<TResult>.Some(projector(_value!, mid.Value)) : Option<TResult>.None();
    }

    /// <summary>LINQ SelectMany overload (without projector).</summary>
    public Option<TNew> SelectMany<TNew>(Func<T, Option<TNew>> binder) => Bind(binder);
    #endregion

    #region Operators
    public static implicit operator Option<T>(T? value) => From(value);
    public static bool operator true(Option<T> o) => o._hasValue;
    public static bool operator false(Option<T> o) => !o._hasValue;
    #endregion
}

public interface IOption<T>
{
    bool IsSome { get; }
    bool IsNone { get; }
    T Value { get; }
    TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none);
}
