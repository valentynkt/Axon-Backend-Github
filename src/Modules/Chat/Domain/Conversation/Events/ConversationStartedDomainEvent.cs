using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using BuildingBlocks.Core.Domain.Events;
using UserId = Axon.Modules.Chat.Domain.Conversation.ValueObjects.UserId;

namespace Axon.Modules.Chat.Domain.Conversation.Events;

/// <summary>
/// Raised when a conversation is started.
/// Minimal payload: ids + title.
/// </summary>
public sealed record ConversationStartedDomainEvent(ConversationId ConversationId, UserId OwnerId, string Title)
    : DomainEventBase;