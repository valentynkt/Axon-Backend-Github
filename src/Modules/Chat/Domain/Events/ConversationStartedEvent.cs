using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a new conversation is started.
/// </summary>
public sealed record ConversationStartedEvent(
    ConversationId ConversationId,
    AxonUserId OwnerId,
    string? Title,
    DateTimeOffset StartedAt
) : DomainEvent;