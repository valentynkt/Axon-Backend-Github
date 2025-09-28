using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when message history is imported into a conversation.
/// </summary>
public sealed record ConversationHistoryImportedEvent(
    ConversationId ConversationId,
    int MessageCount,
    DateTimeOffset ImportedAt
) : DomainEvent;