using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Mothers;

/// <summary>
/// Mother object providing pre-configured Conversation instances for testing.
/// Centralizes common test data scenarios for consistency across tests.
/// </summary>
public static class ConversationMother
{
    private static readonly DateTimeOffset StandardTestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
    
    /// <summary>
    /// Creates a simple active conversation with no messages.
    /// </summary>
    public static Conversation Empty()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithDefaultTitle()
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation with a single user message.
    /// </summary>
    public static Conversation WithSingleUserMessage()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Single Message Chat")
            .WithUserMessage("Hello, I need help")
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation with a typical Q&A exchange.
    /// </summary>
    public static Conversation SimpleQA()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Q&A Session")
            .WithConversationFlow(
                "What is the weather today?",
                "I'm an AI assistant and don't have access to real-time weather data.")
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a longer conversation with multiple exchanges.
    /// </summary>
    public static Conversation LongConversation()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Extended Discussion")
            .WithConversationFlow(
                "Can you help me with a coding problem?",
                "Of course! I'd be happy to help. What's the problem you're facing?",
                "I'm getting a null reference exception in my C# code",
                "Null reference exceptions occur when you try to use an object that hasn't been initialized. Can you share the code?",
                "Here's the problematic line: var result = myObject.Property.Method();",
                "The issue is likely that either myObject or myObject.Property is null. You should add null checks.",
                "How do I add null checks?",
                "You can use the null-conditional operator: var result = myObject?.Property?.Method();",
                "That worked! Thank you!",
                "You're welcome! Feel free to ask if you have more questions.")
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a completed conversation.
    /// </summary>
    public static Conversation Completed()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Resolved Issue")
            .WithConversationFlow(
                "I need help with an error",
                "I can help you with that. What error are you seeing?",
                "Fixed it, thanks!",
                "Great! Glad you resolved it.")
            .Completed()
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation at exactly the message limit.
    /// </summary>
    public static Conversation AtMessageLimit()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Maximum Messages")
            .NearMessageLimit(0)
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation one message away from the limit.
    /// </summary>
    public static Conversation NearMessageLimit()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Almost Full")
            .NearMessageLimit(1)
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation with code discussion.
    /// </summary>
    public static Conversation CodeDiscussion()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Code Review")
            .WithUserMessage(@"Can you review this code?
```csharp
public class Service
{
    public void Process(string data)
    {
        Console.WriteLine(data);
    }
}
```")
            .WithAssistantMessage(@"Here's my review:
1. Add null check for the data parameter
2. Consider returning a result instead of void
3. Add error handling
4. The class should implement an interface for testability")
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation with special characters and emojis.
    /// </summary>
    public static Conversation WithSpecialContent()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Special Characters 🚀")
            .WithConversationFlow(
                "Hello 世界! Can you handle Unicode? 🌍",
                "Yes! I can handle Unicode perfectly: مرحبا мир 你好 🎉",
                "What about special symbols: @#$%^&*()?",
                "All special characters are supported: ©®™€¥£¢")
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation with only user messages (monologue).
    /// </summary>
    public static Conversation UserMonologue()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("User Notes")
            .WithUserMessage("First thought")
            .WithUserMessage("Second thought")
            .WithUserMessage("Third thought")
            .WithUserMessage("Final thought")
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates a conversation that started with an assistant message.
    /// </summary>
    public static Conversation AssistantInitiated()
    {
        return ConversationBuilder.Create()
            .WithDefaultOwner()
            .WithTitle("Proactive Assistant")
            .WithAssistantMessage("Hello! How can I help you today?")
            .WithUserMessage("I need information about testing")
            .WithAssistantMessage("I'd be happy to help with testing information!")
            .AtTime(StandardTestTime)
            .Build();
    }

    /// <summary>
    /// Creates conversations with various error scenarios.
    /// </summary>
    public static class ErrorScenarios
    {
        /// <summary>
        /// Conversation with messages that approach content limits.
        /// </summary>
        public static Conversation NearContentLimit()
        {
            var longContent = new string('a', 99999); // Just under 100k limit
            return ConversationBuilder.Create()
                .WithDefaultOwner()
                .WithTitle("Large Content")
                .WithUserMessage(longContent)
                .AtTime(StandardTestTime)
                .Build();
        }

        /// <summary>
        /// Empty conversation that cannot be completed.
        /// </summary>
        public static Conversation CannotComplete()
        {
            return ConversationBuilder.Create()
                .WithDefaultOwner()
                .WithTitle("Incomplete")
                .AtTime(StandardTestTime)
                .Build();
        }
    }

    /// <summary>
    /// Creates conversations at different points in time.
    /// </summary>
    public static class Temporal
    {
        public static Conversation Today()
        {
            return ConversationBuilder.Create()
                .WithDefaultOwner()
                .WithTitle("Today's Chat")
                .WithUserMessage("Message from today")
                .AtTime(DateTimeOffset.UtcNow)
                .Build();
        }

        public static Conversation Yesterday()
        {
            return ConversationBuilder.Create()
                .WithDefaultOwner()
                .WithTitle("Yesterday's Chat")
                .WithUserMessage("Message from yesterday")
                .AtTime(DateTimeOffset.UtcNow.AddDays(-1))
                .Build();
        }

        public static Conversation LastWeek()
        {
            return ConversationBuilder.Create()
                .WithDefaultOwner()
                .WithTitle("Last Week's Chat")
                .WithUserMessage("Message from last week")
                .AtTime(DateTimeOffset.UtcNow.AddDays(-7))
                .Build();
        }

        public static Conversation LastMonth()
        {
            return ConversationBuilder.Create()
                .WithDefaultOwner()
                .WithTitle("Last Month's Chat")
                .WithUserMessage("Message from last month")
                .AtTime(DateTimeOffset.UtcNow.AddMonths(-1))
                .Build();
        }
    }

    /// <summary>
    /// Creates a collection of diverse conversations for testing.
    /// </summary>
    public static List<Conversation> DiverseSet(int count = 10)
    {
        var conversations = new List<Conversation>();
        var templates = new[]
        {
            Empty(),
            WithSingleUserMessage(),
            SimpleQA(),
            LongConversation(),
            Completed(),
            CodeDiscussion(),
            WithSpecialContent(),
            UserMonologue(),
            AssistantInitiated()
        };

        for (int i = 0; i < count; i++)
        {
            conversations.Add(templates[i % templates.Length]);
        }

        return conversations;
    }
}