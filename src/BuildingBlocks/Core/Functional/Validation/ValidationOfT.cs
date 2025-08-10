using System.ComponentModel;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Validation;

/// <summary>
/// Validation applicative that accumulates all errors (does not short-circuit).
/// Keep instance behavior here; factories & combinators live on the non-generic <see cref="Validation"/> façade.
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

    public bool IsValid  => _errors.Count == 0;
    public bool IsInvalid => _errors.Count > 0;

    public T Value => IsValid
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot access value of invalid Validation. Errors: {string.Join(", ", _errors)}");

    public IReadOnlyList<Error> Errors => _errors.AsReadOnly();

    #region Instance (functor/applicative) operations

    public Validation<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsValid
            ? Validation.Valid(mapper(_value!))
            : Validation.Invalid<TNew>(_errors);
    }

    /// <summary>Apply a wrapped function, accumulating errors.</summary>
    public Validation<TResult> Apply<TResult>(Validation<Func<T, TResult>> fn)
    {
        if (fn.IsInvalid && IsInvalid)
        {
            var allErrors = fn._errors.Concat(_errors).ToList();
            // Allowed: generic types may access private members of other constructed instances
            return new Validation<TResult>(default, allErrors);
        }

        if (fn.IsInvalid)
            return new Validation<TResult>(default, fn._errors);

        if (IsInvalid)
            return new Validation<TResult>(default, _errors);

        return Validation.Valid(fn._value!(_value!));
    }

    #endregion

    #region Conversions

    public Result<T> ToResult()
        => IsValid ? Result<T>.Success(_value!) : Result<T>.Failure(_errors.First());

    public Result<T> ToResultWithAggregatedError()
        => IsValid ? Result<T>.Success(_value!) : Result<T>.Failure(Error.Aggregate(_errors.ToArray()));

    #endregion

    #region Internal factories used by the façade (keep off the public surface)

    internal static Validation<T> CreateValid(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Validation<T>(value, new List<Error>());
    }

    internal static Validation<T> CreateInvalid(IEnumerable<Error> errors)
    {
        var list = errors?.ToList() ?? throw new ArgumentNullException(nameof(errors));
        if (list.Count == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        return new Validation<T>(default, list);
    }

    #endregion
}
