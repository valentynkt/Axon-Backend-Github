using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Validation;

/// <summary>
/// Non-generic façade for creating and combining <see cref="ValidationResult{T}"/> values.
/// Keeps factory and combinator methods off the generic type to satisfy CA1000.
/// </summary>
public static class Validation
{
    /// <summary>Create a valid validation result.</summary>
    public static ValidationResult<T> Valid<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ValidationResult<T>.CreateValid(value);
    }

    /// <summary>Create an invalid validation result from one or more errors.</summary>
    public static ValidationResult<T> Invalid<T>(params Error[] errors)
    {
        if (errors == null || errors.Length == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        return ValidationResult<T>.CreateInvalid(errors);
    }

    /// <summary>Create an invalid validation result from an error sequence.</summary>
    public static ValidationResult<T> Invalid<T>(IEnumerable<Error> errors)
    {
        var list = errors?.ToList() ?? throw new ArgumentNullException(nameof(errors));
        if (list.Count == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        return ValidationResult<T>.CreateInvalid(list);
    }

    /// <summary>Combine two validation results, accumulating all errors.</summary>
    public static ValidationResult<(T1, T2)> Combine<T1, T2>(ValidationResult<T1> v1, ValidationResult<T2> v2)
    {
        var errors = new List.ErrorList();
        if (v1.IsInvalid) errors.AddRange(v1.Errors);
        if (v2.IsInvalid) errors.AddRange(v2.Errors);

        if (errors.Count > 0)
            return Invalid<(T1, T2)>(errors);

        return Valid((v1.Value, v2.Value));
    }

    /// <summary>Combine three validation results, accumulating all errors.</summary>
    public static ValidationResult<(T1, T2, T3)> Combine<T1, T2, T3>(ValidationResult<T1> v1, ValidationResult<T2> v2, ValidationResult<T3> v3)
    {
        var errors = new List.ErrorList();
        if (v1.IsInvalid) errors.AddRange(v1.Errors);
        if (v2.IsInvalid) errors.AddRange(v2.Errors);
        if (v3.IsInvalid) errors.AddRange(v3.Errors);

        if (errors.Count > 0)
            return Invalid<(T1, T2, T3)>(errors);

        return Valid((v1.Value, v2.Value, v3.Value));
    }

    private static class List
    {
        internal sealed class ErrorList : List<Error>;
    }
}
