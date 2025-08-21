// File: /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/AppendUserMessage/AppendUserMessageValidator.cs
using BuildingBlocks.Application.Validation.Base;
using Axon.Modules.Chat.Application.Validation.Extensions;
using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Validates appending a user message to an existing conversation
/// (requires valid conversation ID and message content).
/// </summary>
public sealed class AppendUserMessageValidator : BaseValidator<AppendUserMessageCommand>
{
    public AppendUserMessageValidator()
    {
        // MessageContent Value Object handles its own validation when created  
        RuleFor(x => x.Content)
            .NotNull()
            .WithMessage("Content is required.");
    }
}