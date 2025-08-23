using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.DTOs.Responses;

public sealed record ProcessMessageResponse(
    ConversationId ConversationId,
    MessageId UserMessageId,
    MessageId AssistantMessageId,
    MessageContent AssistantMessage
);