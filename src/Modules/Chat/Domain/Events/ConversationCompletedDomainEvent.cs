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
    /// The duration of the conversation from first to last message
    /// </summary>
    public TimeSpan ConversationDuration { get; }

    /// <summary>
    /// The reason the conversation was completed
    /// </summary>
    public string CompletionReason { get; }

    /// <summary>
    /// When the conversation was started
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// When the conversation was completed
    /// </summary>
    public DateTime CompletedAt { get; }

    public ConversationCompletedDomainEvent(
        ConversationId conversationId,
        int messageCount,
        int toolExecutionCount,
        TimeSpan conversationDuration,
        string completionReason,
        DateTime startedAt,
        DateTime completedAt)
    {
        ConversationId = conversationId;
        MessageCount = messageCount;
        ToolExecutionCount = toolExecutionCount;
        ConversationDuration = conversationDuration;
        CompletionReason = completionReason;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }
}