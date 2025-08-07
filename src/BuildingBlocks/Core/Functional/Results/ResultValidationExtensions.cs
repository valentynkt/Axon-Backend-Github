using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Extension methods for Result types that provide common validation patterns
/// </summary>
public static class ResultValidationExtensions
{
    /// <summary>
    /// Ensures the result value is not null
    /// </summary>
    public static Result<T> EnsureNotNull<T>(this Result<T> result, string? message = null) where T : class
    {
        if (result.IsFailure) return result;
        
        if (result.Value is null)
        {
            var errorMessage = message ?? $"Expected non-null value of type {typeof(T).Name}";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the result value satisfies a predicate
    /// </summary>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, string message)
    {
        if (result.IsFailure) return result;
        
        if (!predicate(result.Value))
        {
            return Result<T>.Failure(Error.Validation(message));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the result value satisfies a predicate with custom error
    /// </summary>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        if (result.IsFailure) return result;
        
        if (!predicate(result.Value))
        {
            return Result<T>.Failure(error);
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the result value satisfies a predicate with error factory
    /// </summary>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Func<T, Error> errorFactory)
    {
        if (result.IsFailure) return result;
        
        if (!predicate(result.Value))
        {
            return Result<T>.Failure(errorFactory(result.Value));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the string result is not null or empty
    /// </summary>
    public static Result<string> EnsureNotNullOrEmpty(this Result<string> result, string? message = null)
    {
        if (result.IsFailure) return result;
        
        if (string.IsNullOrEmpty(result.Value))
        {
            var errorMessage = message ?? "String value cannot be null or empty";
            return Result<string>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the string result is not null or whitespace
    /// </summary>
    public static Result<string> EnsureNotNullOrWhiteSpace(this Result<string> result, string? message = null)
    {
        if (result.IsFailure) return result;
        
        if (string.IsNullOrWhiteSpace(result.Value))
        {
            var errorMessage = message ?? "String value cannot be null or whitespace";
            return Result<string>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the string result has a minimum length
    /// </summary>
    public static Result<string> EnsureMinLength(this Result<string> result, int minLength, string? message = null)
    {
        if (result.IsFailure) return result;
        
        if (result.Value?.Length < minLength)
        {
            var errorMessage = message ?? $"String value must be at least {minLength} characters long";
            return Result<string>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the string result has a maximum length
    /// </summary>
    public static Result<string> EnsureMaxLength(this Result<string> result, int maxLength, string? message = null)
    {
        if (result.IsFailure) return result;
        
        if (result.Value?.Length > maxLength)
        {
            var errorMessage = message ?? $"String value cannot exceed {maxLength} characters";
            return Result<string>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the collection result is not empty
    /// </summary>
    public static Result<T> EnsureNotEmpty<T>(this Result<T> result, string? message = null) where T : System.Collections.IEnumerable
    {
        if (result.IsFailure) return result;
        
        if (!result.Value.Cast<object>().Any())
        {
            var errorMessage = message ?? "Collection cannot be empty";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the collection result has a minimum count
    /// </summary>
    public static Result<T> EnsureMinCount<T>(this Result<T> result, int minCount, string? message = null) where T : System.Collections.IEnumerable
    {
        if (result.IsFailure) return result;
        
        var count = result.Value.Cast<object>().Count();
        if (count < minCount)
        {
            var errorMessage = message ?? $"Collection must contain at least {minCount} items";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the collection result has a maximum count
    /// </summary>
    public static Result<T> EnsureMaxCount<T>(this Result<T> result, int maxCount, string? message = null) where T : System.Collections.IEnumerable
    {
        if (result.IsFailure) return result;
        
        var count = result.Value.Cast<object>().Count();
        if (count > maxCount)
        {
            var errorMessage = message ?? $"Collection cannot contain more than {maxCount} items";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the numeric result is within a specified range
    /// </summary>
    public static Result<T> EnsureInRange<T>(this Result<T> result, T min, T max, string? message = null) where T : IComparable<T>
    {
        if (result.IsFailure) return result;
        
        if (result.Value.CompareTo(min) < 0 || result.Value.CompareTo(max) > 0)
        {
            var errorMessage = message ?? $"Value must be between {min} and {max}";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the numeric result is greater than a minimum value
    /// </summary>
    public static Result<T> EnsureGreaterThan<T>(this Result<T> result, T minimum, string? message = null) where T : IComparable<T>
    {
        if (result.IsFailure) return result;
        
        if (result.Value.CompareTo(minimum) <= 0)
        {
            var errorMessage = message ?? $"Value must be greater than {minimum}";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the numeric result is greater than or equal to a minimum value
    /// </summary>
    public static Result<T> EnsureGreaterThanOrEqual<T>(this Result<T> result, T minimum, string? message = null) where T : IComparable<T>
    {
        if (result.IsFailure) return result;
        
        if (result.Value.CompareTo(minimum) < 0)
        {
            var errorMessage = message ?? $"Value must be greater than or equal to {minimum}";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the numeric result is less than a maximum value
    /// </summary>
    public static Result<T> EnsureLessThan<T>(this Result<T> result, T maximum, string? message = null) where T : IComparable<T>
    {
        if (result.IsFailure) return result;
        
        if (result.Value.CompareTo(maximum) >= 0)
        {
            var errorMessage = message ?? $"Value must be less than {maximum}";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Ensures the numeric result is less than or equal to a maximum value
    /// </summary>
    public static Result<T> EnsureLessThanOrEqual<T>(this Result<T> result, T maximum, string? message = null) where T : IComparable<T>
    {
        if (result.IsFailure) return result;
        
        if (result.Value.CompareTo(maximum) > 0)
        {
            var errorMessage = message ?? $"Value must be less than or equal to {maximum}";
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Validates the result value using Data Annotations
    /// </summary>
    public static Result<T> ValidateDataAnnotations<T>(this Result<T> result) where T : class
    {
        if (result.IsFailure) return result;

        var context = new ValidationContext(result.Value);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(result.Value, context, validationResults, true))
        {
            var errors = validationResults.Select(vr => vr.ErrorMessage ?? "Validation failed").ToList();
            var errorMessage = string.Join("; ", errors);
            return Result<T>.Failure(Error.Validation(errorMessage));
        }
        
        return result;
    }

    /// <summary>
    /// Combines multiple validation results into a single result
    /// </summary>
    public static Result<T> CombineValidations<T>(this Result<T> result, params Func<T, Result>[] validations)
    {
        if (result.IsFailure) return result;
        
        foreach (var validation in validations)
        {
            var validationResult = validation(result.Value);
            if (validationResult.IsFailure)
            {
                return Result<T>.Failure(validationResult.Error);
            }
        }
        
        return result;
    }
}