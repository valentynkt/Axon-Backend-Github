// /Axon/Modules/Chat/Domain/Entities/Message.cs
#nullable enable
using Axon.BuildingBlocks.Core.Primitives.ValueObjects; // MessageContent (Vogen)
using Axon.Modules.Chat.Domain.ValueObjects;            // MessageRole (Vogen)
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Entities;

/// <summary>
/// Immutable-by-construction message entity within a conversation.
/// Created only by the Conversation aggregate to ensure sequence integrity.
/// EF Core materializes via the private parameterless ctor.
/// </summary>
public sealed class Message : AuditableDeletableEntity<MessageId>
{
    public ConversationId ConversationId { get; private set; }
    public MessageRole    Role           { get; private set; }
    public MessageContent Content        { get; private set; }
    public int            Sequence       { get; private set; }
    public AiResponseId?  AiResponseId   { get; private set; }

    // EF Core materialization ctor
    private Message() : base() { }

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
        Role           = role;
        Content        = content;
        Sequence       = sequence;
        AiResponseId   = aiResponseId;

        ValidateInvariants();
    }

    /// <summary>
    /// Assistant messages MUST have an AI response id.
    /// </summary>
    internal static Message CreateAssistantMessage(
        ConversationId conversationId,
        MessageContent content,
        int sequence,
        AiResponseId aiResponseId)
        => new(
            new MessageId(Guid.CreateVersion7()),
            conversationId,
            MessageRole.Assistant,
            content,
            sequence,
            aiResponseId);

    /// <summary>
    /// User messages MUST NOT have an AI response id.
    /// </summary>
    internal static Message CreateUserMessage(
        ConversationId conversationId,
        MessageContent content,
        int sequence)
        => new(
            new MessageId(Guid.CreateVersion7()),
            conversationId,
            MessageRole.User,
            content,
            sequence,
            aiResponseId: null);

    // ----- invariants -----

    private void ValidateInvariants()
    {
        if (Sequence <= 0)
            throw new InvalidOperationException("Message sequence must be a positive integer.");

        ValidateAiResponseIdConsistency(Role, AiResponseId);
    }

    private static void ValidateAiResponseIdConsistency(MessageRole role, AiResponseId? aiResponseId)
    {
        if (role.IsAssistant && aiResponseId is null)
            throw new InvalidOperationException("Assistant messages must have an AI response ID.");

        if (role.IsUser && aiResponseId is not null)
            throw new InvalidOperationException("User messages cannot have an AI response ID.");
    }
}
