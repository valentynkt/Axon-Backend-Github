using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.RemoveWalletLink;

/// <summary>
/// Validator for RemoveWalletLink command.
/// </summary>
public sealed class RemoveWalletLinkValidator : AbstractValidator<RemoveWalletLinkCommand>
{
    public RemoveWalletLinkValidator()
    {
        RuleFor(x => x.AxonId)
            .NotEmpty()
            .WithMessage("AxonId is required");

        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithMessage("WalletId is required");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.CorrelationId))
            .WithMessage("CorrelationId must not exceed 255 characters");
    }
}