// File: /Users/valentynkit/Repos/Axon-Backend/src/Api/Endpoints/Chat/ChatTurnRequestValidator.cs
#nullable enable
using Axon.Api.Contracts.Chat;
using Axon.BuildingBlocks.Core.Constants;
using FastEndpoints;
using FluentValidation;

namespace Axon.Api.Endpoints.Chat;

public sealed class ChatTurnRequestValidator : Validator<ChatTurnRequestDto>
{
    public ChatTurnRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("Message cannot be empty or whitespace.")
            .Must(s => s.Trim().Length <= ChatPrimitiveConstants.MessageContentDefault.MaxLength)
            .WithMessage($"Message cannot exceed {ChatPrimitiveConstants.MessageContentDefault.MaxLength} characters.");

        RuleFor(x => x.ConversationId)
            .Must(id => id is null || id.Value != Guid.Empty)
            .WithMessage("ConversationId, when provided, cannot be empty.");
    }
}
