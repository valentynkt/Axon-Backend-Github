using Axon.Modules.Chat.Primitives.ValueObjects;

namespace Axon.Modules.Chat.Application.Common;

public sealed record ChatMessageResponse(
    ConversationId ConversationId,
    MessageId UserMessageId,
    MessageId AssistantMessageId,
    string AssistantMessage
);