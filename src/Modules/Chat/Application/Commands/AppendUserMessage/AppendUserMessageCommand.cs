using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Command to append a user message to a conversation and receive AI response
/// </summary>
public sealed record AppendUserMessageCommand(
    Guid ConversationId,
    string Content,
    string? IdempotencyKey = null
) : CommandBase<AppendUserMessageResponse>;