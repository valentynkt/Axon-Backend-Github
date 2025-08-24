using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Testable version of AssistantMessageAppendedEvent that allows fixed EventId for equality testing
/// </summary>
public sealed record TestableAssistantMessageAppendedEvent(
    Guid EventId,
    ConversationId ConversationId,
    MessageId MessageId,
    int Sequence,
    string ContentPreview,
    AiResponseId AiResponseId,
    DateTimeOffset CreatedAt
) : DomainEvent(EventId, CreatedAt.UtcDateTime);