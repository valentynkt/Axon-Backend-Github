using BuildingBlocks.Application.Validation;
using BuildingBlocks.Application.Validation.Core;

namespace BuildingBlocks.Application.Validation;

public interface IAppValidator<in T>
{
    ValidationResult Validate(T instance, IValidationContext? context = null);

    Task<ValidationResult> ValidateAsync(
        T instance,
        IValidationContext? context = null,
        CancellationToken ct = default);
}