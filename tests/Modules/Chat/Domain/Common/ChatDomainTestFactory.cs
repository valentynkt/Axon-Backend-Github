using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.Builders;
using Axon.Modules.Chat.Domain.Tests.TestDoubles;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Central factory for creating test objects in the Chat Domain.
/// Provides consistent creation patterns and reduces test duplication.
/// </summary>
public static class ChatDomainTestFactory
{
    /// <summary>
    /// Factory methods for creating test conversation objects.
    /// </summary>
    public static class Conversations
    {
        /// <summary>
        /// Creates a standard conversation for general testing.
        /// </summary>
        public static Conversation CreateStandard()
        {
            return ConversationBuilder.New()
                .WithTitle("Standard Test Conversation")
                .WithUserMessage("This is a test user message")
                .Build();
        }

        /// <summary>
        /// Creates a conversation with multiple messages for interaction testing.
        /// </summary>
        public static Conversation CreateWithMultipleMessages(int messageCount = 4)
        {
            return ConversationBuilder.New()
                .WithTitle("Multi-Message Conversation")
                .WithAlternatingMessages(messageCount)
                .Build();
        }

        /// <summary>
        /// Creates a conversation with specific owner for ownership testing.
        /// </summary>
        public static Conversation CreateWithOwner(UserId ownerId)
        {
            return ConversationBuilder.New()
                .WithOwner(ownerId)
                .WithTitle("Owner-Specific Conversation")
                .WithUserMessage("Message from specific owner")
                .Build();
        }

        /// <summary>
        /// Creates a completed conversation for status testing.
        /// </summary>
        public static Conversation CreateCompleted()
        {
            return ConversationBuilder.New()
                .WithTitle("Completed Test Conversation")
                .WithUserMessage("Final user message")
                .ThatShouldBeCompleted()
                .Build();
        }

        /// <summary>
        /// Creates a conversation with edge case title length.
        /// </summary>
        public static Conversation CreateWithLongTitle()
        {
            var longTitle = new string('A', TestConstants.Limits.MaxConversationMessages);
            return ConversationBuilder.New()
                .WithTitle(longTitle)
                .WithUserMessage("Message with very long conversation title")
                .Build();
        }

        /// <summary>
        /// Creates a conversation at the maximum message limit.
        /// </summary>
        public static Conversation CreateAtMessageLimit()
        {
            return ConversationBuilder.New()
                .WithTitle("Max Messages Conversation")
                .WithAlternatingMessages(TestConstants.Limits.MaxConversationMessages)
                .Build();
        }

        /// <summary>
        /// Creates a conversation with specific creation time.
        /// </summary>
        public static Conversation CreateWithSpecificTime(DateTimeOffset creationTime)
        {
            return ConversationBuilder.New()
                .WithTitle("Time-Specific Conversation")
                .AtTime(creationTime)
                .WithUserMessage("Message created at specific time")
                .Build();
        }

        /// <summary>
        /// Creates a conversation with the minimum valid data.
        /// </summary>
        public static Conversation CreateMinimal()
        {
            return ConversationBuilder.New()
                .WithTitle("Min")
                .WithUserMessage("Hi")
                .Build();
        }

        /// <summary>
        /// Creates multiple conversations for bulk testing scenarios.
        /// </summary>
        public static List<Conversation> CreateMultiple(int count = 3)
        {
            return Enumerable.Range(0, count)
                .Select(i => ConversationBuilder.New()
                    .WithTitle($"Conversation {i + 1}")
                    .WithUserMessage($"Message for conversation {i + 1}")
                    .Build())
                .ToList();
        }

        /// <summary>
        /// Creates conversations with different owners for multi-tenant scenarios.
        /// </summary>
        public static Dictionary<UserId, Conversation> CreateForDifferentOwners(int ownerCount = 3)
        {
            return Enumerable.Range(0, ownerCount)
                .ToDictionary(
                    i => UserId.New(),
                    i => ConversationBuilder.New()
                        .WithTitle($"Conversation for Owner {i + 1}")
                        .WithUserMessage($"Message from owner {i + 1}")
                        .Build());
        }

        /// <summary>
        /// Creates a conversation with Unicode content for internationalization testing.
        /// </summary>
        public static Conversation CreateWithUnicodeContent()
        {
            return ConversationBuilder.New()
                .WithTitle("🌍 International Test - 测试 - тест - テスト")
                .WithUserMessage("Hello! 你好! Привет! こんにちは! 🚀✨")
                .Build();
        }

        /// <summary>
        /// Creates a conversation for performance testing with generated data.
        /// </summary>
        public static Conversation CreateWithGeneratedData()
        {
            return ConversationBuilder.New()
                .WithTitle(TestDataGenerator.GenerateConversationTitle())
                .WithUserMessage(TestDataGenerator.GenerateUserMessage())
                .Build();
        }

        /// <summary>
        /// Creates conversations for testing time-based queries.
        /// </summary>
        public static List<Conversation> CreateWithTimeDistribution(int count = 5)
        {
            var baseTime = TestConstants.DateTimes.DefaultTestTime;
            return Enumerable.Range(0, count)
                .Select(i => ConversationBuilder.New()
                    .WithTitle($"Time-distributed Conversation {i + 1}")
                    .AtTime(baseTime.AddHours(i))
                    .WithUserMessage($"Message at {baseTime.AddHours(i):HH:mm}")
                    .Build())
                .ToList();
        }

        /// <summary>
        /// Creates conversations with varying message counts for testing message-based queries.
        /// </summary>
        public static List<Conversation> CreateWithVaryingMessageCounts()
        {
            return new List<Conversation>([
                ConversationBuilder.New().WithTitle("Single Message").WithUserMessage("Only message").Build(),
                ConversationBuilder.New().WithTitle("Few Messages").WithAlternatingMessages(3).Build(),
                ConversationBuilder.New().WithTitle("Many Messages").WithAlternatingMessages(8).Build(),
                ConversationBuilder.New().WithTitle("Max Messages").WithAlternatingMessages(TestConstants.Limits.MaxConversationMessages).Build()
            ]);
        }

        /// <summary>
        /// Creates a conversation specifically for domain event testing.
        /// </summary>
        public static Conversation CreateForEventTesting()
        {
            var timeProvider = new FakeTimeProvider();
            var conversation = ConversationBuilder.New()
                .WithTimeProvider(timeProvider)
                .WithAlternatingMessages(4)
                .Build();
            
            // Clear initial creation events for focused event testing
            conversation.ClearDomainEvents();
            return conversation;
        }
    }

    /// <summary>
    /// Factory methods for creating test message objects.
    /// </summary>
    public static class Messages
    {
        /// <summary>
        /// Creates a standard user message for testing.
        /// </summary>
        public static Message CreateUserMessage()
        {
            return MessageBuilder.NewUserMessage()
                .WithContent("Standard test user message")
                .Build();
        }

        /// <summary>
        /// Creates a standard assistant message for testing.
        /// </summary>
        public static Message CreateAssistantMessage()
        {
            return MessageBuilder.NewAssistantMessage()
                .WithContent("Standard test assistant response")
                .Build();
        }

        /// <summary>
        /// Creates messages with edge case content lengths.
        /// </summary>
        public static List<Message> CreateEdgeCaseLengths()
        {
            return new List<Message>
            {
                MessageBuilder.NewUserMessage().WithContent("Hi").Build(),
                MessageBuilder.NewUserMessage().WithContent(new string('A', TestConstants.Limits.MaxMessageContentLength)).Build(),
                MessageBuilder.NewAssistantMessage().WithContent("OK").Build(),
                MessageBuilder.NewAssistantMessage().WithContent(new string('B', TestConstants.Limits.MaxMessageContentLength)).Build()
            };
        }

        /// <summary>
        /// Creates a sequence of alternating messages for conversation testing.
        /// </summary>
        public static List<Message> CreateAlternatingSequence(int count = 4)
        {
            var messages = new List<Message>();
            for (int i = 0; i < count; i++)
            {
                if (i % 2 == 0)
                {
                    messages.Add(MessageBuilder.NewUserMessage()
                        .WithContent($"User message {(i / 2) + 1}")
                        .Build());
                }
                else
                {
                    messages.Add(MessageBuilder.NewAssistantMessage()
                        .WithContent($"Assistant response {((i - 1) / 2) + 1}")
                        .Build());
                }
            }
            return messages;
        }
    }

    /// <summary>
    /// Factory methods for creating test time providers and time-related objects.
    /// </summary>
    public static class TimeProviders
    {
        /// <summary>
        /// Creates a FakeTimeProvider with default test time.
        /// </summary>
        public static FakeTimeProvider CreateTimeProvider()
        {
            return new FakeTimeProvider();
        }

        /// <summary>
        /// Creates a FakeTimeProvider with custom time.
        /// </summary>
        public static FakeTimeProvider CreateTimeProvider(DateTimeOffset fixedTime)
        {
            return new FakeTimeProvider(fixedTime);
        }

        /// <summary>
        /// Creates a FakeTimeProvider with auto-advance for testing time progression.
        /// </summary>
        public static FakeTimeProvider CreateAdvancingTimeProvider(TimeSpan interval)
        {
            var provider = new FakeTimeProvider();
            provider.AutoAdvanceAmount = interval;
            return provider;
        }
    }
}

/// <summary>
/// Extension methods and utilities for test collections.
/// </summary>
public static class Collections
{
    /// <summary>
    /// Creates a large number of conversations for performance testing.
    /// </summary>
    public static List<Conversation> CreateLargeConversationSet(int count = 1000)
    {
        return Enumerable.Range(0, count)
            .Select(_ => ChatDomainTestFactory.Conversations.CreateWithGeneratedData())
            .ToList();
    }

    /// <summary>
    /// Creates conversations with different owners for multi-tenant testing.
    /// </summary>
    public static Dictionary<UserId, List<Conversation>> CreateMultiOwnerConversations(
        int ownerCount = 5, 
        int conversationsPerOwner = 3)
    {
        var result = new Dictionary<UserId, List<Conversation>>();

        for (int i = 0; i < ownerCount; i++)
        {
            var owner = UserId.New();
            var conversations = Enumerable.Range(0, conversationsPerOwner)
                .Select(_ => ConversationBuilder.New()
                    .WithOwner(owner)
                    .WithGeneratedData()
                    .Build())
                .ToList();

            result[owner] = conversations;
        }

        return result;
    }

    private static ConversationBuilder WithGeneratedData(this ConversationBuilder builder)
    {
        return builder
            .WithTitle(TestDataGenerator.GenerateConversationTitle())
            .WithAlternatingMessages(new Random().Next(2, 8));
    }
}