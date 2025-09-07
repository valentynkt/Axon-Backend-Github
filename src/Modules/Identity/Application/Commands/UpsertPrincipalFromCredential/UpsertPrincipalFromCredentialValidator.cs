using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;

/// <summary>
/// Validator for UpsertPrincipalFromCredential command.
/// Note: AttachWallet validation removed - wallet operations handled separately.
/// </summary>
public sealed class UpsertPrincipalFromCredentialValidator : AbstractValidator<UpsertPrincipalFromCredentialCommand>
{
    public UpsertPrincipalFromCredentialValidator()
    {
        RuleFor(x => x.ProviderType)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("ProviderType is required and must not exceed 50 characters");

        RuleFor(x => x.Issuer)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Issuer is required and must not exceed 255 characters");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Subject is required and must not exceed 255 characters");

        RuleFor(x => x.EnvironmentId)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.EnvironmentId))
            .WithMessage("EnvironmentId must not exceed 100 characters");

        RuleFor(x => x.PrimaryEmailHash)
            .MaximumLength(64)
            .When(x => !string.IsNullOrEmpty(x.PrimaryEmailHash))
            .WithMessage("PrimaryEmailHash must not exceed 64 characters");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.IdempotencyKey))
            .WithMessage("IdempotencyKey must not exceed 255 characters");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.CorrelationId))
            .WithMessage("CorrelationId must not exceed 255 characters");
    }
}