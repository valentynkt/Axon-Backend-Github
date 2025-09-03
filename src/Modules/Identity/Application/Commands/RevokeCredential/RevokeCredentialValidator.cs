using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.RevokeCredential;

/// <summary>
/// Validator for RevokeCredential command.
/// </summary>
public sealed class RevokeCredentialValidator : AbstractValidator<RevokeCredentialCommand>
{
    public RevokeCredentialValidator()
    {
        RuleFor(x => x.AxonId)
            .NotEmpty()
            .WithMessage("AxonId is required");

        RuleFor(x => x.CredentialIdentifier)
            .NotNull()
            .WithMessage("CredentialIdentifier is required")
            .Must(identifier => identifier.IsValid())
            .WithMessage("Either CredentialId or (ProviderType + Issuer + Subject) must be provided");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Reason))
            .WithMessage("Reason must not exceed 500 characters");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.CorrelationId))
            .WithMessage("CorrelationId must not exceed 255 characters");
    }
}