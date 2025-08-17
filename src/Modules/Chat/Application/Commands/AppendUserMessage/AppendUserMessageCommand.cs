using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Command to append a user message to a conversation and receive AI response
/// </summary>
public sealed record AppendUserMessageCommand(
    Guid ConversationId,
    string Content,
    string? IdempotencyKey = null
) : IRequest<Result<AppendUserMessageResponse>>;