using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Aggregates;

/// <summary>
/// Conversation aggregate root following SPARC architecture patterns
/// Manages messages with proper domain invariants and event sourcing capability
/// </summary>
public sealed class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = new();

    /// <summary>
    /// Gets the ordered messages in this conversation (aggregate accessor pattern)
    /// </summary>
    public IReadOnlyList<Message> MessagesOrdered => 
        _messages.OrderBy(m => m.Sequence).ToList();

    /// <summary>
    /// The conversation title
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// The conversation status following SPARC enum pattern
    /// </summary>
    public ConversationStatus Status { get; private set; }

    /// <summary>
    /// When the conversation was completed (if applicable)
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Whether the conversation is active
    /// </summary>
    public bool IsActive => Status == ConversationStatus.Active;

    /// <summary>
    /// The current message count
    /// </summary>
    public int MessageCount => _messages.Count;

    // Private constructor for EF Core
    private Conversation() : base(default!)
    {
        Title = string.Empty;
        Status = ConversationStatus.Active;
    }

    private Conversation(ConversationId id, string title) : base(id)
    {
        Title = title;
        Status = ConversationStatus.Active;
    }

    /// <summary>
    /// Creates a new conversation following SPARC factory pattern
    /// </summary>
    public static Result<Conversation> Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Conversation title cannot be null or empty");

        if (title.Length > 200)
            return Error.Validation("Conversation title cannot exceed 200 characters");

        var conversationId = ConversationId.New();
        return new Conversation(conversationId, title.Trim());
    }

    /// <summary>
    /// Adds a message to the conversation with proper business rule validation
    /// </summary>
    public Result<Message> AddMessage(string content, MessageRole role, Dictionary<string, object>? metadata = null)
    {
        if (Status != ConversationStatus.Active)
            return Error.Validation("Cannot add messages to inactive conversation");

        // Determine sequence number
        var sequence = _messages.Count + 1;
        
        // Create the new message
        var messageResult = Message.Create(content, Id, role, sequence, metadata);
        if (messageResult.IsFailure)
            return messageResult.Error;

        var message = messageResult.Value;
        _messages.Add(message);

        // Raise domain event
        var isFirstMessage = _messages.Count == 1;
        RaiseDomainEvent(new MessageAddedDomainEvent(Id, message.Id, content, role.Value, isFirstMessage));

        return message;
    }

    /// <summary>
    /// Completes the conversation following SPARC pattern
    /// </summary>
    public Result CompleteConversation()
    {
        if (Status != ConversationStatus.Active)
            return Error.Validation("Can only complete active conversations");

        Status = ConversationStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        // Raise domain event
        RaiseDomainEvent(new ConversationCompletedDomainEvent(
            Id,
            MessageCount,
            0, // Tool execution count removed per SPARC
            "Completed",
            CompletedAt.Value));

        return Result.Success();
    }

    /// <summary>
    /// Updates the conversation title
    /// </summary>
    public Result UpdateTitle(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            return Error.Validation("Title cannot be null or empty");

        if (newTitle.Length > 200)
            return Error.Validation("Title cannot exceed 200 characters");

        Title = newTitle.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Applies a domain event to this aggregate for event sourcing replay scenarios.
    /// Handles state reconstruction from event stream.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply</param>
    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case MessageAddedDomainEvent messageAdded:
                ApplyMessageAddedEvent(messageAdded);
                break;
            case ConversationCompletedDomainEvent conversationCompleted:
                ApplyConversationCompletedEvent(conversationCompleted);
                break;
            default:
                // Unknown event type - ignore for forward compatibility
                break;
        }
    }

    /// <summary>
    /// Creates a snapshot of the current conversation state for event sourcing optimization.
    /// </summary>
    /// <returns>A snapshot containing the current conversation state</returns>
    protected override object CreateSnapshot()
    {
        return new ConversationSnapshot
        {
            Id = Id,
            Title = Title,
            Status = Status,
            CompletedAt = CompletedAt,
            Messages = _messages.ToList(),
            MessageCount = MessageCount,
            CreatedAt = DateTime.UtcNow // Approximation for snapshot timing
        };
    }

    /// <summary>
    /// Restores the conversation state from a snapshot for event sourcing optimization.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore from</param>
    protected override void RestoreFromSnapshot(object snapshot)
    {
        if (snapshot is not ConversationSnapshot conversationSnapshot)
            throw new ArgumentException("Invalid snapshot type for Conversation aggregate", nameof(snapshot));

        // Restore aggregate state from snapshot
        Title = conversationSnapshot.Title;
        Status = conversationSnapshot.Status;
        CompletedAt = conversationSnapshot.CompletedAt;
        
        // Restore messages collection
        _messages.Clear();
        _messages.AddRange(conversationSnapshot.Messages);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Method designed for future event sourcing instance state manipulation")]
    private void ApplyMessageAddedEvent(MessageAddedDomainEvent messageAdded)
    {
        // Note: In a full event sourcing implementation, we would reconstruct the Message entity
        // from the event data rather than assuming it exists in the current state
        // This is a simplified implementation for the current architecture
        
        // For full event sourcing, we would recreate the Message entity from event data
        // Currently keeping method for future event sourcing expansion
        _ = messageAdded; // Acknowledge parameter usage for future implementation
    }

    private void ApplyConversationCompletedEvent(ConversationCompletedDomainEvent conversationCompleted)
    {
        Status = ConversationStatus.Completed;
        CompletedAt = conversationCompleted.CompletedAt;
    }
}

/// <summary>
/// Snapshot data structure for Conversation aggregate event sourcing optimization.
/// Contains the essential state needed to reconstruct the aggregate without replaying all events.
/// </summary>
public sealed class ConversationSnapshot
{
    public required ConversationId Id { get; init; }
    public required string Title { get; init; }
    public required ConversationStatus Status { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required List<Message> Messages { get; init; }
    public required int MessageCount { get; init; }
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Conversation status enumeration following SPARC patterns
/// </summary>
public enum ConversationStatus
{
    Active,
    Completed,
    Archived
}