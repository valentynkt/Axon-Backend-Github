using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a conversation is completed or closed
/// </summary>
public sealed record ConversationCompletedDomainEvent : DomainEvent
{
    /// <summary>
    /// The ID of the conversation that was completed
    /// </summary>
    public ConversationId ConversationId { get; }

    /// <summary>
    /// The total number of messages in the conversation
    /// </summary>
    public int MessageCount { get; }

    /// <summary>
    /// The total number of tool executions in the conversation
    /// </summary>
    public int ToolExecutionCount { get; }

    /// <summary>
    /// The reason the conversation was completed
    /// </summary>
    public string CompletionReason { get; }

    /// <summary>
    /// When the conversation was completed
    /// </summary>
    public DateTime CompletedAt { get; }

    public ConversationCompletedDomainEvent(
        ConversationId conversationId,
        int messageCount,
        int toolExecutionCount,
        string completionReason,
        DateTime completedAt,
        string? correlationId = null,
        string? causationId = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(correlationId: correlationId, causationId: causationId, metadata: metadata)
    {
        ConversationId = conversationId;
        MessageCount = messageCount;
        ToolExecutionCount = toolExecutionCount;
        CompletionReason = completionReason;
        CompletedAt = completedAt;
    }
}