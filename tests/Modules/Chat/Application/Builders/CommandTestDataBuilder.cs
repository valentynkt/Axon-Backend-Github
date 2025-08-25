using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Tests.Builders;

/// <summary>
/// Builder for creating test commands with configurable properties.
/// Follows the Builder pattern to provide fluent API for test data creation.
/// Extensible design allows for easy addition of new command types.
/// </summary>
public static class CommandTestDataBuilder
{
    /// <summary>
    /// Creates a builder for StartConversationCommand
    /// </summary>
    public static StartConversationCommandBuilder StartConversation() => new();
    
    /// <summary>
    /// Creates a builder for AppendUserMessageCommand
    /// </summary>
    public static AppendUserMessageCommandBuilder AppendUserMessage() => new();
}

/// <summary>
/// Builder for creating StartConversationCommand test instances
/// </summary>
public class StartConversationCommandBuilder
{
    private MessageContent _message = MessageContent.Create("Test message").Value;

    public StartConversationCommandBuilder WithMessage(string content)
    {
        var messageResult = MessageContent.Create(content);
        if (messageResult.IsSuccess)
        {
            _message = messageResult.Value;
        }
        return this;
    }

    public StartConversationCommandBuilder WithLongMessage()
    {
        return WithMessage(new string('x', 10000)); // Very long message
    }

    public StartConversationCommandBuilder WithEmptyMessage()
    {
        return WithMessage(string.Empty);
    }

    public StartConversationCommand Build()
    {
        return new StartConversationCommand(_message);
    }
}

/// <summary>
/// Builder for creating AppendUserMessageCommand test instances
/// </summary>
public class AppendUserMessageCommandBuilder
{
    private ConversationId _conversationId = ConversationId.New();
    private MessageContent _content = MessageContent.Create("Test message").Value;

    public AppendUserMessageCommandBuilder WithConversationId(ConversationId conversationId)
    {
        _conversationId = conversationId;
        return this;
    }

    public AppendUserMessageCommandBuilder WithContent(string content)
    {
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsSuccess)
        {
            _content = contentResult.Value;
        }
        return this;
    }

    public AppendUserMessageCommandBuilder WithValidData()
    {
        _conversationId = ConversationId.New();
        return WithContent("This is a valid test message for appending to conversation");
    }

    public AppendUserMessageCommandBuilder WithInvalidContent()
    {
        return WithContent(string.Empty);
    }

    public AppendUserMessageCommandBuilder WithLongContent()
    {
        return WithContent(new string('x', 10000)); // Very long message
    }

    public AppendUserMessageCommand Build()
    {
        return new AppendUserMessageCommand(_conversationId, _content);
    }
}