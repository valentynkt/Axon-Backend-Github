using System.Linq.Expressions;
using FluentValidation;

namespace Axon.BuildingBlocks.Validation;

/// <summary>
/// Base validator class providing common validation patterns and methods
/// </summary>
/// <typeparam name="T">The type of object being validated</typeparam>
public abstract class ValidatorBase<T> : AbstractValidator<T> where T : class
{
    /// <summary>
    /// Validates that a string is not null, empty, or whitespace
    /// </summary>
    protected IRuleBuilderOptions<T, string?> NotNullOrWhiteSpace<TProperty>(
        Expression<Func<T, string?>> expression,
        string propertyName)
    {
        return RuleFor(expression)
            .NotEmpty()
            .WithMessage($"{propertyName} is required.")
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage($"{propertyName} cannot be empty or whitespace.");
    }

    /// <summary>
    /// Validates string length with maximum constraint
    /// </summary>
    protected IRuleBuilderOptions<T, string?> MaxLength<TProperty>(
        Expression<Func<T, string?>> expression,
        int maxLength,
        string propertyName)
    {
        return RuleFor(expression)
            .Must(s => string.IsNullOrEmpty(s) || s.Length <= maxLength)
            .WithMessage($"{propertyName} cannot exceed {maxLength} characters.");
    }

    /// <summary>
    /// Validates trimmed string length with maximum constraint
    /// </summary>
    protected IRuleBuilderOptions<T, string?> TrimmedMaxLength<TProperty>(
        Expression<Func<T, string?>> expression,
        int maxLength,
        string propertyName)
    {
        return RuleFor(expression)
            .Must(s => string.IsNullOrEmpty(s) || s.Trim().Length <= maxLength)
            .WithMessage($"{propertyName} cannot exceed {maxLength} characters.");
    }

    /// <summary>
    /// Validates that a GUID is not empty when provided
    /// </summary>
    protected IRuleBuilderOptions<T, Guid?> NotEmptyGuid<TProperty>(
        Expression<Func<T, Guid?>> expression,
        string propertyName)
    {
        return RuleFor(expression)
            .Must(id => id is null || id.Value != Guid.Empty)
            .WithMessage($"{propertyName}, when provided, cannot be empty.");
    }

    /// <summary>
    /// Validates email format
    /// </summary>
    protected IRuleBuilderOptions<T, string?> ValidEmail<TProperty>(
        Expression<Func<T, string?>> expression,
        string propertyName)
    {
        return RuleFor(expression)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty((expression.Compile())(x)))
            .WithMessage($"{propertyName} must be a valid email address.");
    }

    /// <summary>
    /// Validates numeric range
    /// </summary>
    protected IRuleBuilderOptions<T, TValue> InRange<TProperty, TValue>(
        Expression<Func<T, TValue>> expression,
        TValue min,
        TValue max,
        string propertyName) where TValue : IComparable<TValue>, IComparable
    {
        return RuleFor(expression)
            .InclusiveBetween(min, max)
            .WithMessage($"{propertyName} must be between {min} and {max}.");
    }

    /// <summary>
    /// Validates collection is not empty
    /// </summary>
    protected IRuleBuilderOptions<T, IEnumerable<TElement>?> NotEmptyCollection<TProperty, TElement>(
        Expression<Func<T, IEnumerable<TElement>?>> expression,
        string propertyName)
    {
        return RuleFor(expression)
            .NotEmpty()
            .WithMessage($"{propertyName} must contain at least one item.");
    }

    /// <summary>
    /// Validates URL format
    /// </summary>
    protected IRuleBuilderOptions<T, string?> ValidUrl<TProperty>(
        Expression<Func<T, string?>> expression,
        string propertyName)
    {
        return RuleFor(expression)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage($"{propertyName} must be a valid URL.");
    }

    /// <summary>
    /// Validates phone number format (basic E.164 format)
    /// </summary>
    protected IRuleBuilderOptions<T, string?> ValidPhoneNumber<TProperty>(
        Expression<Func<T, string?>> expression,
        string propertyName)
    {
        return RuleFor(expression)
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .When(x => !string.IsNullOrEmpty((expression.Compile())(x)))
            .WithMessage($"{propertyName} must be a valid phone number.");
    }

    /// <summary>
    /// Custom async validation with database/external service check
    /// </summary>
    protected IRuleBuilderOptions<T, TProperty> MustAsync<TProperty>(
        Expression<Func<T, TProperty>> expression,
        Func<TProperty, CancellationToken, Task<bool>> predicate,
        string errorMessage)
    {
        return RuleFor(expression)
            .MustAsync(predicate)
            .WithMessage(errorMessage);
    }

    /// <summary>
    /// Validates that a value is unique within a context
    /// </summary>
    protected IRuleBuilderOptions<T, TProperty> Unique<TProperty>(
        Expression<Func<T, TProperty>> expression,
        Func<TProperty, CancellationToken, Task<bool>> isUniqueCheck,
        string propertyName)
    {
        return RuleFor(expression)
            .MustAsync(isUniqueCheck)
            .WithMessage($"{propertyName} must be unique.");
    }

    /// <summary>
    /// Validates that at least one of the specified properties is not null or empty
    /// </summary>
    protected void AtLeastOneRequired(params Expression<Func<T, object?>>[] expressions)
    {
        RuleFor(x => x)
            .Must(x =>
            {
                foreach (var expression in expressions)
                {
                    var value = expression.Compile()(x);
                    if (value != null && (value is not string str || !string.IsNullOrWhiteSpace(str)))
                    {
                        return true;
                    }
                }
                return false;
            })
            .WithMessage("At least one field must be provided.");
    }

    /// <summary>
    /// Conditional validation based on another property
    /// </summary>
    protected IRuleBuilderOptions<T, TProperty> RequiredWhen<TProperty, TOther>(
        Expression<Func<T, TProperty>> expression,
        Expression<Func<T, TOther>> otherExpression,
        Func<TOther, bool> condition,
        string propertyName)
    {
        var otherFunc = otherExpression.Compile();
        return RuleFor(expression)
            .NotEmpty()
            .When(x => condition(otherFunc(x)))
            .WithMessage($"{propertyName} is required when condition is met.");
    }
}