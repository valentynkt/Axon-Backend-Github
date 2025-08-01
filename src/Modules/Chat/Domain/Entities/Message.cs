using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Entities;

/// <summary>
/// Represents a message within a conversation
/// </summary>
public sealed class Message : Entity<MessageId>
{
    /// <summary>
    /// The content of the message
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// The ID of the conversation this message belongs to
    /// </summary>
    public ConversationId ConversationId { get; private set; }

    /// <summary>
    /// The ID of the previous message in the conversation thread (for maintaining order)
    /// </summary>
    public MessageId? PreviousMessageId { get; private set; }

    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When the message was last updated
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// The current processing status of the message
    /// </summary>
    public MessageStatus Status { get; private set; }

    /// <summary>
    /// The role of the message sender
    /// </summary>
    public MessageRole Role { get; private set; }

    /// <summary>
    /// Optional metadata associated with the message
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; private set; }

    /// <summary>
    /// When the message processing was completed (if applicable)
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// The number of tokens in the message content (for tracking usage)
    /// </summary>
    public int? TokenCount { get; private set; }

    // Private constructor for EF Core
    private Message() : base(default!)
    {
        Content = string.Empty;
        Metadata = new Dictionary<string, string>();
    }

    private Message(
        MessageId id,
        string content,
        ConversationId conversationId,
        MessageRole role,
        MessageId? previousMessageId = null,
        Dictionary<string, string>? metadata = null) : base(id)
    {
        Content = content;
        ConversationId = conversationId;
        Role = role;
        PreviousMessageId = previousMessageId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Status = MessageStatus.Draft;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Creates a new message with validation
    /// </summary>
    /// <param name="content">The message content</param>
    /// <param name="conversationId">The conversation ID</param>
    /// <param name="role">The message role</param>
    /// <param name="previousMessageId">Optional previous message ID for ordering</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>Result containing the created message or validation errors</returns>
    public static Result<Message> Create(
        string content,
        ConversationId conversationId,
        MessageRole role,
        MessageId? previousMessageId = null,
        Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Error.Validation("Message content cannot be null or empty");

        if (content.Length > 100_000) // 100KB limit
            return Error.Validation("Message content cannot exceed 100,000 characters");

        // Validate metadata if provided
        if (metadata != null)
        {
            foreach (var (key, value) in metadata)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return Error.Validation("Metadata keys cannot be null or empty");
                
                if (key.Length > 100)
                    return Error.Validation("Metadata keys cannot exceed 100 characters");
                    
                if (value?.Length > 1000)
                    return Error.Validation("Metadata values cannot exceed 1000 characters");
            }
        }

        var messageId = MessageId.New();
        return new Message(messageId, content.Trim(), conversationId, role, previousMessageId, metadata);
    }

    /// <summary>
    /// Marks the message as being processed
    /// </summary>
    /// <returns>Result indicating success or failure</returns>
    public Result StartProcessing()
    {
        if (!Status.CanBeProcessed)
            return Error.Validation($"Cannot start processing message in status: {Status}");

        Status = MessageStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = null; // Clear any previous error

        return Result.Success();
    }

    /// <summary>
    /// Marks the message as successfully processed
    /// </summary>
    /// <param name="tokenCount">Optional token count for the message</param>
    /// <returns>Result indicating success or failure</returns>
    public Result CompleteProcessing(int? tokenCount = null)
    {
        if (!Status.IsProcessing)
            return Error.Validation($"Cannot complete processing for message in status: {Status}");

        if (tokenCount.HasValue && tokenCount.Value < 0)
            return Error.Validation("Token count cannot be negative");

        Status = MessageStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        TokenCount = tokenCount;
        ErrorMessage = null;

        return Result.Success();
    }

    /// <summary>
    /// Marks the message processing as failed
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure</param>
    /// <returns>Result indicating success or failure</returns>
    public Result FailProcessing(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return Error.Validation("Error message cannot be null or empty when failing processing");

        if (!Status.IsProcessing)
            return Error.Validation($"Cannot fail processing for message in status: {Status}");

        Status = MessageStatus.Failed;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage.Trim();

        return Result.Success();
    }

    /// <summary>
    /// Cancels message processing
    /// </summary>
    /// <returns>Result indicating success or failure</returns>
    public Result CancelProcessing()
    {
        if (Status.IsFinal && !Status.IsProcessing)
            return Error.Validation($"Cannot cancel processing for message in final status: {Status}");

        Status = MessageStatus.Cancelled;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    /// <summary>
    /// Updates the message content (only allowed for draft messages)
    /// </summary>
    /// <param name="newContent">The new content</param>
    /// <returns>Result indicating success or failure</returns>
    public Result UpdateContent(string newContent)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            return Error.Validation("Message content cannot be null or empty");

        if (newContent.Length > 100_000)
            return Error.Validation("Message content cannot exceed 100,000 characters");

        if (Status != MessageStatus.Draft)
            return Error.Validation("Can only update content of draft messages");

        Content = newContent.Trim();
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    /// <summary>
    /// Adds or updates metadata for the message
    /// </summary>
    /// <param name="key">The metadata key</param>
    /// <param name="value">The metadata value</param>
    /// <returns>Result indicating success or failure</returns>
    public Result AddOrUpdateMetadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Error.Validation("Metadata key cannot be null or empty");

        if (key.Length > 100)
            return Error.Validation("Metadata key cannot exceed 100 characters");

        if (value?.Length > 1000)
            return Error.Validation("Metadata value cannot exceed 1000 characters");

        var mutableMetadata = new Dictionary<string, string>(Metadata)
        {
            [key.Trim()] = value?.Trim() ?? string.Empty
        };

        Metadata = mutableMetadata;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    /// <summary>
    /// Determines if this message can be linked to another message
    /// </summary>
    /// <param name="otherMessage">The other message to link to</param>
    /// <returns>True if linking is allowed</returns>
    public bool CanLinkTo(Message otherMessage)
    {
        ArgumentNullException.ThrowIfNull(otherMessage);
        
        // Can link if they're in the same conversation and this message comes after the other
        return ConversationId == otherMessage.ConversationId 
               && CreatedAt >= otherMessage.CreatedAt
               && Id != otherMessage.Id;
    }

    /// <summary>
    /// Determines if this message is a user message
    /// </summary>
    public bool IsUserMessage => Role.IsUser;

    /// <summary>
    /// Determines if this message is an assistant message
    /// </summary>
    public bool IsAssistantMessage => Role.IsAssistant;

    /// <summary>
    /// Determines if this message is a system message
    /// </summary>
    public bool IsSystemMessage => Role.IsSystem;

    /// <summary>
    /// Determines if this message can be processed
    /// </summary>
    public bool CanBeProcessed => Status.CanBeProcessed;

    /// <summary>
    /// Gets the processing duration if the message has been processed
    /// </summary>
    public TimeSpan? ProcessingDuration => 
        ProcessedAt.HasValue ? ProcessedAt.Value - CreatedAt : null;
}