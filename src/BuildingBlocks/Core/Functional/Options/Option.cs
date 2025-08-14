using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Options;

/// <summary>Option monad representing optional values with comprehensive LINQ support.</summary>
public readonly record struct Option<T> : IOption<T>
{
    private readonly T? _value;
    private readonly bool _hasValue;

    private Option(T? value, bool hasValue)
    {
        _value = value;
        _hasValue = hasValue;
    }

    #region Internal Factories

    internal static Option<T> CreateSome(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Option<T>(value, true);
    }

    internal static Option<T> CreateNone() => new(default, false);

    #endregion

    public bool IsSome => _hasValue;
    public bool IsNone => !_hasValue;
    public T Value => _hasValue ? _value! : throw new InvalidOperationException("Cannot access value of None");

    #region Matching

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

    #region Mapping, Binding, and Filtering
    public Option<TNew> Map<TNew>(Func<T, TNew> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return _hasValue ? Options.Some(map(_value!)) : Options.None<TNew>();
    }

    public async Task<Option<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!_hasValue) return Options.None<TNew>();
        return Options.Some(await map(_value!).ConfigureAwait(false));
    }

    public Option<TNew> Bind<TNew>(Func<T, Option<TNew>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _hasValue ? bind(_value!) : Options.None<TNew>();
    }

    public async Task<Option<TNew>> BindAsync<TNew>(Func<T, Task<Option<TNew>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        if (!_hasValue) return Options.None<TNew>();
        return await bind(_value!).ConfigureAwait(false);
    }

    public Option<T> Filter(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return !_hasValue ? this : (predicate(_value!) ? this : Options.None<T>());
    }
    #endregion

    #region Default Values

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
    
    public T[] ToArray() => _hasValue ? [_value!] : Array.Empty<T>();
    
    public List<T> ToList() => _hasValue ? [_value!] : [];
    #endregion

    #region LINQ Support

    public Option<TNew> Select<TNew>(Func<T, TNew> selector) => Map(selector);
    
    public Option<T> Where(Func<T, bool> predicate) => Filter(predicate);

    public Option<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Option<TIntermediate>> binder,
        Func<T, TIntermediate, TResult> projector)
    {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(projector);

        if (!_hasValue) return Options.None<TResult>();
        var mid = binder(_value!);
        return mid.IsSome ? Options.Some(projector(_value!, mid.Value)) : Options.None<TResult>();
    }

    public Option<TNew> SelectMany<TNew>(Func<T, Option<TNew>> binder) => Bind(binder);

    #endregion

    #region Flow Helpers

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = _hasValue ? _value : default;
        return _hasValue;
    }

    public void Deconstruct(out bool isSome, out T? value)
        => (isSome, value) = (_hasValue, _hasValue ? _value : default);

    #endregion

    #region Operators

    public static implicit operator Option<T>(T? value) => Options.From(value);
    public static bool operator true(Option<T> o) => o._hasValue;
    public static bool operator false(Option<T> o) => !o._hasValue;

    #endregion
}

/// <summary>Interface for option types.</summary>
public interface IOption<T>
{
    bool IsSome { get; }
    bool IsNone { get; }
    T Value { get; }
    TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none);
}

/// <summary>
/// Non-generic façade for creating <see cref="Option{T}"/> values.
/// Keeps factory methods off the generic type to satisfy CA1000.
/// </summary>
public static class Options
{
    /// <summary>Create a Some option with a value.</summary>
    public static Option<T> Some<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Option<T>.CreateSome(value);
    }

    /// <summary>Create a None option.</summary>
    public static Option<T> None<T>() => Option<T>.CreateNone();
    
    /// <summary>Create an option from a nullable value.</summary>
    public static Option<T> From<T>(T? value) => value is null ? None<T>() : Some(value);
    
    /// <summary>Create an option based on a condition.</summary>
    public static Option<T> When<T>(bool condition, T value) => condition ? Some(value) : None<T>();
    
    /// <summary>Create an option based on a condition with lazy evaluation.</summary>
    public static Option<T> When<T>(bool condition, Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return condition ? Some(factory()) : None<T>();
    }
}
