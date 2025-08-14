using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Validation;

/// <summary>
/// Validation result that accumulates all errors (does not short-circuit).
/// Instance behavior is defined here; factories and combinators are on the <see cref="Validation"/> façade.
/// </summary>
public readonly record struct ValidationResult<T>
{
    private readonly T? _value;
    private readonly List<Error> _errors;

    private ValidationResult(T? value, List<Error> errors)
    {
        _value = value;
        _errors = errors ?? new List<Error>();
    }

    public bool IsValid  => _errors.Count == 0;
    public bool IsInvalid => _errors.Count > 0;

    public T Value => IsValid
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot access value of invalid ValidationResult. Errors: {string.Join(", ", _errors)}");

    public IReadOnlyList<Error> Errors => _errors.AsReadOnly();

    #region Functor and Applicative Operations

    public ValidationResult<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsValid
            ? Validation.Valid(mapper(_value!))
            : Validation.Invalid<TNew>(_errors);
    }

    public ValidationResult<TResult> Apply<TResult>(ValidationResult<Func<T, TResult>> fn)
    {
        if (fn.IsInvalid && IsInvalid)
        {
            var allErrors = fn._errors.Concat(_errors).ToList();
            return new ValidationResult<TResult>(default, allErrors);
        }

        if (fn.IsInvalid)
            return new ValidationResult<TResult>(default, fn._errors);

        if (IsInvalid)
            return new ValidationResult<TResult>(default, _errors);

        return Validation.Valid(fn._value!(_value!));
    }

    #endregion

    #region Conversions

    public Result<T> ToResult()
        => IsValid ? ResultFactory.Success(_value!) : ResultFactory.Failure<T>(_errors.First());

    public Result<T> ToResultWithAggregatedError()
        => IsValid ? ResultFactory.Success(_value!) : ResultFactory.Failure<T>(Error.Aggregate(_errors.ToArray()));

    #endregion

    #region Internal Factories

    internal static ValidationResult<T> CreateValid(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ValidationResult<T>(value, new List<Error>());
    }

    internal static ValidationResult<T> CreateInvalid(IEnumerable<Error> errors)
    {
        var list = errors?.ToList() ?? throw new ArgumentNullException(nameof(errors));
        if (list.Count == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        return new ValidationResult<T>(default, list);
    }

    #endregion
}
