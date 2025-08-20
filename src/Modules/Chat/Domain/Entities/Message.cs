using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Core.Domain.Primitives;
using Axon.Modules.Chat.Domain.ValueObjects;
;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Primitives.Ids;

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
    public AiResponseId? AiResponseId { get; private set; }

    private Message() : base(MessageId.New()) 
    {
        // Required for EF Core - properties will be set during deserialization
        ConversationId = null!;
        Role = null!;
        Content = null!;
    }

    private Message(
        MessageId id,
        ConversationId conversationId,
        MessageRole role,
        MessageContent content,
        int sequence,
        AiResponseId? aiResponseId)
        : base(id)
    {
        ConversationId = conversationId;
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Content = content;
        Sequence = sequence;
        AiResponseId = aiResponseId;
        
        // Post-construction validation
        ValidateInvariants();
    }

    /// <summary>
    /// Validates message invariants after construction
    /// </summary>
    private void ValidateInvariants()
    {
        ValidateAiResponseIdConsistency(Role, AiResponseId);
    }



    /// <summary>
    /// Factory method for creating assistant messages with REQUIRED AI response tracking.
    /// Assistant messages MUST have an AI response ID.
    /// </summary>
    internal static Message CreateAssistantMessage(
        ConversationId conversationId,
        MessageContent content,
        int sequence,
        AiResponseId aiResponseId)
    {
        ArgumentNullException.ThrowIfNull(aiResponseId, nameof(aiResponseId));

        return new Message(
            MessageId.New(),
            conversationId,
            MessageRole.Assistant,
            content,
            sequence,
            aiResponseId);
    }

    /// <summary>
    /// Factory method for creating user messages.
    /// User messages MUST NOT have an AI response ID.
    /// </summary>
    internal static Message CreateUserMessage(
        ConversationId conversationId,
        MessageContent content,
        int sequence)
    {
        return new Message(
            MessageId.New(),
            conversationId,
            MessageRole.User,
            content,
            sequence,
            aiResponseId: null); // User messages never have AI response ID
    }



    /// <summary>
    /// Validates the consistency between message role and AI response ID.
    /// </summary>
    private static void ValidateAiResponseIdConsistency(MessageRole role, AiResponseId? aiResponseId)
    {
        // Assistant messages MUST have an AI response ID
        if (role.IsAssistant && aiResponseId is null)
            throw new InvalidOperationException("Assistant messages must have an AI response ID");

        // User messages MUST NOT have an AI response ID
        if (role.IsUser && aiResponseId is not null)
            throw new InvalidOperationException("User messages cannot have an AI response ID");
    }
}