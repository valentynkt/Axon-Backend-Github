using BuildingBlocks.Core.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Primitives.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when an assistant message is appended to a conversation.
/// Includes the AI response ID for tracking and context linking purposes.
/// </summary>
public sealed record AssistantMessageAppendedEvent(
    ConversationId ConversationId,
    MessageId MessageId,
    int Sequence,
    string ContentPreview,
    AiResponseId AiResponseId,
    DateTimeOffset CreatedAt
) : DomainEvent;