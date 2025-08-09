using BuildingBlocks.Application.Abstractions.Validation;

namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Single entry point for executing validation over a request/command.
/// Orchestrates all registered validators (FluentValidation + custom) and
/// returns a unified <see cref="ValidationResult"/>. Stateless by design.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Synchronously validate an instance with an optional metadata-aware context.
    /// </summary>
    /// <typeparam name="T">Type of instance to validate</typeparam>
    /// <param name="instance">Instance to validate</param>
    /// <param name="ctx">Optional validation context (tenant, user, flags, etc.)</param>
    /// <returns>Aggregated validation result</returns>
    ValidationResult Validate<T>(T instance, IValidationContext? ctx = null);

    /// <summary>
    /// Asynchronously validate an instance with an optional metadata-aware context.
    /// </summary>
    /// <typeparam name="T">Type of instance to validate</typeparam>
    /// <param name="instance">Instance to validate</param>
    /// <param name="ctx">Optional validation context (tenant, user, flags, etc.)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Aggregated validation result</returns>
    Task<ValidationResult> ValidateAsync<T>(T instance, IValidationContext? ctx = null, CancellationToken ct = default);
}