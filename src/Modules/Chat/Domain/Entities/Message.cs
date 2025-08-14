using BuildingBlocks.Core.Domain.Primitives;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Chat.Domain.Entities;

/// <summary>
/// Represents an immutable message entity within a conversation.
/// Created only by the Conversation aggregate to ensure sequence integrity.
/// </summary>
public sealed class Message : AuditableDeletableEntity<MessageId>
{
    public ConversationId ConversationId { get; }
    public MessageRole Role { get; }
    public MessageContent Content { get; }
    public int Sequence { get; }

    private Message() : base(MessageId.New()) 
    {
        // Required for EF Core - properties will be set during deserialization
        ConversationId = null!;
        Role = null!;
        Content = null!;
    }

    internal Message(
        MessageId id,
        ConversationId conversationId,
        MessageRole role,
        MessageContent content,
        int sequence)
        : base(id)
    {
        ConversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Sequence = sequence;
    }

    /// <summary>
    /// Internal factory for Conversation aggregate use only.
    /// Ensures messages are created with proper sequence assignment.
    /// </summary>
    internal static Message Create(
        ConversationId conversationId,
        MessageRole role,
        MessageContent content,
        int sequence)
    {
        if (sequence <= 0)
            throw new ArgumentException("Sequence must be positive.", nameof(sequence));

        return new Message(
            MessageId.New(),
            conversationId,
            role,
            content,
            sequence);
    }
}