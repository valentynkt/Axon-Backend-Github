using Axon.Modules.Chat.Domain.Tests.Common;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Tests.Builders;

/// <summary>
/// Fluent builder for creating Conversation aggregates in tests.
/// Provides realistic defaults and allows customization for specific test scenarios.
/// Uses the Builder pattern to create test data in a readable, maintainable way.
/// </summary>
public class ConversationBuilder
{
    private UserId _ownerId = TestConstants.Users.DefaultOwnerId;
    private string? _title = TestConstants.Conversations.DefaultTitle;
    private readonly List<(MessageRole role, string content, AiResponseId? aiResponseId)> _messages = new();
    private DateTimeOffset _creationTime = TestConstants.DateTimes.DefaultTestTime;
    private TimeProvider? _timeProvider;
    private bool _shouldComplete;

    /// <summary>
    /// Creates a new ConversationBuilder with sensible defaults.
    /// Default: Active conversation owned by DefaultOwnerId with DefaultTitle.
    /// </summary>
    public static ConversationBuilder New() => new();

    /// <summary>
    /// Creates a new ConversationBuilder for a minimal valid conversation.
    /// Default: Active conversation with no title and no messages.
    /// </summary>
    public static ConversationBuilder Minimal() => new ConversationBuilder().WithTitle(null);

    /// <summary>
    /// Creates a new ConversationBuilder for an invalid scenario.
    /// Default: Conversation with invalid owner (empty UserId).
    /// </summary>
    public static ConversationBuilder Invalid() => new ConversationBuilder().WithOwner(default(UserId));

    /// <summary>
    /// Sets the conversation owner.
    /// </summary>
    public ConversationBuilder WithOwner(UserId ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    /// <summary>
    /// Sets the conversation title. Pass null for no title.
    /// </summary>
    public ConversationBuilder WithTitle(string? title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets a title that will exceed maximum length (for validation testing).
    /// </summary>
    public ConversationBuilder WithTooLongTitle()
    {
        _title = TestConstants.Conversations.TooLongTitle;
        return this;
    }

    /// <summary>
    /// Sets the creation time for the conversation.
    /// </summary>
    public ConversationBuilder AtTime(DateTimeOffset creationTime)
    {
        _creationTime = creationTime;
        return this;
    }

    /// <summary>
    /// Sets a custom TimeProvider for the conversation creation.
    /// </summary>
    public ConversationBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    /// <summary>
    /// Adds a user message to the conversation.
    /// Messages are added in the order they are specified.
    /// </summary>
    public ConversationBuilder WithUserMessage(string content = TestConstants.Messages.DefaultUserMessage)
    {
        _messages.Add((MessageRole.User, content, null));
        return this;
    }

    /// <summary>
    /// Adds an assistant message to the conversation.
    /// Requires an AI response ID for tracking.
    /// </summary>
    public ConversationBuilder WithAssistantMessage(
        string content = TestConstants.Messages.DefaultAssistantMessage,
        AiResponseId? aiResponseId = null)
    {
        var responseId = aiResponseId ?? TestConstants.AiResponses.DefaultAiResponseId;
        _messages.Add((MessageRole.Assistant, content, responseId));
        return this;
    }

    /// <summary>
    /// Adds multiple alternating user and assistant messages.
    /// Useful for creating realistic conversation flows.
    /// </summary>
    public ConversationBuilder WithAlternatingMessages(int messageCount = 4)
    {
        for (int i = 0; i < messageCount; i++)
        {
            if (i % 2 == 0)
            {
                WithUserMessage($"User message {i + 1}");
            }
            else
            {
                var aiResponseId = AiResponseId.From($"ai-response-{i + 1}");
                WithAssistantMessage($"Assistant response {i + 1}", aiResponseId);
            }
        }
        return this;
    }

    /// <summary>
    /// Adds messages up to the conversation limit for testing edge cases.
    /// </summary>
    public ConversationBuilder WithMaximumMessages()
    {
        // Add messages close to the limit for testing
        for (int i = 0; i < 100; i++) // Use smaller number for performance
        {
            if (i % 2 == 0)
            {
                WithUserMessage($"User message {i + 1}");
            }
            else
            {
                var aiResponseId = AiResponseId.From($"ai-response-{i + 1}");
                WithAssistantMessage($"Assistant response {i + 1}", aiResponseId);
            }
        }
        return this;
    }

    /// <summary>
    /// Marks the conversation to be completed after creation and message addition.
    /// </summary>
    public ConversationBuilder ThatShouldBeCompleted()
    {
        _shouldComplete = true;
        return this;
    }

    /// <summary>
    /// Builds and returns a valid Conversation aggregate.
    /// Throws if the conversation cannot be created due to invalid data.
    /// Use BuildResult() for non-throwing version.
    /// </summary>
    public Conversation Build()
    {
        var result = BuildResult();
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to build conversation: {result.Error.Message}");
        }
        return result.Value;
    }

    /// <summary>
    /// Builds and returns a Result containing either a valid Conversation or an Error.
    /// Preferred method for testing error scenarios.
    /// </summary>
    public Result<Conversation, Error> BuildResult()
    {
        var timeProvider = _timeProvider ?? CreateTestTimeProvider();

        // Create the conversation
        var conversationResult = Conversation.StartNewConversation(_ownerId, _title, timeProvider);
        if (conversationResult.IsFailure)
        {
            return conversationResult;
        }

        var conversation = conversationResult.Value;

        // Add messages in sequence
        foreach (var (role, content, aiResponseId) in _messages)
        {
            var messageContentResult = MessageContent.Create(content);
            if (messageContentResult.IsFailure)
            {
                return Result.Failure<Conversation, Error>(messageContentResult.Error);
            }

            Result<Message, Error> messageResult = role.IsUser 
                ? conversation.AppendUserMessageToConversation(messageContentResult.Value, timeProvider)
                : conversation.AppendAssistantResponseToConversation(messageContentResult.Value, aiResponseId!, timeProvider);

            if (messageResult.IsFailure)
            {
                return Result.Failure<Conversation, Error>(messageResult.Error);
            }
        }

        // Complete if requested
        if (_shouldComplete && conversation.MessageCount > 0)
        {
            var completeResult = conversation.Complete(timeProvider);
            if (completeResult.IsFailure)
            {
                return Result.Failure<Conversation, Error>(completeResult.Error);
            }
        }

        return Result.Success<Conversation, Error>(conversation);
    }

    /// <summary>
    /// Creates a test TimeProvider that returns the specified creation time.
    /// </summary>
    private TimeProvider CreateTestTimeProvider()
    {
        return new FakeTimeProvider(_creationTime);
    }
}