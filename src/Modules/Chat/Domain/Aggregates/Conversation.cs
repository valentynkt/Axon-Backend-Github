using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Aggregates;

/// <summary>
/// Conversation aggregate root that manages messages and tool executions
/// </summary>
public sealed class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = new();
    private readonly List<ToolExecution> _toolExecutions = new();

    /// <summary>
    /// Gets the read-only collection of messages in this conversation
    /// </summary>
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Gets the read-only collection of tool executions in this conversation
    /// </summary>
    public IReadOnlyList<ToolExecution> ToolExecutions => _toolExecutions.AsReadOnly();

    /// <summary>
    /// The conversation context containing configuration and metadata
    /// </summary>
    public ConversationContext Context { get; private set; }

    /// <summary>
    /// When the conversation was created
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When the conversation was last updated
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// When the conversation was completed (if applicable)
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Whether the conversation is active
    /// </summary>
    public bool IsActive => CompletedAt == null;

    /// <summary>
    /// Whether the conversation has been completed
    /// </summary>
    public bool IsCompleted => CompletedAt != null;

    /// <summary>
    /// The current message count
    /// </summary>
    public int MessageCount => _messages.Count;

    /// <summary>
    /// The current tool execution count
    /// </summary>
    public int ToolExecutionCount => _toolExecutions.Count;

    /// <summary>
    /// Gets the first message in the conversation
    /// </summary>
    public Message? FirstMessage => _messages.OrderBy(m => m.CreatedAt).FirstOrDefault();

    /// <summary>
    /// Gets the last message in the conversation
    /// </summary>
    public Message? LastMessage => _messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

    /// <summary>
    /// Gets the total conversation duration (from first to last message)
    /// </summary>
    public TimeSpan? ConversationDuration
    {
        get
        {
            var first = FirstMessage;
            var last = LastMessage;
            return first != null && last != null && first != last 
                ? last.CreatedAt - first.CreatedAt 
                : null;
        }
    }

    // Private constructor for EF Core
    private Conversation() : base(default!)
    {
        Context = ConversationContext.Default();
    }

    private Conversation(ConversationId id, ConversationContext context) : base(id)
    {
        Context = context;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new conversation with the specified context
    /// </summary>
    /// <param name="context">The conversation context</param>
    /// <returns>Result containing the created conversation or validation errors</returns>
    public static Result<Conversation> Create(ConversationContext? context = null)
    {
        var conversationId = ConversationId.New();
        var conversationContext = context ?? ConversationContext.Default();
        
        return new Conversation(conversationId, conversationContext);
    }

    /// <summary>
    /// Adds a message to the conversation with proper business rule validation
    /// </summary>
    /// <param name="content">The message content</param>
    /// <param name="role">The message role</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>Result containing the created message or validation errors</returns>
    public Result<Message> AddMessage(string content, MessageRole role, Dictionary<string, string>? metadata = null)
    {
        if (IsCompleted)
            return Error.Validation("Cannot add messages to a completed conversation");

        // Check if we've exceeded the context limits
        if (!Context.MaintainFullHistory && _messages.Count >= Context.MaxMessages)
        {
            // Remove older messages to make room (keeping the most recent ones)
            var messagesToRemove = _messages.Count - Context.MaxMessages + 1;
            var oldestMessages = _messages.OrderBy(m => m.CreatedAt).Take(messagesToRemove).ToList();
            
            foreach (var oldMessage in oldestMessages)
            {
                _messages.Remove(oldMessage);
            }
        }

        // Determine the previous message for ordering
        var previousMessage = LastMessage;
        
        // Create the new message
        var messageResult = Message.Create(content, Id, role, previousMessage?.Id, metadata);
        if (messageResult.IsFailure)
            return messageResult.Error;

        var message = messageResult.Value;
        _messages.Add(message);
        UpdatedAt = DateTime.UtcNow;

        // Raise domain event
        var isFirstMessage = _messages.Count == 1;
        RaiseDomainEvent(new MessageAddedDomainEvent(
            Id, 
            message.Id, 
            content, 
            role.Value, 
            isFirstMessage));

        return message;
    }

    /// <summary>
    /// Adds a tool execution result to the conversation
    /// </summary>
    /// <param name="toolExecution">The tool execution to add</param>
    /// <param name="associatedMessageId">The message that triggered this tool execution</param>
    /// <returns>Result indicating success or failure</returns>
    public Result AddToolExecution(ToolExecution toolExecution, MessageId associatedMessageId)
    {
        ArgumentNullException.ThrowIfNull(toolExecution);

        if (IsCompleted)
            return Error.Validation("Cannot add tool executions to a completed conversation");

        // Verify the associated message exists in this conversation
        var associatedMessage = _messages.FirstOrDefault(m => m.Id == associatedMessageId);
        if (associatedMessage == null)
            return Error.Validation("Associated message not found in this conversation");

        // Check if we've exceeded the context limits for tool executions
        if (!Context.MaintainFullHistory && _toolExecutions.Count >= Context.MaxToolExecutions)
        {
            // Remove older tool executions to make room
            var executionsToRemove = _toolExecutions.Count - Context.MaxToolExecutions + 1;
            var oldestExecutions = _toolExecutions.Take(executionsToRemove).ToList();
            
            foreach (var oldExecution in oldestExecutions)
            {
                _toolExecutions.Remove(oldExecution);
            }
        }

        _toolExecutions.Add(toolExecution);
        UpdatedAt = DateTime.UtcNow;

        // Raise domain event
        RaiseDomainEvent(new ToolExecutedDomainEvent(Id, associatedMessageId, toolExecution));

        return Result.Success();
    }

    /// <summary>
    /// Completes the conversation, preventing further modifications
    /// </summary>
    /// <param name="completionReason">The reason for completion</param>
    /// <returns>Result indicating success or failure</returns>
    public Result CompleteConversation(string completionReason = "User completed")
    {
        if (IsCompleted)
            return Error.Validation("Conversation is already completed");

        if (string.IsNullOrWhiteSpace(completionReason))
            return Error.Validation("Completion reason cannot be null or empty");

        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        // Raise domain event
        var duration = ConversationDuration ?? TimeSpan.Zero;
        RaiseDomainEvent(new ConversationCompletedDomainEvent(
            Id,
            MessageCount,
            ToolExecutionCount,
            duration,
            completionReason.Trim(),
            CreatedAt,
            CompletedAt.Value));

        return Result.Success();
    }

    /// <summary>
    /// Updates the conversation context
    /// </summary>
    /// <param name="newContext">The new context</param>
    /// <returns>Result indicating success or failure</returns>
    public Result UpdateContext(ConversationContext newContext)
    {
        ArgumentNullException.ThrowIfNull(newContext);

        if (IsCompleted)
            return Error.Validation("Cannot update context of a completed conversation");

        Context = newContext;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    /// <summary>
    /// Gets messages by role
    /// </summary>
    /// <param name="role">The message role to filter by</param>
    /// <returns>Messages with the specified role</returns>
    public IEnumerable<Message> GetMessagesByRole(MessageRole role)
    {
        return _messages.Where(m => m.Role == role).OrderBy(m => m.CreatedAt);
    }

    /// <summary>
    /// Gets the most recent messages up to a specified count
    /// </summary>
    /// <param name="count">The maximum number of messages to return</param>
    /// <returns>The most recent messages</returns>
    public IEnumerable<Message> GetRecentMessages(int count)
    {
        if (count <= 0)
            return Enumerable.Empty<Message>();

        return _messages.OrderByDescending(m => m.CreatedAt).Take(count).Reverse();
    }

    /// <summary>
    /// Gets successful tool executions
    /// </summary>
    /// <returns>Tool executions that completed successfully</returns>
    public IEnumerable<ToolExecution> GetSuccessfulToolExecutions()
    {
        return _toolExecutions.Where(te => te.IsSuccess);
    }

    /// <summary>
    /// Gets failed tool executions
    /// </summary>
    /// <returns>Tool executions that failed</returns>
    public IEnumerable<ToolExecution> GetFailedToolExecutions()
    {
        return _toolExecutions.Where(te => !te.IsSuccess);
    }

    /// <summary>
    /// Gets the total execution time for all tool executions
    /// </summary>
    /// <returns>The total time spent on tool executions</returns>
    public TimeSpan GetTotalToolExecutionTime()
    {
        return _toolExecutions.Aggregate(TimeSpan.Zero, (total, execution) => total + execution.ExecutionTime);
    }

    /// <summary>
    /// Determines if the conversation can accept a new message
    /// </summary>
    /// <returns>True if a new message can be added</returns>
    public bool CanAddMessage()
    {
        return IsActive && (Context.MaintainFullHistory || _messages.Count < Context.MaxMessages);
    }

    /// <summary>
    /// Determines if the conversation can accept a new tool execution
    /// </summary>
    /// <returns>True if a new tool execution can be added</returns>
    public bool CanAddToolExecution()
    {
        return IsActive && (Context.MaintainFullHistory || _toolExecutions.Count < Context.MaxToolExecutions);
    }

    /// <summary>
    /// Gets conversation statistics
    /// </summary>
    /// <returns>A summary of conversation metrics</returns>
    public ConversationStatistics GetStatistics()
    {
        return new ConversationStatistics(
            Id,
            MessageCount,
            ToolExecutionCount,
            GetSuccessfulToolExecutions().Count(),
            GetFailedToolExecutions().Count(),
            ConversationDuration ?? TimeSpan.Zero,
            GetTotalToolExecutionTime(),
            CreatedAt,
            UpdatedAt,
            CompletedAt,
            IsActive);
    }
}

/// <summary>
/// Represents conversation statistics and metrics
/// </summary>
public sealed record ConversationStatistics(
    ConversationId ConversationId,
    int MessageCount,
    int ToolExecutionCount,
    int SuccessfulToolExecutions,
    int FailedToolExecutions,
    TimeSpan ConversationDuration,
    TimeSpan TotalToolExecutionTime,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    bool IsActive);