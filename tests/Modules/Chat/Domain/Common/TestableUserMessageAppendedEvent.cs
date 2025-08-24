using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Testable version of UserMessageAppendedEvent that allows fixed EventId for equality testing
/// </summary>
public sealed record TestableUserMessageAppendedEvent(
    Guid EventId,
    ConversationId ConversationId,
    MessageId MessageId,
    int Sequence,
    string ContentPreview,
    DateTimeOffset CreatedAt
) : DomainEvent(EventId, CreatedAt.UtcDateTime);