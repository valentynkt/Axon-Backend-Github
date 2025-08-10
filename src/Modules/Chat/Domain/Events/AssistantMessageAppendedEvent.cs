using BuildingBlocks.Core.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when an assistant message is appended to a conversation.
/// </summary>
public sealed record AssistantMessageAppendedEvent(
    ConversationId ConversationId,
    MessageId MessageId,
    int Sequence,
    string ContentPreview,
    DateTimeOffset CreatedAt
) : DomainEvent;