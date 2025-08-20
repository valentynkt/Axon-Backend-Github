// /BuildingBlocks/Application/Validation/Extensions/ValidationExtensions.cs
#nullable enable
using FluentValidation;
using CSharpFunctionalExtensions;

namespace BuildingBlocks.Application.Validation.Extensions;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, Guid> NotEmptyGuid<T>(this IRuleBuilder<T, Guid> ruleBuilder) =>
        ruleBuilder
            .Must(id => id != Guid.Empty)
            .WithErrorCode("VAL.GUID.EMPTY")
            .WithMessage("'{PropertyName}' cannot be empty.");

    public static IRuleBuilderOptions<T, Guid?> NotEmptyGuid<T>(this IRuleBuilder<T, Guid?> ruleBuilder) =>
        ruleBuilder
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithErrorCode("VAL.GUID.EMPTY")
            .WithMessage("'{PropertyName}' cannot be empty.");

    /// <summary>
    /// Validate using a domain factory that returns Result&lt;TDomain&gt; (CFE).
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidDomainObject<T, TDomain>(
        this IRuleBuilder<T, string> ruleBuilder,
        Func<string, Result<TDomain>> createFunction)
        where TDomain : class =>
        ruleBuilder
            .Must(value => string.IsNullOrEmpty(value) || createFunction(value).IsSuccess)
            .WithErrorCode("VAL.DOMAIN.INVALID")
            .WithMessage("'{PropertyName}' is not valid.");

    /// <summary>
    /// For structs / strongly-typed IDs: ensure not default(T).
    /// </summary>
    public static IRuleBuilderOptions<T, TValue> NotDefault<T, TValue>(
        this IRuleBuilder<T, TValue> ruleBuilder)
        where TValue : struct =>
        ruleBuilder
            .Must(v => !EqualityComparer<TValue>.Default.Equals(v, default))
            .WithErrorCode("VAL.DEFAULT")
            .WithMessage("'{PropertyName}' cannot be the default value.");

    public static IRuleBuilderOptions<T, string> ContentLength<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        int minLength,
        int maxLength) =>
        ruleBuilder
            .Must(content =>
            {
                if (string.IsNullOrWhiteSpace(content)) return minLength == 0;
                var trimmed = content.Trim();
                return trimmed.Length >= minLength && trimmed.Length <= maxLength;
            })
            .WithErrorCode("VAL.CONTENT.LENGTH")
            .WithMessage($"'{{PropertyName}}' must be between {minLength} and {maxLength} characters after trimming.");

    public static IRuleBuilderOptions<T, string> NotEmptyOrWhitespace<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("VAL.STRING.EMPTY_OR_WHITESPACE")
            .WithMessage("'{PropertyName}' cannot be empty or contain only whitespace.");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> ruleBuilder, int maxPageSize = 100) =>
        ruleBuilder
            .GreaterThan(0)
            .LessThanOrEqualTo(maxPageSize)
            .WithErrorCode("VAL.PAGINATION.INVALID_SIZE")
            .WithMessage($"'{{PropertyName}}' must be between 1 and {maxPageSize}.");

    public static IRuleBuilderOptions<T, int> ValidPageNumber<T>(this IRuleBuilder<T, int> ruleBuilder) =>
        ruleBuilder
            .GreaterThanOrEqualTo(1)
            .WithErrorCode("VAL.PAGINATION.INVALID_PAGE")
            .WithMessage("'{PropertyName}' must be 1 or greater.");
}
