using FluentValidation;

namespace BuildingBlocks.Web.Validation;

/// <summary>
/// Base validator for API DTOs with common validation patterns
/// </summary>
/// <typeparam name="T">DTO type to validate</typeparam>
public abstract class BaseDtoValidator<T> : AbstractValidator<T>
{
    protected BaseDtoValidator()
    {
        // Stop per rule and per class (keeps messages concise and fast)
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;
    }

    /// <summary>
    /// Validates a required string field with length constraints
    /// </summary>
    protected IRuleBuilderOptions<T, string> RequiredString<TProperty>(
        IRuleBuilder<T, string> ruleBuilder,
        int? maxLength = null,
        string? fieldName = null)
    {
        var rule = ruleBuilder
            .NotEmpty()
            .WithMessage($"{fieldName ?? "Field"} is required.")
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage($"{fieldName ?? "Field"} cannot be empty or whitespace.");

        if (maxLength.HasValue)
        {
            rule = rule.MaximumLength(maxLength.Value)
                .WithMessage($"{fieldName ?? "Field"} cannot exceed {maxLength.Value} characters.");
        }

        return rule;
    }

    /// <summary>
    /// Validates an optional string field with length constraints
    /// </summary>
    protected IRuleBuilderOptions<T, string?> OptionalString<TProperty>(
        IRuleBuilder<T, string?> ruleBuilder,
        int? maxLength = null,
        string? fieldName = null)
    {
        var rule = ruleBuilder;

        if (maxLength.HasValue)
        {
            rule = rule.MaximumLength(maxLength.Value)
                .WithMessage($"{fieldName ?? "Field"} cannot exceed {maxLength.Value} characters.");
        }

        return (IRuleBuilderOptions<T, string?>)rule;
    }

    /// <summary>
    /// Validates a Guid field
    /// </summary>
    protected IRuleBuilderOptions<T, Guid?> OptionalGuid(
        IRuleBuilder<T, Guid?> ruleBuilder,
        string? fieldName = null)
    {
        return ruleBuilder
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage($"{fieldName ?? "Id"}, when provided, cannot be empty.");
    }

    /// <summary>
    /// Validates a required Guid field
    /// </summary>
    protected IRuleBuilderOptions<T, Guid> RequiredGuid(
        IRuleBuilder<T, Guid> ruleBuilder,
        string? fieldName = null)
    {
        return ruleBuilder
            .Must(id => id != Guid.Empty)
            .WithMessage($"{fieldName ?? "Id"} is required and cannot be empty.");
    }

    /// <summary>
    /// Validates an email address
    /// </summary>
    protected IRuleBuilderOptions<T, string> EmailAddress(
        IRuleBuilder<T, string> ruleBuilder,
        string? fieldName = null)
    {
        return ruleBuilder
            .EmailAddress()
            .WithMessage($"{fieldName ?? "Email"} must be a valid email address.");
    }

    /// <summary>
    /// Validates a collection is not empty
    /// </summary>
    protected IRuleBuilderOptions<T, ICollection<TItem>> RequiredCollection<TItem>(
        IRuleBuilder<T, ICollection<TItem>> ruleBuilder,
        string? fieldName = null)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage($"{fieldName ?? "Collection"} cannot be empty.");
    }
}