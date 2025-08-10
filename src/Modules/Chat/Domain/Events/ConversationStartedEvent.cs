using BuildingBlocks.Core.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a new conversation is started.
/// </summary>
public sealed record ConversationStartedEvent(
    ConversationId ConversationId,
    UserId OwnerId,
    string Title,
    bool IsDefaultTitle,
    DateTimeOffset StartedAt
) : DomainEvent;