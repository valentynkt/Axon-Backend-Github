using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Testable version of ConversationCompletedEvent that allows fixed EventId for equality testing
/// </summary>
public sealed record TestableConversationCompletedEvent(
    Guid EventId,
    ConversationId ConversationId,
    int MessageCount,
    DateTimeOffset CompletedAt
) : DomainEvent(EventId, CompletedAt.UtcDateTime);