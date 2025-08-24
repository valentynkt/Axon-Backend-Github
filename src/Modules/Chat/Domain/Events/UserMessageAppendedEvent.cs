using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a user message is appended to a conversation.
/// </summary>
public sealed record UserMessageAppendedEvent(
    ConversationId ConversationId,
    MessageId MessageId,
    int Sequence,
    string ContentPreview,
    DateTimeOffset CreatedAt
) : DomainEvent(CreatedAt.UtcDateTime);