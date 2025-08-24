using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Testable version of ConversationTitleUpdatedEvent that allows fixed EventId for equality testing
/// </summary>
public sealed record TestableConversationTitleUpdatedEvent(
    Guid EventId,
    ConversationId ConversationId,
    string Title,
    DateTimeOffset UpdatedAt
) : DomainEvent(EventId, UpdatedAt.UtcDateTime);