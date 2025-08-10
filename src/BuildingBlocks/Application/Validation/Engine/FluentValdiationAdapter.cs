using BuildingBlocks.Application.Validation;
using FluentValidation;

namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Adapts one or more FluentValidation validators into the unified app validator contract.
/// </summary>
public sealed class FluentValidationAdapter<T> : IAppValidator<T>
{
    private readonly IReadOnlyList<IValidator<T>> _validators;

    public FluentValidationAdapter(IEnumerable<IValidator<T>> validators)
    {
        _validators = validators?.ToArray() ?? Array.Empty<IValidator<T>>();
    }

    public ValidationResult Validate(T instance, IValidationContext? context = null)
        => RunAsync(instance, context, CancellationToken.None, synchronous: true).GetAwaiter().GetResult();

    public Task<ValidationResult> ValidateAsync(T instance, IValidationContext? context = null, CancellationToken ct = default)
        => RunAsync(instance, context, ct, synchronous: false);

    private async Task<ValidationResult> RunAsync(T instance, IValidationContext? ctx, CancellationToken ct, bool synchronous)
    {
        if (_validators.Count == 0) return ValidationResult.Success;

        var failures = new List<ValidationError>();

        foreach (var v in _validators)
        {
            var result = synchronous
                ? v.Validate(instance!)
                : await v.ValidateAsync(instance!, ct);

            if (!result.IsValid)
            {
                foreach (var f in result.Errors)
                {
                    failures.Add(new ValidationError(
                        Code: string.IsNullOrWhiteSpace(f.ErrorCode) ? "VAL.GENERIC" : f.ErrorCode,
                        Message: f.ErrorMessage,
                        Field: f.PropertyName,
                        Severity: ValidationSeverity.Error,
                        AttemptedValue: f.AttemptedValue,
                        Metadata: f.CustomState as IReadOnlyDictionary<string, object>));
                }
            }
        }

        return failures.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(failures).GroupByField();
    }
}
