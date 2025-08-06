using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Model;

namespace Axon.Modules.Chat.Domain.Conversation.Entities;

/// <summary>
/// Message entity (child of Conversation aggregate).
/// Minimal, immutable-after-create state; no domain events here (Conversation raises them).
/// </summary>
public sealed record Message : BaseAuditableEntity<MessageId>
{
    /// <summary>Owning conversation.</summary>
    public ConversationId ConversationId { get; private set; }

    /// <summary>Actor role (e.g., User). Conversation enforces who may append.</summary>
    public MessageRole Role { get; private set; }

    /// <summary>Plain text content. Trimmed; max 100k chars.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// 1-based, monotonically increasing ordinal inside its Conversation
    /// (assigned by Conversation).
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>Optional metadata bag (domain-agnostic; infra maps to JSONB).</summary>
    public Dictionary<string, object?>? Metadata { get; private set; }

    // EF Core / serializers
    private Message() { ConversationId = default!; Role = default!; }

    private Message(
        MessageId id,
        ConversationId conversationId,
        MessageRole role,
        string content,
        int sequence,
        Dictionary<string, object?>? metadata)
    {
        Id = id;
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Sequence = sequence;
        Metadata = metadata;
    }

    /// <summary>
    /// Factory: validates content and sequence; Conversation controls ownership/role.
    /// </summary>
    public static Result<Message> Create(
        string content,
        ConversationId conversationId,
        MessageRole role,
        int sequence,
        Dictionary<string, object?>? metadata = null)
    {
        content = (content ?? string.Empty).Trim();
        if (content.Length == 0)
            return Error.Validation("Content cannot be empty.");
        if (content.Length > 100_000)
            return Error.Validation("Content cannot exceed 100,000 characters.");
        if (sequence <= 0)
            return Error.Validation("Sequence must be a positive integer.");

        var message = new Message(
            id: MessageId.New(),
            conversationId: conversationId,
            role: role,
            content: content,
            sequence: sequence,
            metadata: metadata);

        return message;
    }
}
