using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a conversation title is updated.
/// </summary>
public sealed record ConversationTitleUpdatedEvent(
    ConversationId ConversationId,
    string Title,
    DateTimeOffset UpdatedAt
) : DomainEvent;