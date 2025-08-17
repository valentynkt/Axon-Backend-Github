using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Response containing both user and assistant message details
/// </summary>
public sealed record AppendUserMessageResponse(
    ConversationId ConversationId,
    MessageId UserMessageId,
    MessageId AssistantMessageId,
    MessageContent Content
);