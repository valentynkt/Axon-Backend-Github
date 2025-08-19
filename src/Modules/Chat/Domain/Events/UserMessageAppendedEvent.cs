using BuildingBlocks.Core.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Primitives.ValueObjects;

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
) : DomainEvent;