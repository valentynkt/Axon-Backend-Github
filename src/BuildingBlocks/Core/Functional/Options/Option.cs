using BuildingBlocks.Core.Functional.Results;

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