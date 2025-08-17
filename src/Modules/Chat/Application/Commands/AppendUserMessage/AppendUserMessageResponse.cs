namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Response containing both user and assistant message details
/// </summary>
public sealed record AppendUserMessageResponse(
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string Content
);