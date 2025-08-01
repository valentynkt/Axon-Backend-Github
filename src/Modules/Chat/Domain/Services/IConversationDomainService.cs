using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Domain service for complex conversation operations
/// </summary>
public interface IConversationDomainService
{
    /// <summary>
    /// Processes a message through the conversation workflow
    /// </summary>
    /// <param name="conversation">The conversation to process the message in</param>
    /// <param name="content">The message content</param>
    /// <param name="role">The message role</param>
    /// <param name="metadata">Optional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the processed message</returns>
    Task<Result<Message>> ProcessMessageAsync(
        Conversation conversation,
        string content,
        MessageRole role,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a tool within the context of a conversation
    /// </summary>
    /// <param name="conversation">The conversation context</param>
    /// <param name="toolName">The name of the tool to execute</param>
    /// <param name="arguments">The tool arguments</param>
    /// <param name="associatedMessageId">The message that triggered the tool execution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the tool execution result</returns>
    Task<Result<ToolExecution>> ExecuteToolAsync(
        Conversation conversation,
        string toolName,
        string arguments,
        MessageId associatedMessageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives old messages from a conversation based on retention policies
    /// </summary>
    /// <param name="conversation">The conversation to archive messages from</param>
    /// <param name="retentionPolicy">The retention policy to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with the number of messages archived</returns>
    Task<Result<int>> ArchiveOldMessagesAsync(
        Conversation conversation,
        MessageRetentionPolicy retentionPolicy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules for conversation state transitions
    /// </summary>
    /// <param name="conversation">The conversation to validate</param>
    /// <param name="operation">The operation being performed</param>
    /// <returns>Result indicating if the operation is valid</returns>
    Result ValidateBusinessRules(Conversation conversation, ConversationOperation operation);

    /// <summary>
    /// Calculates conversation health metrics
    /// </summary>
    /// <param name="conversation">The conversation to analyze</param>
    /// <returns>Health metrics for the conversation</returns>
    ConversationHealthMetrics CalculateHealthMetrics(Conversation conversation);
}

/// <summary>
/// Represents different operations that can be performed on a conversation
/// </summary>
public enum ConversationOperation
{
    AddMessage,
    AddToolExecution,
    CompleteConversation,
    UpdateContext,
    ArchiveMessages
}

/// <summary>
/// Represents message retention policies
/// </summary>
public sealed record MessageRetentionPolicy
{
    public int MaxMessages { get; }
    public TimeSpan MaxAge { get; }
    public bool PreserveSystemMessages { get; }
    public bool PreserveFirstMessage { get; }
    public bool PreserveLastMessage { get; }

    public MessageRetentionPolicy(
        int maxMessages = 1000,
        TimeSpan? maxAge = null,
        bool preserveSystemMessages = true,
        bool preserveFirstMessage = true,
        bool preserveLastMessage = true)
    {
        MaxMessages = maxMessages;
        MaxAge = maxAge ?? TimeSpan.FromDays(30);
        PreserveSystemMessages = preserveSystemMessages;
        PreserveFirstMessage = preserveFirstMessage;
        PreserveLastMessage = preserveLastMessage;
    }

    public static MessageRetentionPolicy Default() => new();
    
    public static MessageRetentionPolicy Aggressive() => new(
        maxMessages: 100,
        maxAge: TimeSpan.FromDays(7),
        preserveSystemMessages: false,
        preserveFirstMessage: true,
        preserveLastMessage: true);
}

/// <summary>
/// Represents health metrics for a conversation
/// </summary>
public sealed record ConversationHealthMetrics
{
    public ConversationId ConversationId { get; }
    public double MessageFrequency { get; }
    public double ToolExecutionSuccessRate { get; }
    public TimeSpan AverageResponseTime { get; }
    public int ErrorCount { get; }
    public double EngagementScore { get; }
    public string HealthStatus { get; }
    public IReadOnlyList<string> Recommendations { get; }

    public ConversationHealthMetrics(
        ConversationId conversationId,
        double messageFrequency,
        double toolExecutionSuccessRate,
        TimeSpan averageResponseTime,
        int errorCount,
        double engagementScore,
        string healthStatus,
        IReadOnlyList<string> recommendations)
    {
        ConversationId = conversationId;
        MessageFrequency = messageFrequency;
        ToolExecutionSuccessRate = toolExecutionSuccessRate;
        AverageResponseTime = averageResponseTime;
        ErrorCount = errorCount;
        EngagementScore = engagementScore;
        HealthStatus = healthStatus;
        Recommendations = recommendations;
    }
}