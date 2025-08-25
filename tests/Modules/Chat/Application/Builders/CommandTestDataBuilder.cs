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