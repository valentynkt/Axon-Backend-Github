using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Tests.Builders;

/// <summary>
/// Fluent builder for creating Message entities in tests.
/// Provides realistic defaults and allows customization for specific test scenarios.
/// Note: Since Messages are created internally by Conversation aggregate,
/// this builder simulates the aggregate behavior for testing message validation and properties.
/// For full integration testing, use ConversationBuilder instead.
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
    /// Sets content with spaces that should be trimmed (for validation testing).
    /// </summary>
    public MessageBuilder WithContentRequiringTrim()
    {
        _content = TestConstants.Messages.ValidMessageWithSpaces;
        return this;
    }

    /// <summary>
    /// Sets the maximum allowed content length (for edge case testing).
    /// </summary>
    public MessageBuilder WithMaxLengthContent()
    {
        _content = TestConstants.Messages.MaxLengthMessage;
        return this;
    }

    /// <summary>
    /// Sets content with whitespace only (for validation testing).
    /// </summary>
    public MessageBuilder WithWhitespaceContent()
    {
        _content = TestConstants.Messages.WhitespaceMessage;
        return this;
    }

    /// <summary>
    /// Creates a Message entity using internal factory methods.
    /// This simulates how messages are created by the Conversation aggregate.
    /// Throws if the message cannot be created due to invalid data.
    /// Use BuildResult() for non-throwing version.
    /// </summary>
    public Message Build()
    {
        var result = BuildResult();
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to build message: {result.Error.Message}");
        }
        return result.Value;
    }

    /// <summary>
    /// Creates a Message entity returning a Result for proper error handling.
    /// Preferred method for testing error scenarios.
    /// </summary>
    public Result<Message, Error> BuildResult()
    {
        try
        {
            var messageContentResult = MessageContent.Create(_content);
            if (messageContentResult.IsFailure)
            {
                return Result.Failure<Message, Error>(messageContentResult.Error);
            }

            // Validate AI Response ID consistency before creating the message
            if (_role.IsUser && _aiResponseId is not null)
            {
                throw new InvalidOperationException("User messages cannot have an AI response ID");
            }

            if (_role.IsAssistant && _aiResponseId is null)
            {
                throw new InvalidOperationException("Assistant messages must have an AI response ID");
            }

            if (_role.IsUser)
            {
                var userMessage = Message.CreateUserMessage(_conversationId, messageContentResult.Value, _sequence);
                return Result.Success<Message, Error>(userMessage);
            }
            else
            {
                var aiResponseId = _aiResponseId!.Value;  // We know it's not null from validation above
                var assistantMessage = Message.CreateAssistantMessage(_conversationId, messageContentResult.Value, _sequence, aiResponseId);
                return Result.Success<Message, Error>(assistantMessage);
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<Message, Error>(
                Error.Validation($"Message creation failed: {ex.Message}", "MESSAGE.CREATION.FAILED"));
        }
    }

    /// <summary>
    /// Creates a message builder configured for testing business rule violations.
    /// </summary>
    public static MessageBuilder ForBusinessRuleTesting()
    {
        return new MessageBuilder()
            .WithSequence(TestConstants.BusinessRules.InvalidSequence)
            .WithContent(TestConstants.Messages.EmptyMessage);
    }

    /// <summary>
    /// Creates a message with invalid AI response ID consistency (user with AI ID or assistant without).
    /// </summary>
    public static MessageBuilder WithInvalidAiResponseIdConsistency()
    {
        return new MessageBuilder()
            .WithRole(MessageRole.User)
            .WithAiResponseId(TestConstants.AiResponses.DefaultAiResponseId); // Invalid: user with AI ID
    }

    /// <summary>
    /// Creates a message that violates turn-taking rules.
    /// </summary>
    public MessageBuilder ThatViolatesTurnTaking(MessageRole expectedPreviousRole)
    {
        // If previous was user, this should be assistant, but we set it to user (violation)
        var violatingRole = expectedPreviousRole.IsUser ? MessageRole.User : MessageRole.Assistant;
        return this.WithRole(violatingRole);
    }
}