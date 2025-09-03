using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.EnsureWalletLinked;

/// <summary>
/// Validator for EnsureWalletLinked command.
/// </summary>
public sealed class EnsureWalletLinkedValidator : AbstractValidator<EnsureWalletLinkedCommand>
{
    public EnsureWalletLinkedValidator()
    {
        RuleFor(x => x.AxonId)
            .NotEmpty()
            .WithMessage("AxonId is required");

        RuleFor(x => x.ChainId)
            .NotEmpty()
            .WithMessage("ChainId is required");

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