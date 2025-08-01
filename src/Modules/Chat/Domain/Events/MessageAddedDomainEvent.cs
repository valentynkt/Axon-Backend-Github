using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a message is added to a conversation
/// </summary>
public sealed record MessageAddedDomainEvent : DomainEvent
{
    /// <summary>
    /// The ID of the conversation the message was added to
    /// </summary>
    public ConversationId ConversationId { get; }

    /// <summary>
    /// The ID of the message that was added
    /// </summary>
    public MessageId MessageId { get; }

    /// <summary>
    /// The content of the message
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// The role that sent the message (user, assistant, system)
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// Whether this message is the first message in the conversation
    /// </summary>
    public bool IsFirstMessage { get; }

    public MessageAddedDomainEvent(
        ConversationId conversationId,
        MessageId messageId,
        string content,
        string role,
        bool isFirstMessage)
    {
        ConversationId = conversationId;
        MessageId = messageId;
        Content = content;
        Role = role;
        IsFirstMessage = isFirstMessage;
    }
}