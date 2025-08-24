using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Testable version of ConversationStartedEvent that allows fixed EventId for equality testing
/// </summary>
public sealed record TestableConversationStartedEvent(
    Guid EventId,
    ConversationId ConversationId,
    UserId OwnerId,
    string Title,
    DateTimeOffset StartedAt
) : DomainEvent(EventId, StartedAt.UtcDateTime);