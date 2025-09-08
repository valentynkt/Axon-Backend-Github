using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.UpsertWalletActivity;

/// <summary>
/// Validator for UpsertWalletActivity command.
/// INTERNAL USE ONLY - Not part of public Identity API surface.
/// </summary>
internal sealed class UpsertWalletActivityValidator : AbstractValidator<UpsertWalletActivityCommand>
{
    public UpsertWalletActivityValidator()
    {
        // Either WalletId or (ChainId + RawAddress) must be provided
        RuleFor(x => x)
            .Must(x => x.IsValid())
            .WithMessage("Either WalletId or (ChainId + RawAddress) must be provided, but not both");

        RuleFor(x => x.RawAddress)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.ChainId is not null)
            .WithMessage("RawAddress is required when ChainId is provided and must not exceed 100 characters");

        RuleFor(x => x.ChainId)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.RawAddress))
            .WithMessage("ChainId is required when RawAddress is provided");

        RuleFor(x => x.ObservedAt)
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .When(x => x.ObservedAt.HasValue)
            .WithMessage("ObservedAt cannot be more than 5 minutes in the future");

        RuleFor(x => x.TagsToAdd)
            .Must(tags => tags!.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 50))
            .When(x => x.TagsToAdd is not null)
            .WithMessage("All tags must be non-empty and not exceed 50 characters");

        RuleFor(x => x.TagsToRemove)
            .Must(tags => tags!.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 50))
            .When(x => x.TagsToRemove is not null)
            .WithMessage("All tags must be non-empty and not exceed 50 characters");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.CorrelationId))
            .WithMessage("CorrelationId must not exceed 255 characters");
    }
}