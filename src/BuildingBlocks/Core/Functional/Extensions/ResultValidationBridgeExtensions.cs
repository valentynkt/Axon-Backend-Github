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
    public static ValidationResult<T> ToValidation<T>(this Result<T> result)
        => result.IsSuccess
            ? ValidationResult<T>.CreateValid(result.Value)
            : ValidationResult<T>.CreateInvalid(new[] { result.Error });

    /// <summary>
    /// Convert Option to Validation using the provided error when None.
    /// </summary>
    public static ValidationResult<T> ToValidation<T>(this Option<T> option, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return option.IsSome
            ? ValidationResult<T>.CreateValid(option.Value)
            : ValidationResult<T>.CreateInvalid(new[] { error });
    }
}