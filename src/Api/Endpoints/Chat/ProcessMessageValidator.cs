// File: /Api/Endpoints/Chat/ProcessMessageValidator.cs

using Axon.Api.Contracts.Chat;
using Axon.BuildingBlocks.Core.Constants;
using Axon.Modules.Chat.Primitives.Constants;
using FastEndpoints;
using FluentValidation;

namespace Axon.Api.Endpoints.Chat;

public sealed class ProcessMessageValidator : Validator<ProcessMessageRequest>
{
    public ProcessMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(ChatPrimitiveConstants.MessageContent.MaxLength)
            .WithMessage($"Message cannot exceed {ChatPrimitiveConstants.MessageContent.MaxLength} characters.");
    }
}