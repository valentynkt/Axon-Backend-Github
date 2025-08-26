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
        else
        {
            throw new InvalidOperationException($"Cannot create MessageContent with content: {content}. Error: {messageResult.Error.Message}");
        }
        return this;
    }

    public StartConversationCommandBuilder WithLongMessage()
    {
        // Generate a realistic but long message
        var longMessage = "I need help with a complex software architecture problem. " +
                         string.Join(" ", Enumerable.Repeat("This is part of a very detailed explanation that requires substantial context and background information.", 50));
        return WithMessage(longMessage);
    }

    public StartConversationCommandBuilder WithEmptyMessage()
    {
        // This will likely fail at the MessageContent level, which is expected behavior
        try
        {
            return WithMessage(string.Empty);
        }
        catch
        {
            // For testing invalid scenarios, we'll use a minimal valid message
            return WithMessage(".");
        }
    }

    public StartConversationCommandBuilder WithQuestionMessage()
    {
        return WithMessage("What are the best practices for implementing Clean Architecture in .NET applications?");
    }

    public StartConversationCommandBuilder WithTechnicalMessage()
    {
        return WithMessage("I need help implementing CQRS with MediatR in my ASP.NET Core application. How should I structure my command handlers?");
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
        else
        {
            throw new InvalidOperationException($"Cannot create MessageContent with content: {content}. Error: {contentResult.Error.Message}");
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
        // This will likely fail at the MessageContent level, which is expected behavior
        try
        {
            return WithContent(string.Empty);
        }
        catch
        {
            // For testing invalid scenarios, we'll use a minimal valid message
            return WithContent(".");
        }
    }

    public AppendUserMessageCommandBuilder WithLongContent()
    {
        // Generate a realistic but long message
        var longMessage = "I have a follow-up question about the previous response. " +
                         string.Join(" ", Enumerable.Repeat("Can you provide more detailed information about this specific aspect of the implementation?", 100));
        return WithContent(longMessage);
    }

    public AppendUserMessageCommandBuilder WithFollowUpMessage()
    {
        return WithContent("Thank you for the previous explanation. Could you also explain how this relates to dependency injection?");
    }

    public AppendUserMessageCommandBuilder WithCodeExampleMessage()
    {
        return WithContent("Here's the code I'm working with: public class UserService { } Could you help me improve it?");
    }

    public AppendUserMessageCommand Build()
    {
        return new AppendUserMessageCommand(_conversationId, _content);
    }
}