;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Common;

public sealed record ChatMessageResponse(
    ConversationId ConversationId,
    MessageId UserMessageId,
    MessageId AssistantMessageId,
    string AssistantMessage
);