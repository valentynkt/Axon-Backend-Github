using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

/// <summary>
/// Fluent builder for creating Message entities with various configurations.
/// Simplifies message creation in tests with sensible defaults.
/// </summary>
public class MessageBuilder
{
    private MessageId? _id;
    private ConversationId? _conversationId;
    private MessageContent? _content;
    private MessageRole _role = MessageRole.User;
    private int _sequence = 1;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a new MessageBuilder instance.
    /// </summary>
    public static MessageBuilder Create() => new();

    /// <summary>
    /// Sets a specific message ID.
    /// </summary>
    public MessageBuilder WithId(MessageId id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets a new random message ID.
    /// </summary>
    public MessageBuilder WithNewId()
    {
        _id = MessageId.New();
        return this;
    }

    /// <summary>
    /// Sets the conversation ID this message belongs to.
    /// </summary>
    public MessageBuilder InConversation(ConversationId conversationId)
    {
        _conversationId = conversationId;
        return this;
    }

    /// <summary>
    /// Sets the message content.
    /// </summary>
    public MessageBuilder WithContent(string content)
    {
        _content = MessageContent.Create(content).Value;
        return this;
    }

    /// <summary>
    /// Sets the message content from a MessageContent value object.
    /// </summary>
    public MessageBuilder WithContent(MessageContent content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Sets empty content for edge case testing.
    /// </summary>
    public MessageBuilder WithEmptyContent()
    {
        // This might fail validation, useful for negative testing
        var result = MessageContent.Create("");
        if (result.IsSuccess)
        {
            _content = result.Value;
        }
        return this;
    }

    /// <summary>
    /// Sets very long content for boundary testing.
    /// </summary>
    public MessageBuilder WithLongContent(int length = 100000)
    {
        var content = new string('a', length);
        _content = MessageContent.Create(content).Value;
        return this;
    }

    /// <summary>
    /// Sets content with special characters.
    /// </summary>
    public MessageBuilder WithSpecialCharacters()
    {
        _content = MessageContent.Create("Hello 🌍! Special chars: @#$%^&*()").Value;
        return this;
    }

    /// <summary>
    /// Sets content with code snippets.
    /// </summary>
    public MessageBuilder WithCodeContent()
    {
        var code = @"```csharp
public class Test
{
    public void Method() => Console.WriteLine(""Hello"");
}
```";
        _content = MessageContent.Create(code).Value;
        return this;
    }

    /// <summary>
    /// Sets the message role to User.
    /// </summary>
    public MessageBuilder AsUser()
    {
        _role = MessageRole.User;
        return this;
    }

    /// <summary>
    /// Sets the message role to Assistant.
    /// </summary>
    public MessageBuilder AsAssistant()
    {
        _role = MessageRole.Assistant;
        return this;
    }

    /// <summary>
    /// Sets the message role to System.
    /// </summary>
    public MessageBuilder AsSystem()
    {
        _role = MessageRole.System;
        return this;
    }

    /// <summary>
    /// Sets the message sequence number.
    /// </summary>
    public MessageBuilder WithSequence(int sequence)
    {
        _sequence = sequence;
        return this;
    }

    /// <summary>
    /// Sets the creation timestamp.
    /// </summary>
    public MessageBuilder CreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    /// <summary>
    /// Sets creation time to a specific number of minutes ago.
    /// </summary>
    public MessageBuilder CreatedMinutesAgo(int minutes)
    {
        _createdAt = DateTimeOffset.UtcNow.AddMinutes(-minutes);
        return this;
    }

    /// <summary>
    /// Builds the message entity with the configured settings.
    /// </summary>
    public Message Build()
    {
        // Ensure required fields have values
        _id ??= MessageId.New();
        _conversationId ??= ConversationId.New();
        _content ??= MessageContent.Create("Default message content").Value;
        
        return new Message(
            _id,
            _conversationId,
            _content,
            _role,
            _sequence,
            _createdAt);
    }

    /// <summary>
    /// Creates a minimal valid user message.
    /// </summary>
    public static Message UserMessage()
    {
        return Create()
            .AsUser()
            .WithContent("User message")
            .Build();
    }

    /// <summary>
    /// Creates a minimal valid assistant message.
    /// </summary>
    public static Message AssistantMessage()
    {
        return Create()
            .AsAssistant()
            .WithContent("Assistant response")
            .Build();
    }

    /// <summary>
    /// Creates a system message.
    /// </summary>
    public static Message SystemMessage()
    {
        return Create()
            .AsSystem()
            .WithContent("System notification")
            .Build();
    }

    /// <summary>
    /// Creates a message with specific content and role.
    /// </summary>
    public static Message WithContentAndRole(string content, MessageRole role)
    {
        return Create()
            .WithContent(content)
            .WithRole(role)
            .Build();
    }

    /// <summary>
    /// Sets the message role directly.
    /// </summary>
    private MessageBuilder WithRole(MessageRole role)
    {
        _role = role;
        return this;
    }

    /// <summary>
    /// Creates a sequence of messages for a conversation.
    /// </summary>
    public static List<Message> ConversationSequence(ConversationId conversationId, params (string Content, MessageRole Role)[] messages)
    {
        var result = new List<Message>();
        var sequence = 1;
        
        foreach (var (content, role) in messages)
        {
            result.Add(Create()
                .InConversation(conversationId)
                .WithContent(content)
                .WithRole(role)
                .WithSequence(sequence++)
                .Build());
        }
        
        return result;
    }

    /// <summary>
    /// Creates a message at the maximum allowed content length.
    /// </summary>
    public static Message MaxLengthMessage()
    {
        return Create()
            .WithLongContent(100000) // Max length per domain rules
            .Build();
    }

    /// <summary>
    /// Creates a message with Unicode and emoji content.
    /// </summary>
    public static Message UnicodeMessage()
    {
        return Create()
            .WithContent("Hello 世界 🌍 مرحبا мир 🚀")
            .Build();
    }

    /// <summary>
    /// Creates a message with markdown formatting.
    /// </summary>
    public static Message MarkdownMessage()
    {
        var markdown = @"# Header
**Bold** and *italic* text
- List item 1
- List item 2

> Quote block

[Link](https://example.com)";
        
        return Create()
            .WithContent(markdown)
            .Build();
    }
}