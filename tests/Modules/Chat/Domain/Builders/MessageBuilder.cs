using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Builders;

/// <summary>
/// Fluent builder for creating Message entities in tests.
/// Provides realistic defaults and allows customization for specific test scenarios.
/// Note: Messages are typically created through Conversation aggregate methods,
/// but this builder is useful for testing Message-specific logic and validation.
/// </summary>
public class MessageBuilder
{
    private ConversationId _conversationId = ConversationId.New();
    private MessageRole _role = MessageRole.User;
    private string _content = TestConstants.Messages.DefaultUserMessage;
    private int _sequence = 1;
    private AiResponseId? _aiResponseId;

    /// <summary>
    /// Creates a new MessageBuilder with defaults for a user message.
    /// </summary>
    public static MessageBuilder NewUserMessage() => new MessageBuilder()
        .WithRole(MessageRole.User)
        .WithContent(TestConstants.Messages.DefaultUserMessage);

    /// <summary>
    /// Creates a new MessageBuilder with defaults for an assistant message.
    /// </summary>
    public static MessageBuilder NewAssistantMessage() => new MessageBuilder()
        .WithRole(MessageRole.Assistant)
        .WithContent(TestConstants.Messages.DefaultAssistantMessage)
        .WithAiResponseId(TestConstants.AiResponses.DefaultAiResponseId);

    /// <summary>
    /// Sets the conversation ID this message belongs to.
    /// </summary>
    public MessageBuilder InConversation(ConversationId conversationId)
    {
        _conversationId = conversationId;
        return this;
    }

    /// <summary>
    /// Sets the message role (User or Assistant).
    /// </summary>
    public MessageBuilder WithRole(MessageRole role)
    {
        _role = role;
        return this;
    }

    /// <summary>
    /// Sets the message content.
    /// </summary>
    public MessageBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Sets content that exceeds the maximum length (for validation testing).
    /// </summary>
    public MessageBuilder WithTooLongContent()
    {
        _content = TestConstants.Messages.TooLongMessage;
        return this;
    }

    /// <summary>
    /// Sets empty content (for validation testing).
    /// </summary>
    public MessageBuilder WithEmptyContent()
    {
        _content = TestConstants.Messages.EmptyMessage;
        return this;
    }

    /// <summary>
    /// Sets the sequence number for this message in the conversation.
    /// </summary>
    public MessageBuilder WithSequence(int sequence)
    {
        _sequence = sequence;
        return this;
    }

    /// <summary>
    /// Sets the AI response ID (required for assistant messages).
    /// </summary>
    public MessageBuilder WithAiResponseId(AiResponseId aiResponseId)
    {
        _aiResponseId = aiResponseId;
        return this;
    }

    /// <summary>
    /// Removes the AI response ID (should only be used for user messages).
    /// </summary>
    public MessageBuilder WithoutAiResponseId()
    {
        _aiResponseId = null;
        return this;
    }

    /// <summary>
    /// Creates a Message entity using internal factory methods.
    /// This simulates how messages are created by the Conversation aggregate.
    /// Throws if the message cannot be created due to invalid data.
    /// </summary>
    public Message Build()
    {
        var messageContent = MessageContent.From(_content);

        return _role.IsUser 
            ? Message.CreateUserMessage(_conversationId, messageContent, _sequence)
            : Message.CreateAssistantMessage(_conversationId, messageContent, _sequence, _aiResponseId!);
    }
}