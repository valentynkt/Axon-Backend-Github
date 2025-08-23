using Axon.Api.Contracts.V1.Chat;
using Axon.BuildingBlocks.Core.Constants;
using BuildingBlocks.Application.Validation;
using FastEndpoints;
using FluentValidation;

namespace Axon.Api.Endpoints.V1.Chat.Commands.ChatTurn;

/// <summary>
/// Validator for chat turn requests
/// </summary>
public sealed class ChatTurnRequestValidator : Validator<ChatTurnRequestDto>
{
    public ChatTurnRequestValidator()
    {
        // Use the base validator patterns from ValidatorBase
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required.")
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage("Message cannot be empty or whitespace.")
            .Must(s => s != null && s.Trim().Length <= ChatPrimitiveConstants.MessageContentDefault.MaxLength)
            .WithMessage($"Message cannot exceed {ChatPrimitiveConstants.MessageContentDefault.MaxLength} characters.");

        RuleFor(x => x.ConversationId)
            .Must(id => id is null || id.Value != Guid.Empty)
            .WithMessage("ConversationId, when provided, cannot be empty.");
    }
}
