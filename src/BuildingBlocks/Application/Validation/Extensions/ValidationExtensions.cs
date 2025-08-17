using FluentValidation;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Validation.Extensions;

/// <summary>
/// Custom FluentValidation extensions for common validation patterns
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Validates that a Guid is not empty
    /// </summary>
    public static IRuleBuilderOptions<T, Guid> NotEmptyGuid<T>(this IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
            .Must(id => id != Guid.Empty)
            .WithErrorCode("VAL.GUID.EMPTY")
            .WithMessage("'{PropertyName}' cannot be empty.");
    }

    /// <summary>
    /// Validates that a nullable Guid is not empty if it has a value
    /// </summary>
    public static IRuleBuilderOptions<T, Guid?> NotEmptyGuid<T>(this IRuleBuilder<T, Guid?> ruleBuilder)
    {
        return ruleBuilder
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithErrorCode("VAL.GUID.EMPTY")
            .WithMessage("'{PropertyName}' cannot be empty.");
    }

    /// <summary>
    /// Validates that a domain value object can be created successfully
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidDomainObject<T, TDomain>(
        this IRuleBuilder<T, string> ruleBuilder,
        Func<string, Result<TDomain>> createFunction)
        where TDomain : class
    {
        return ruleBuilder
            .Must(value => string.IsNullOrEmpty(value) || createFunction(value).IsSuccess)
            .WithErrorCode("VAL.DOMAIN.INVALID")
            .WithMessage("'{PropertyName}' is not valid.");
    }



    /// <summary>
    /// Validates content length with proper trimming
    /// </summary>
    public static IRuleBuilderOptions<T, string> ContentLength<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        int minLength,
        int maxLength)
    {
        return ruleBuilder
            .Must(content => 
            {
                if (string.IsNullOrWhiteSpace(content)) return minLength == 0;
                var trimmed = content.Trim();
                return trimmed.Length >= minLength && trimmed.Length <= maxLength;
            })
            .WithErrorCode("VAL.CONTENT.LENGTH")
            .WithMessage($"'{{PropertyName}}' must be between {minLength} and {maxLength} characters after trimming.");
    }

    /// <summary>
    /// Validates that a string contains meaningful content (not just whitespace)
    /// </summary>
    public static IRuleBuilderOptions<T, string> NotEmptyOrWhitespace<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("VAL.STRING.EMPTY_OR_WHITESPACE")
            .WithMessage("'{PropertyName}' cannot be empty or contain only whitespace.");
    }

    /// <summary>
    /// Validates pagination parameters
    /// </summary>
    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(
        this IRuleBuilder<T, int> ruleBuilder,
        int maxPageSize = 100)
    {
        return ruleBuilder
            .GreaterThan(0)
            .LessThanOrEqualTo(maxPageSize)
            .WithErrorCode("VAL.PAGINATION.INVALID_SIZE")
            .WithMessage($"'{{PropertyName}}' must be between 1 and {maxPageSize}.");
    }

    /// <summary>
    /// Validates page number
    /// </summary>
    public static IRuleBuilderOptions<T, int> ValidPageNumber<T>(this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(1)
            .WithErrorCode("VAL.PAGINATION.INVALID_PAGE")
            .WithMessage("'{PropertyName}' must be 1 or greater.");
    }
}