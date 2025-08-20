using BuildingBlocks.Core.Domain.Events;

using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a conversation is completed.
/// </summary>
public sealed record ConversationCompletedEvent(
    ConversationId ConversationId,
    int MessageCount,
    DateTimeOffset CompletedAt
) : DomainEvent;