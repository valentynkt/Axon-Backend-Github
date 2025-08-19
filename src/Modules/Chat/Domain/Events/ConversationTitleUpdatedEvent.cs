using BuildingBlocks.Core.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Primitives.ValueObjects;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a conversation title is updated.
/// </summary>
public sealed record ConversationTitleUpdatedEvent(
    ConversationId ConversationId,
    string Title,
    DateTimeOffset UpdatedAt
) : DomainEvent;