using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Tests.Builders;

/// <summary>
/// Fluent builder for creating Conversation aggregates in tests.
/// Provides realistic defaults and allows customization for specific test scenarios.
/// Uses the Builder pattern to create test data in a readable, maintainable way.
/// </summary>
public class ConversationBuilder
{
    private AxonUserId _ownerId = TestConstants.Users.DefaultOwnerId;
    private string? _title = TestConstants.Conversations.DefaultTitle;
    private readonly List<(MessageRole role, string content, AiResponseId? aiResponseId)> _messages = [];
    private readonly List<string> _expectedDomainEvents = [];
    private ConversationStatus _expectedStatus = ConversationStatus.Active;
    private DateTimeOffset _creationTime = TestConstants.DateTimes.DefaultTestTime;
    private FakeTimeProvider? _timeProvider;
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
    /// Default: Conversation with invalid owner (empty AxonUserId).
    /// </summary>
    public static ConversationBuilder Invalid() => new ConversationBuilder().WithOwner(default(AxonUserId));

    /// <summary>
    /// Sets the conversation owner.
    /// </summary>
    public ConversationBuilder WithOwner(AxonUserId ownerId)
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
    public ConversationBuilder WithTimeProvider(FakeTimeProvider timeProvider)
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
        _expectedDomainEvents.Add(nameof(UserMessageAppendedEvent));
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
        // Generate unique AI response ID if none provided
        var responseId = aiResponseId ?? new AiResponseId($"ai-response-{Guid.NewGuid()}");
        _messages.Add((MessageRole.Assistant, content, responseId));
        _expectedDomainEvents.Add(nameof(AssistantMessageAppendedEvent));
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
                var aiResponseId = new AiResponseId($"ai-response-{i + 1}");
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
        // Create a conversation that has reached its maximum capacity
        // We need exactly 10,000 messages to trigger the "at capacity" rule
        const int maxMessages = 10_000;
        
        for (int i = 0; i < maxMessages; i++)
        {
            if (i % 2 == 0)
            {
                WithUserMessage($"User {i + 1}");
            }
            else
            {
                var aiResponseId = new AiResponseId($"ai-{i + 1}");
                WithAssistantMessage($"Assistant {i + 1}", aiResponseId);
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
        _expectedStatus = ConversationStatus.Completed;
        _expectedDomainEvents.Add(nameof(ConversationCompletedEvent));
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
        
        // Track expected domain events
        if (!_expectedDomainEvents.Contains(nameof(ConversationStartedEvent)))
            _expectedDomainEvents.Add(nameof(ConversationStartedEvent));

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
                : conversation.AppendAssistantResponseToConversation(messageContentResult.Value, aiResponseId ?? TestConstants.AiResponses.DefaultAiResponseId, timeProvider);

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
    private FakeTimeProvider CreateTestTimeProvider()
    {
        return new FakeTimeProvider(_creationTime);
    }

    /// <summary>
    /// Gets the expected domain events that should be raised during conversation building.
    /// Useful for testing domain event publishing.
    /// </summary>
    public IReadOnlyList<string> GetExpectedDomainEvents() => _expectedDomainEvents.AsReadOnly();

    /// <summary>
    /// Gets the expected final status of the conversation.
    /// </summary>
    public ConversationStatus GetExpectedStatus() => _expectedStatus;

    /// <summary>
    /// Creates a conversation for testing business rule violations.
    /// </summary>
    public ConversationBuilder ForBusinessRuleTesting()
    {
        // Configure for common business rule testing scenarios
        return this.WithTitle(null).WithOwner(TestConstants.Users.DefaultOwnerId);
    }

    /// <summary>
    /// Creates a conversation that violates the message limit rule.
    /// </summary>
    public ConversationBuilder ThatViolatesMessageLimit()
    {
        // Add more messages than allowed (testing edge case)
        for (int i = 0; i < TestConstants.Limits.MaxConversationMessages + 1; i++)
        {
            if (i % 2 == 0)
            {
                WithUserMessage($"Message {i + 1}");
            }
            else
            {
                var aiId = new AiResponseId($"ai-response-{i + 1}");
                WithAssistantMessage($"Response {i + 1}", aiId);
            }
        }
        return this;
    }

    /// <summary>
    /// Creates a conversation for testing error scenarios.
    /// </summary>
    public ConversationBuilder WithInvalidData()
    {
        return this.WithOwner(new AxonUserId(Guid.Empty))
                  .WithTitle(TestConstants.EdgeCases.OneOverMaxTitle);
    }
}