using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Builders;

/// <summary>
/// Fluent builder for creating Chat test data with realistic scenarios.
/// Provides predefined scenarios and customizable builders for comprehensive testing.
/// </summary>
public sealed class ChatTestDataBuilder
{
    private readonly FakeTimeProvider _timeProvider;

    public ChatTestDataBuilder(FakeTimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Creates a conversation builder with default settings.
    /// </summary>
    public ConversationBuilder Conversation() => ConversationBuilder.Create(_timeProvider);

    /// <summary>
    /// Creates a message builder with default settings.
    /// </summary>
    public MessageBuilder Message() => new(_timeProvider);

    #region Predefined Scenarios

    /// <summary>
    /// Creates a basic conversation scenario with user and assistant messages.
    /// </summary>
    public static ConversationScenario BasicConversationScenario(FakeTimeProvider? timeProvider = null)
    {
        var provider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);
        var ownerId = AxonUserId.New();

        var conversation = ConversationBuilder.Create(provider)
            .WithOwner(ownerId)
            .WithTitle("Basic Test Conversation")
            .Build();

        var userMessage1 = conversation.AppendUserMessageToConversation(
            MessageContent.Create("Hello, can you help me with something?").Value,
            provider);

        var assistantMessage1 = conversation.AppendAssistantResponseToConversation(
            MessageContent.Create("Of course! I'd be happy to help you. What do you need assistance with?").Value,
            new AiResponseId(Guid.NewGuid().ToString()),
            provider);

        var userMessage2 = conversation.AppendUserMessageToConversation(
            MessageContent.Create("I need help understanding how to use this API.").Value,
            provider);

        var assistantMessage2 = conversation.AppendAssistantResponseToConversation(
            MessageContent.Create("I can explain the API usage. Let me provide you with detailed examples...").Value,
            new AiResponseId(Guid.NewGuid().ToString()),
            provider);

        userMessage1.IsSuccess.ShouldBeTrue();
        assistantMessage1.IsSuccess.ShouldBeTrue();
        userMessage2.IsSuccess.ShouldBeTrue();
        assistantMessage2.IsSuccess.ShouldBeTrue();

        return new ConversationScenario(conversation, ownerId, provider);
    }

    /// <summary>
    /// Creates a completed conversation scenario.
    /// </summary>
    public static ConversationScenario CompletedConversationScenario(FakeTimeProvider? timeProvider = null)
    {
        var scenario = BasicConversationScenario(timeProvider);
        var completeResult = scenario.Conversation.Complete(scenario.TimeProvider);
        completeResult.IsSuccess.ShouldBeTrue();

        return scenario;
    }

    /// <summary>
    /// Creates a conversation with many messages for testing performance and limits.
    /// </summary>
    public static ConversationScenario LargeConversationScenario(
        int messageCount = 20,
        FakeTimeProvider? timeProvider = null)
    {
        var provider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);
        var ownerId = AxonUserId.New();

        var conversation = ConversationBuilder.Create(provider)
            .WithOwner(ownerId)
            .WithTitle("Large Test Conversation")
            .Build();

        for (int i = 0; i < messageCount; i++)
        {
            if (i % 2 == 0)
            {
                // User message
                var userResult = conversation.AppendUserMessageToConversation(
                    MessageContent.Create($"User message number {i + 1}. This contains some test content to make it realistic.").Value,
                    provider);
                userResult.IsSuccess.ShouldBeTrue();
            }
            else
            {
                // Assistant message
                var assistantResult = conversation.AppendAssistantResponseToConversation(
                    MessageContent.Create($"Assistant response number {i + 1}. This is a detailed response with helpful information.").Value,
                    new AiResponseId(Guid.NewGuid().ToString()),
                    provider);
                assistantResult.IsSuccess.ShouldBeTrue();
            }

            // Advance time slightly for each message
            provider.Advance(TimeSpan.FromSeconds(30));
        }

        return new ConversationScenario(conversation, ownerId, provider);
    }

    /// <summary>
    /// Creates multiple conversations for the same owner.
    /// </summary>
    public static MultipleConversationsScenario MultipleConversationsForOwnerScenario(
        AxonUserId? ownerId = null,
        FakeTimeProvider? timeProvider = null)
    {
        var provider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);
        var owner = ownerId ?? AxonUserId.New();

        var activeConversation = ConversationBuilder.Create(provider)
            .WithOwner(owner)
            .WithTitle("Active Conversation")
            .WithUserMessage("Hello there!")
            .WithAssistantMessage("Hi! How can I help you today?")
            .Build();

        provider.Advance(TimeSpan.FromHours(1));

        var completedConversation = ConversationBuilder.Create(provider)
            .WithOwner(owner)
            .WithTitle("Completed Conversation")
            .WithUserMessage("Quick question about pricing.")
            .WithAssistantMessage("I'd be happy to help with pricing information.")
            .Build();

        var completeResult = completedConversation.Complete(provider);
        completeResult.IsSuccess.ShouldBeTrue();

        provider.Advance(TimeSpan.FromHours(2));

        var longConversation = ConversationBuilder.Create(provider)
            .WithOwner(owner)
            .WithTitle("Long Technical Discussion")
            .WithUserMessage("I need help with a complex technical problem.")
            .WithAssistantMessage("I'll do my best to help. Can you describe the issue?")
            .WithUserMessage("It's related to database performance optimization.")
            .WithAssistantMessage("Database performance is crucial. Let me suggest some strategies...")
            .WithUserMessage("That's very helpful, thank you!")
            .WithAssistantMessage("You're welcome! Feel free to ask if you need more clarification.")
            .Build();

        return new MultipleConversationsScenario(
            owner,
            activeConversation,
            completedConversation,
            longConversation,
            provider);
    }

    #endregion
}

/// <summary>
/// Fluent builder for creating Conversation entities with customizable properties.
/// </summary>
public sealed class ConversationBuilder
{
    private readonly FakeTimeProvider _timeProvider;
    private AxonUserId? _ownerId;
    private string? _title;
    private readonly List<(string content, MessageRole role, AiResponseId? aiResponseId)> _messages = new();

    private ConversationBuilder(FakeTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public static ConversationBuilder Create(FakeTimeProvider timeProvider) => new(timeProvider);

    public ConversationBuilder WithOwner(AxonUserId ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public ConversationBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public ConversationBuilder WithUserMessage(string content)
    {
        _messages.Add((content, MessageRole.User, null));
        return this;
    }

    public ConversationBuilder WithAssistantMessage(string content, AiResponseId? aiResponseId = null)
    {
        _messages.Add((content, MessageRole.Assistant, aiResponseId ?? new AiResponseId(Guid.NewGuid().ToString())));
        return this;
    }

    public ConversationBuilder WithMessages(params (string content, MessageRole role)[] messages)
    {
        foreach (var (content, role) in messages)
        {
            if (role.IsUser)
            {
                WithUserMessage(content);
            }
            else
            {
                WithAssistantMessage(content);
            }
        }
        return this;
    }

    public Conversation Build()
    {
        var ownerId = _ownerId ?? AxonUserId.New();

        var conversationResult = Conversation.StartNewConversation(ownerId, _title, _timeProvider);
        conversationResult.IsSuccess.ShouldBeTrue();
        var conversation = conversationResult.Value;

        foreach (var (content, role, aiResponseId) in _messages)
        {
            if (role.IsUser)
            {
                var messageContent = MessageContent.Create(content).Value;
                var result = conversation.AppendUserMessageToConversation(messageContent, _timeProvider);
                result.IsSuccess.ShouldBeTrue();
            }
            else
            {
                var messageContent = MessageContent.Create(content).Value;
                var result = conversation.AppendAssistantResponseToConversation(
                    messageContent,
                    aiResponseId ?? new AiResponseId(Guid.NewGuid().ToString()),
                    _timeProvider);
                result.IsSuccess.ShouldBeTrue();
            }

            // Advance time slightly between messages
            _timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        return conversation;
    }
}

/// <summary>
/// Fluent builder for creating Message entities with customizable properties.
/// </summary>
public sealed class MessageBuilder
{
    private readonly FakeTimeProvider _timeProvider;
    private ConversationId? _conversationId;
    private MessageRole _role = MessageRole.User;
    private string _content = "Default test message content";
    private int _sequence = 1;
    private AiResponseId? _aiResponseId;

    internal MessageBuilder(FakeTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public MessageBuilder ForConversation(ConversationId conversationId)
    {
        _conversationId = conversationId;
        return this;
    }

    public MessageBuilder AsUser()
    {
        _role = MessageRole.User;
        _aiResponseId = null;
        return this;
    }

    public MessageBuilder AsAssistant(AiResponseId? aiResponseId = null)
    {
        _role = MessageRole.Assistant;
        _aiResponseId = aiResponseId ?? new AiResponseId(Guid.NewGuid().ToString());
        return this;
    }

    public MessageBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public MessageBuilder WithSequence(int sequence)
    {
        _sequence = sequence;
        return this;
    }

    public Message Build()
    {
        var conversationId = _conversationId ?? ConversationId.New();
        var messageContent = MessageContent.Create(_content).Value;

        return _role.IsUser
            ? Message.CreateUserMessage(conversationId, messageContent, _sequence)
            : Message.CreateAssistantMessage(conversationId, messageContent, _sequence, _aiResponseId ?? new AiResponseId(Guid.NewGuid().ToString()));
    }
}

#region Scenario Classes

/// <summary>
/// Represents a complete conversation test scenario with context.
/// </summary>
public sealed record ConversationScenario(
    Conversation Conversation,
    AxonUserId OwnerId,
    FakeTimeProvider TimeProvider);

/// <summary>
/// Represents a test scenario with multiple conversations for the same owner.
/// </summary>
public sealed record MultipleConversationsScenario(
    AxonUserId OwnerId,
    Conversation ActiveConversation,
    Conversation CompletedConversation,
    Conversation LongConversation,
    FakeTimeProvider TimeProvider);

#endregion