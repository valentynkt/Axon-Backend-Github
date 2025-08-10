using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Validation;

/// <summary>
/// Non-generic façade for creating and combining <see cref="Validation{T}"/> values.
/// Keeps factory & combinator methods off the generic type to satisfy CA1000.
/// </summary>
public static class Validation
{
    /// <summary>Create a valid validation.</summary>
    public static Validation<T> Valid<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Validation<T>.CreateValid(value);
    }

    /// <summary>Create an invalid validation from one or more errors.</summary>
    public static Validation<T> Invalid<T>(params Error[] errors)
    {
        if (errors == null || errors.Length == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        return Validation<T>.CreateInvalid(errors);
    }

    /// <summary>Create an invalid validation from an error sequence.</summary>
    public static Validation<T> Invalid<T>(IEnumerable<Error> errors)
    {
        var list = errors?.ToList() ?? throw new ArgumentNullException(nameof(errors));
        if (list.Count == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        return Validation<T>.CreateInvalid(list);
    }

    /// <summary>Combine two validations, accumulating all errors.</summary>
    public static Validation<(T1, T2)> Combine<T1, T2>(Validation<T1> v1, Validation<T2> v2)
    {
        var errors = new List.ErrorList();
        if (v1.IsInvalid) errors.AddRange(v1.Errors);
        if (v2.IsInvalid) errors.AddRange(v2.Errors);

        if (errors.Count > 0)
            return Invalid<(T1, T2)>(errors);

        return Valid((v1.Value, v2.Value));
    }

    /// <summary>Combine three validations, accumulating all errors.</summary>
    public static Validation<(T1, T2, T3)> Combine<T1, T2, T3>(Validation<T1> v1, Validation<T2> v2, Validation<T3> v3)
    {
        var errors = new List.ErrorList();
        if (v1.IsInvalid) errors.AddRange(v1.Errors);
        if (v2.IsInvalid) errors.AddRange(v2.Errors);
        if (v3.IsInvalid) errors.AddRange(v3.Errors);

        if (errors.Count > 0)
            return Invalid<(T1, T2, T3)>(errors);

        return Valid((v1.Value, v2.Value, v3.Value));
    }

    // Small helper so call sites stay tidy.
    private static class List
    {
        internal sealed class ErrorList : List<Error> { }
    }
}
