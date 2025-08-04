using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Entities;

/// <summary>
/// Message entity following SPARC architecture patterns
/// </summary>
public sealed class Message : AuditableEntity<MessageId>
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
    /// The role of the message sender
    /// </summary>
    public MessageRole Role { get; private set; }

    /// <summary>
    /// The sequence number for message ordering within conversation
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>
    /// Optional metadata associated with the message
    /// </summary>
    public Dictionary<string, object>? Metadata { get; private set; }

    // Private constructor for EF Core
    private Message() : base(default!)
    {
        Content = string.Empty;
    }

    private Message(
        MessageId id,
        string content,
        ConversationId conversationId,
        MessageRole role,
        int sequence,
        Dictionary<string, object>? metadata = null) : base(id)
    {
        Content = content;
        ConversationId = conversationId;
        Role = role;
        Sequence = sequence;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a new message following SPARC factory pattern
    /// </summary>
    public static Result<Message> Create(
        string content,
        ConversationId conversationId,
        MessageRole role,
        int sequence,
        Dictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Error.Validation("Message content cannot be null or empty");

        if (content.Length > 100_000) // 100KB limit per SPARC
            return Error.Validation("Message content cannot exceed 100,000 characters");

        if (sequence <= 0)
            return Error.Validation("Message sequence must be positive");

        var messageId = MessageId.New();
        return new Message(messageId, content.Trim(), conversationId, role, sequence, metadata);
    }

    /// <summary>
    /// Updates the message content (only for domain-specific scenarios)
    /// </summary>
    public Result UpdateContent(string newContent)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            return Error.Validation("Message content cannot be null or empty");

        if (newContent.Length > 100_000)
            return Error.Validation("Message content cannot exceed 100,000 characters");

        Content = newContent.Trim();
        return Result.Success();
    }
}