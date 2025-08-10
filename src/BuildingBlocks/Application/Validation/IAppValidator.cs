using BuildingBlocks.Application.Validation;

namespace BuildingBlocks.Application.Validation;

public interface IAppValidator<in T>
{
    ValidationResult Validate(T instance, IValidationContext? context = null);

    Task<ValidationResult> ValidateAsync(
        T instance,
        IValidationContext? context = null,
        CancellationToken ct = default);
}