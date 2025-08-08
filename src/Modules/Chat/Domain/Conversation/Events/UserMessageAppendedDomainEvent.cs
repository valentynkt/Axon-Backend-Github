using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Conversation.Events;

/// <summary>
/// Raised when a user message is appended to a conversation.
/// Minimal payload: ids + sequence + actor.
/// </summary>
public sealed record UserMessageAppendedDomainEvent(
    ConversationId ConversationId,
    MessageId MessageId,
    int Sequence,
    string AppendedByUserId)
    : DomainEventBase;