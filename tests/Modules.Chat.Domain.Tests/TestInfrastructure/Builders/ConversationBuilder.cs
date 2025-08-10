using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Time;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

/// <summary>
/// Fluent builder for creating Conversation aggregates with various configurations.
/// Follows the test data builder pattern for maintainable and expressive tests.
/// </summary>
public class ConversationBuilder
{
    private ConversationId? _id;
    private UserId? _ownerId;
    private ConversationTitle? _title;
    private ConversationStatus _status = ConversationStatus.Active;
    private readonly List<(MessageContent Content, MessageRole Role)> _messages = new();
    private IClock _clock = new FixedClock(DateTimeOffset.UtcNow);
    private DateTimeOffset? _createdAt;
    private DateTimeOffset? _updatedAt;
    private bool _useDefaultTitle = true;

    /// <summary>
    /// Creates a new ConversationBuilder instance.
    /// </summary>
    public static ConversationBuilder Create() => new();

    /// <summary>
    /// Sets a specific conversation ID.
    /// </summary>
    public ConversationBuilder WithId(ConversationId id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets a new random conversation ID.
    /// </summary>
    public ConversationBuilder WithNewId()
    {
        _id = ConversationId.New();
        return this;
    }

    /// <summary>
    /// Sets the owner of the conversation.
    /// </summary>
    public ConversationBuilder WithOwner(UserId ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    /// <summary>
    /// Sets a default test owner.
    /// </summary>
    public ConversationBuilder WithDefaultOwner()
    {
        _ownerId = UserId.New();
        return this;
    }

    /// <summary>
    /// Sets the conversation title.
    /// </summary>
    public ConversationBuilder WithTitle(string title)
    {
        _title = ConversationTitle.Create(title).Value;
        _useDefaultTitle = false;
        return this;
    }

    /// <summary>
    /// Sets an empty/default title.
    /// </summary>
    public ConversationBuilder WithDefaultTitle()
    {
        _title = null;
        _useDefaultTitle = true;
        return this;
    }

    /// <summary>
    /// Sets the conversation status to active.
    /// </summary>
    public ConversationBuilder Active()
    {
        _status = ConversationStatus.Active;
        return this;
    }

    /// <summary>
    /// Sets the conversation status to completed.
    /// </summary>
    public ConversationBuilder Completed()
    {
        _status = ConversationStatus.Completed;
        return this;
    }

    /// <summary>
    /// Adds a user message to the conversation.
    /// </summary>
    public ConversationBuilder WithUserMessage(string content)
    {
        var messageContent = MessageContent.Create(content).Value;
        _messages.Add((messageContent, MessageRole.User));
        return this;
    }

    /// <summary>
    /// Adds an assistant message to the conversation.
    /// </summary>
    public ConversationBuilder WithAssistantMessage(string content)
    {
        var messageContent = MessageContent.Create(content).Value;
        _messages.Add((messageContent, MessageRole.Assistant));
        return this;
    }

    /// <summary>
    /// Adds multiple messages in sequence.
    /// </summary>
    public ConversationBuilder WithMessages(params (string Content, MessageRole Role)[] messages)
    {
        foreach (var (content, role) in messages)
        {
            var messageContent = MessageContent.Create(content).Value;
            _messages.Add((messageContent, role));
        }
        return this;
    }

    /// <summary>
    /// Adds a conversation flow with alternating user and assistant messages.
    /// </summary>
    public ConversationBuilder WithConversationFlow(params string[] messages)
    {
        for (int i = 0; i < messages.Length; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var messageContent = MessageContent.Create(messages[i]).Value;
            _messages.Add((messageContent, role));
        }
        return this;
    }

    /// <summary>
    /// Sets a specific clock for time-based operations.
    /// </summary>
    public ConversationBuilder WithClock(IClock clock)
    {
        _clock = clock;
        return this;
    }

    /// <summary>
    /// Sets a fixed time for the conversation.
    /// </summary>
    public ConversationBuilder AtTime(DateTimeOffset time)
    {
        _clock = new FixedClock(time);
        _createdAt = time;
        _updatedAt = time;
        return this;
    }

    /// <summary>
    /// Sets specific creation and update times.
    /// </summary>
    public ConversationBuilder WithTimestamps(DateTimeOffset created, DateTimeOffset updated)
    {
        _createdAt = created;
        _updatedAt = updated;
        return this;
    }

    /// <summary>
    /// Creates a conversation near the message limit.
    /// </summary>
    public ConversationBuilder NearMessageLimit(int messagesShortOfLimit = 1)
    {
        var messageCount = 10000 - messagesShortOfLimit;
        for (int i = 0; i < messageCount; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var content = MessageContent.Create($"Message {i + 1}").Value;
            _messages.Add((content, role));
        }
        return this;
    }

    /// <summary>
    /// Creates a conversation with a large number of messages.
    /// </summary>
    public ConversationBuilder WithManyMessages(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var content = MessageContent.Create($"Message {i + 1}").Value;
            _messages.Add((content, role));
        }
        return this;
    }

    /// <summary>
    /// Builds the conversation aggregate with the configured settings.
    /// </summary>
    public Conversation Build()
    {
        // Ensure required fields have values
        _ownerId ??= UserId.New();
        
        // Start the conversation
        var titleValue = _useDefaultTitle ? null : _title?.Value;
        var result = Conversation.Start(_ownerId, titleValue, _clock);
        
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to create conversation: {result.Error.Message}");
        }
        
        var conversation = result.Value;
        
        // Add messages
        foreach (var (content, role) in _messages)
        {
            Result addResult = role == MessageRole.User
                ? conversation.AppendUserMessage(content, _clock)
                : conversation.AppendAssistantMessage(content, _clock);
            
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add message: {addResult.Error.Message}");
            }
        }
        
        // Complete if needed
        if (_status == ConversationStatus.Completed && _messages.Any())
        {
            var completeResult = conversation.Complete(_clock);
            if (completeResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to complete conversation: {completeResult.Error.Message}");
            }
        }
        
        // Clear domain events if needed for clean testing
        conversation.ClearDomainEvents();
        
        return conversation;
    }

    /// <summary>
    /// Builds the conversation and returns it with its domain events.
    /// </summary>
    public (Conversation Conversation, List<IDomainEvent> Events) BuildWithEvents()
    {
        var conversation = Build();
        var events = conversation.DomainEvents.ToList();
        return (conversation, events);
    }

    /// <summary>
    /// Creates a minimal valid conversation.
    /// </summary>
    public static Conversation Minimal()
    {
        return Create()
            .WithDefaultOwner()
            .WithDefaultTitle()
            .Build();
    }

    /// <summary>
    /// Creates a typical conversation with some messages.
    /// </summary>
    public static Conversation Typical()
    {
        return Create()
            .WithDefaultOwner()
            .WithTitle("Typical Conversation")
            .WithConversationFlow(
                "Hello, how can I help you?",
                "I need assistance with testing.",
                "I'd be happy to help with testing!",
                "Thank you!")
            .Build();
    }

    /// <summary>
    /// Creates a completed conversation.
    /// </summary>
    public static Conversation CompletedConversation()
    {
        return Create()
            .WithDefaultOwner()
            .WithTitle("Completed Chat")
            .WithUserMessage("Question")
            .WithAssistantMessage("Answer")
            .Completed()
            .Build();
    }
}