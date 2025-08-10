using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Tiny bridges between Result/Option and Validation (accumulates errors).
/// </summary>
public static class ResultValidationBridgeExtensions
{
    /// <summary>
    /// Convert Result to Validation (first error only for invalid case).
    /// </summary>
    public static Validation<T> ToValidation<T>(this Result<T> result)
        => result.IsSuccess
            ? Validation<T>.Valid(result.Value)
            : Validation<T>.Invalid(result.Error);

    /// <summary>
    /// Convert Option to Validation using the provided error when None.
    /// </summary>
    public static Validation<T> ToValidation<T>(this Option<T> option, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return option.IsSome
            ? Validation<T>.Valid(option.Value)
            : Validation<T>.Invalid(error);
    }
}