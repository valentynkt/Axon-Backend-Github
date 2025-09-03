using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;

/// <summary>
/// Validator for UpsertPrincipalFromCredential command.
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

        // Validate AttachWallet if provided
        RuleFor(x => x.AttachWallet)
            .SetValidator(new AttachWalletRequestValidator()!)
            .When(x => x.AttachWallet is not null);
    }
}

/// <summary>
/// Validator for AttachWalletRequest.
/// </summary>
public sealed class AttachWalletRequestValidator : AbstractValidator<DTOs.Requests.AttachWalletRequest>
{
    public AttachWalletRequestValidator()
    {
        RuleFor(x => x.RawAddress)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("RawAddress is required and must not exceed 100 characters");

        RuleFor(x => x.ProofType)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("ProofType is required and must not exceed 50 characters");

        RuleFor(x => x.AccessMode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.AccessMode))
            .WithMessage("AccessMode must not exceed 50 characters");

        RuleFor(x => x.Label)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Label))
            .WithMessage("Label must not exceed 100 characters");
    }
}