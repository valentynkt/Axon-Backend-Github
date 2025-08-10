using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Non-generic façade for creating <see cref="Result{T}"/> values
/// and working with the non-generic <see cref="Result"/>.
/// This keeps public static factories off the generic type to satisfy CA1000.
/// </summary>
public static class Results
{
    /// <summary>Create a successful <see cref="Result{T}"/>.</summary>
    public static Result<T> Success<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Result<T>.CreateSuccess(value);
    }

    /// <summary>Create a failed <see cref="Result{T}"/>.</summary>
    public static Result<T> Failure<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Result<T>.CreateFailure(error);
    }

    /// <summary>Create a <see cref="Result{T}"/> from a nullable value.</summary>
    public static Result<T> From<T>(T? value, Error error)
        => value is not null ? Success(value) : Failure<T>(error);

    /// <summary>Create a successful non-generic <see cref="Result"/>.</summary>
    public static Result Success() => Result.Success();

    /// <summary>Create a failed non-generic <see cref="Result"/>.</summary>
    public static Result Failure(Error error) => Result.Failure(error);
}