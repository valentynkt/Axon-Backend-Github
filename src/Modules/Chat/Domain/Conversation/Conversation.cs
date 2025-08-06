using Axon.Modules.Chat.Domain.Conversation.Entities;
using Axon.Modules.Chat.Domain.Conversation.Events;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Model;

namespace Axon.Modules.Chat.Domain.Conversation;

/// <summary>
/// Conversation aggregate (DDD/CQRS).
/// Minimal, behavior-first model focused on user messages.
/// Inherits BaseAggregate for identity/versioning/auditing/event buffer.
/// </summary>
public sealed record Conversation : BaseAggregate<ConversationId>
{
    private readonly List<Message> _messages = new();

    /// <summary>Conversation owner.</summary>
    public UserId OwnerId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public ConversationStatus Status { get; private set; } = ConversationStatus.Active;

    /// <summary>Completion timestamp (if completed).</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>True when conversation accepts new messages.</summary>
    public bool IsActive => Status == ConversationStatus.Active;

    /// <summary>Current number of messages.</summary>
    public int MessageCount => _messages.Count;

    /// <summary>Read-only view of messages (in insertion/sequence order).</summary>
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    // EF Core / serialization
    private Conversation() { OwnerId = default!; }

    private Conversation(ConversationId id, UserId ownerId, string title)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        Status = ConversationStatus.Active;

        AddDomainEvent(new ConversationStartedDomainEvent(Id, OwnerId, Title));
    }

    /// <summary>
    /// Start a new conversation. Validates owner and title, raises ConversationStarted.
    /// </summary>
    public static Result<Conversation> Start(string title, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ChatErrors.Conversation.OwnerRequired;

        title = (title ?? string.Empty).Trim();
        if (title.Length == 0)
            return ChatErrors.Conversation.CannotStart("Title cannot be empty");
        if (title.Length > 200)
            return ChatErrors.Conversation.CannotStart("Title cannot exceed 200 characters");

        var ownerIdResult = UserId.Create(userId);
        if (ownerIdResult.IsFailure)
            return ownerIdResult.Error;

        var conversation = new Conversation(ConversationId.New(), ownerIdResult.Value, title);
        return conversation;
    }

    /// <summary>
    /// Append a user message (ownership/status/content invariants). Raises UserMessageAppended.
    /// Sequence = 1-based ordinal within this conversation.
    /// </summary>
    public Result<Message> AppendUserMessage(string content, string requestingUserId)
    {
        if (!BelongsToUser(requestingUserId))
            return ChatErrors.Conversation.NotConversationOwner(requestingUserId, Id.ToString());

        if (!IsActive)
            return ChatErrors.Conversation.ConversationNotActive;

        content = (content ?? string.Empty).Trim();
        if (content.Length == 0)
            return ChatErrors.Conversation.InvalidMessageContent("Content cannot be empty");
        if (content.Length > 100_000)
            return ChatErrors.Conversation.InvalidMessageContent("Content cannot exceed 100,000 characters");

        var nextSeq = _messages.Count + 1;

        var messageResult = Message.Create(
            content: content,
            conversationId: Id,
            role: MessageRole.User,
            sequence: nextSeq);

        if (messageResult.IsFailure)
            return messageResult.Error;

        var message = messageResult.Value;
        _messages.Add(message);

        // Validate sequence integrity after modification
        var validationResult = ValidateSequenceIntegrity();
        if (validationResult.IsFailure)
            return validationResult.Error;

        AddDomainEvent(new UserMessageAppendedDomainEvent(
            Id,
            message.Id,
            nextSeq,
            requestingUserId));

        return message;
    }

    /// <summary>Update the title (trimmed,  200 chars). No event (YAGNI).</summary>
    public Result UpdateTitle(string newTitle)
    {
        newTitle = (newTitle ?? string.Empty).Trim();
        if (newTitle.Length == 0)
            return Error.Validation("Title cannot be empty");
        if (newTitle.Length > 200)
            return Error.Validation("Title cannot exceed 200 characters");

        Title = newTitle;
        return Result.Success();
    }

    /// <summary>Complete the conversation. No event (kept minimal).</summary>
    public Result Complete()
    {
        if (!IsActive)
            return Error.Validation("Only active conversations can be completed.");

        Status = ConversationStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Invariant: does this conversation belong to the given user?</summary>
    public bool BelongsToUser(string userId) =>
        !string.IsNullOrWhiteSpace(userId) &&
        OwnerId.Value.Equals(userId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Optional probe: validate sequence integrity (1..N).</summary>
    public Result ValidateSequenceIntegrity()
    {
        for (int i = 0; i < _messages.Count; i++)
        {
            var expected = i + 1;
            if (_messages[i].Sequence != expected)
                return ChatErrors.Conversation.SequenceViolation(expected, _messages[i].Sequence);
        }
        return Result.Success();
    }
}

public enum ConversationStatus
{
    Active,
    Completed,
    Archived
}
