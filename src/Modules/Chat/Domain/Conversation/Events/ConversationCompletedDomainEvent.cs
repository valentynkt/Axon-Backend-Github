using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Conversation.Events;

/// <summary>
/// Domain event raised when a conversation is completed.
/// Follows Epic 2 domain event patterns with primitive values.
/// </summary>
public sealed record ConversationCompletedDomainEvent : DomainEvent
{
    public ConversationId ConversationId { get; }
    public UserId UserId { get; }
    public int MessageCount { get; }

    public ConversationCompletedDomainEvent(
        ConversationId conversationId, 
        UserId userId, 
        int messageCount)
    {
        ConversationId = conversationId;
        UserId = userId;
        MessageCount = messageCount;
    }
}