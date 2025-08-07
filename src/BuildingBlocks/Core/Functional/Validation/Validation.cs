using BuildingBlocks.Core.Functional.Results;

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