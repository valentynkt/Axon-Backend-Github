using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Mothers;

/// <summary>
/// Mother object providing pre-configured Message instances and MessageContent values for testing.
/// Centralizes common message scenarios for consistency across tests.
/// </summary>
public static class MessageMother
{
    /// <summary>
    /// Creates a valid user message content.
    /// </summary>
    public static MessageContent ValidUserMessage()
    {
        return MessageContent.Create("Hello, I need assistance.").Value;
    }

    /// <summary>
    /// Creates a valid assistant message content.
    /// </summary>
    public static MessageContent ValidAssistantMessage()
    {
        return MessageContent.Create("I'm here to help! What do you need?").Value;
    }

    /// <summary>
    /// Creates a simple greeting message content.
    /// </summary>
    public static MessageContent Greeting()
    {
        return MessageContent.Create("Hello!").Value;
    }

    /// <summary>
    /// Creates a farewell message content.
    /// </summary>
    public static MessageContent Farewell()
    {
        return MessageContent.Create("Goodbye, have a great day!").Value;
    }

    /// <summary>
    /// Creates a question message content.
    /// </summary>
    public static MessageContent Question()
    {
        return MessageContent.Create("Can you explain how this works?").Value;
    }

    /// <summary>
    /// Creates an answer message content.
    /// </summary>
    public static MessageContent Answer()
    {
        return MessageContent.Create("Here's how it works: First, you need to understand the basics...").Value;
    }

    /// <summary>
    /// Creates a message content at the maximum allowed length.
    /// </summary>
    public static MessageContent MaxLength()
    {
        var content = new string('a', 100000); // Maximum allowed
        return MessageContent.Create(content).Value;
    }

    /// <summary>
    /// Creates a message content just under the maximum length.
    /// </summary>
    public static MessageContent NearMaxLength()
    {
        var content = new string('a', 99999);
        return MessageContent.Create(content).Value;
    }

    /// <summary>
    /// Creates a very short message content.
    /// </summary>
    public static MessageContent Minimal()
    {
        return MessageContent.Create("a").Value;
    }

    /// <summary>
    /// Creates message content with code.
    /// </summary>
    public static MessageContent WithCode()
    {
        var content = @"Here's the code:
```csharp
public class Example
{
    public string Name { get; set; }
    public int Value { get; set; }
}
```";
        return MessageContent.Create(content).Value;
    }

    /// <summary>
    /// Creates message content with markdown formatting.
    /// </summary>
    public static MessageContent WithMarkdown()
    {
        var content = @"# Title
## Subtitle
**Bold text** and *italic text*
- List item 1
- List item 2
> Blockquote
[Link](https://example.com)";
        return MessageContent.Create(content).Value;
    }

    /// <summary>
    /// Creates message content with special characters.
    /// </summary>
    public static MessageContent WithSpecialCharacters()
    {
        return MessageContent.Create("Special: @#$%^&*()_+-=[]{}|;':\",./<>?").Value;
    }

    /// <summary>
    /// Creates message content with emojis.
    /// </summary>
    public static MessageContent WithEmojis()
    {
        return MessageContent.Create("Hello World! 🌍 🚀 ✨ 🎉 💻 🔥").Value;
    }

    /// <summary>
    /// Creates message content with Unicode characters.
    /// </summary>
    public static MessageContent WithUnicode()
    {
        return MessageContent.Create("Multi-language: Hello 世界 مرحبا мир שלום").Value;
    }

    /// <summary>
    /// Creates message content with line breaks.
    /// </summary>
    public static MessageContent WithLineBreaks()
    {
        return MessageContent.Create(@"Line 1
Line 2
Line 3

Line 5 (after empty line)").Value;
    }

    /// <summary>
    /// Creates message content with URLs.
    /// </summary>
    public static MessageContent WithUrls()
    {
        return MessageContent.Create("Check out https://example.com and http://test.org for more info.").Value;
    }

    /// <summary>
    /// Creates message content with JSON.
    /// </summary>
    public static MessageContent WithJson()
    {
        var content = @"{
  ""name"": ""Test"",
  ""value"": 42,
  ""nested"": {
    ""property"": ""value""
  }
}";
        return MessageContent.Create(content).Value;
    }

    /// <summary>
    /// Creates message content with SQL.
    /// </summary>
    public static MessageContent WithSql()
    {
        var content = @"SELECT u.Id, u.Name, COUNT(m.Id) as MessageCount
FROM Users u
LEFT JOIN Messages m ON u.Id = m.UserId
GROUP BY u.Id, u.Name
HAVING COUNT(m.Id) > 10;";
        return MessageContent.Create(content).Value;
    }

    /// <summary>
    /// Creates an error message content.
    /// </summary>
    public static MessageContent ErrorMessage()
    {
        return MessageContent.Create("Error: Operation failed. Please try again.").Value;
    }

    /// <summary>
    /// Creates a success message content.
    /// </summary>
    public static MessageContent SuccessMessage()
    {
        return MessageContent.Create("Success! The operation completed successfully.").Value;
    }

    /// <summary>
    /// Message content samples for different domains.
    /// </summary>
    public static class Domain
    {
        public static MessageContent Technical()
        {
            return MessageContent.Create("The API endpoint returns a 404 status code when the resource is not found.").Value;
        }

        public static MessageContent Business()
        {
            return MessageContent.Create("Our Q3 revenue increased by 15% compared to last year.").Value;
        }

        public static MessageContent Casual()
        {
            return MessageContent.Create("Hey! How's it going? Want to grab coffee later?").Value;
        }

        public static MessageContent Formal()
        {
            return MessageContent.Create("Dear Sir/Madam, I am writing to inquire about your services.").Value;
        }

        public static MessageContent Educational()
        {
            return MessageContent.Create("The mitochondria is the powerhouse of the cell.").Value;
        }
    }

    /// <summary>
    /// Creates complete Message entities (not just content).
    /// </summary>
    public static class Messages
    {
        public static Message SimpleUserMessage()
        {
            return MessageBuilder.Create()
                .AsUser()
                .WithContent(ValidUserMessage())
                .Build();
        }

        public static Message SimpleAssistantMessage()
        {
            return MessageBuilder.Create()
                .AsAssistant()
                .WithContent(ValidAssistantMessage())
                .Build();
        }

        public static Message SystemMessage()
        {
            return MessageBuilder.Create()
                .AsSystem()
                .WithContent("System: Conversation started")
                .Build();
        }

        public static Message LongMessage()
        {
            return MessageBuilder.Create()
                .AsUser()
                .WithContent(NearMaxLength())
                .Build();
        }

        public static Message CodeMessage()
        {
            return MessageBuilder.Create()
                .AsUser()
                .WithContent(WithCode())
                .Build();
        }

        public static List<Message> ConversationFlow()
        {
            var conversationId = ConversationId.New();
            return new List<Message>
            {
                MessageBuilder.Create()
                    .InConversation(conversationId)
                    .AsUser()
                    .WithContent(Question())
                    .WithSequence(1)
                    .Build(),
                
                MessageBuilder.Create()
                    .InConversation(conversationId)
                    .AsAssistant()
                    .WithContent(Answer())
                    .WithSequence(2)
                    .Build(),
                
                MessageBuilder.Create()
                    .InConversation(conversationId)
                    .AsUser()
                    .WithContent("Thank you!")
                    .WithSequence(3)
                    .Build(),
                
                MessageBuilder.Create()
                    .InConversation(conversationId)
                    .AsAssistant()
                    .WithContent("You're welcome!")
                    .WithSequence(4)
                    .Build()
            };
        }
    }

    /// <summary>
    /// Invalid message content attempts for negative testing.
    /// </summary>
    public static class Invalid
    {
        public static string Empty => "";
        public static string Whitespace => "   ";
        public static string? Null => null;
        public static string TooLong => new('a', 100001); // Over the limit
    }

    /// <summary>
    /// Generates a variety of message contents for property-based testing.
    /// </summary>
    public static IEnumerable<MessageContent> GenerateVariety(int count = 20)
    {
        var generators = new Func<MessageContent>[]
        {
            Greeting,
            Question,
            Answer,
            WithCode,
            WithMarkdown,
            WithEmojis,
            WithUnicode,
            WithUrls,
            ErrorMessage,
            SuccessMessage
        };

        for (int i = 0; i < count; i++)
        {
            yield return generators[i % generators.Length]();
        }
    }
}