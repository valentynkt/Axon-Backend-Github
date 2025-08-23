using Axon.Modules.Chat.Domain.Tests.Builders;
using Axon.Modules.Chat.Domain.Tests.TestDoubles;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Central factory for creating test objects in the Chat Domain.
/// Provides a single entry point for all test data creation with consistent defaults.
/// Combines builders, test doubles, and generators for comprehensive test scenarios.
/// </summary>
public static class ChatDomainTestFactory
{
    /// <summary>
    /// Creates conversation-related test objects and scenarios.
    /// </summary>
    public static class Conversations
    {
        /// <summary>
        /// Creates a simple, valid conversation with default settings.
        /// </summary>
        public static Conversation CreateValid()
        {
            return ConversationBuilder.New().Build();
        }

        /// <summary>
        /// Creates a conversation with realistic generated data.
        /// </summary>
        public static Conversation CreateWithGeneratedData()
        {
            return ConversationBuilder.New()
                .WithOwner(UserId.New())
                .WithTitle(TestDataGenerator.GenerateConversationTitle())
                .Build();
        }

        /// <summary>
        /// Creates a conversation with multiple messages for testing complex scenarios.
        /// </summary>
        public static Conversation CreateWithMessages(int messageCount = 4)
        {
            return ConversationBuilder.New()
                .WithAlternatingMessages(messageCount)
                .Build();
        }

        /// <summary>
        /// Creates a completed conversation for testing final states.
        /// </summary>
        public static Conversation CreateCompleted()
        {
            return ConversationBuilder.New()
                .WithAlternatingMessages(2)
                .ThatShouldBeCompleted()
                .Build();
        }

        /// <summary>
        /// Creates a conversation without a title (default title scenario).
        /// </summary>
        public static Conversation CreateWithoutTitle()
        {
            return ConversationBuilder.Minimal().Build();
        }

        /// <summary>
        /// Creates multiple conversations for batch testing scenarios.
        /// </summary>
        public static List<Conversation> CreateBatch(int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ => CreateWithGeneratedData())
                .ToList();
        }
    }

    /// <summary>
    /// Creates message-related test objects.
    /// </summary>
    public static class Messages
    {
        /// <summary>
        /// Creates a valid user message.
        /// </summary>
        public static Message CreateUserMessage()
        {
            return MessageBuilder.NewUserMessage().Build();
        }

        /// <summary>
        /// Creates a valid assistant message.
        /// </summary>
        public static Message CreateAssistantMessage()
        {
            return MessageBuilder.NewAssistantMessage().Build();
        }

        /// <summary>
        /// Creates a sequence of alternating user and assistant messages.
        /// </summary>
        public static List<Message> CreateAlternatingSequence(int count)
        {
            var conversationId = ConversationId.New();
            var messages = new List<Message>();

            for (int i = 0; i < count; i++)
            {
                var sequence = i + 1;
                if (i % 2 == 0)
                {
                    var message = MessageBuilder.NewUserMessage()
                        .InConversation(conversationId)
                        .WithSequence(sequence)
                        .WithContent($"User message {sequence}")
                        .Build();
                    messages.Add(message);
                }
                else
                {
                    var aiResponseId = AiResponseId.From($"ai-response-{sequence}");
                    var message = MessageBuilder.NewAssistantMessage()
                        .InConversation(conversationId)
                        .WithSequence(sequence)
                        .WithContent($"Assistant message {sequence}")
                        .WithAiResponseId(aiResponseId)
                        .Build();
                    messages.Add(message);
                }
            }

            return messages;
        }
    }

    /// <summary>
    /// Creates value objects for testing.
    /// </summary>
    public static class ValueObjects
    {
        /// <summary>
        /// Creates valid conversation titles.
        /// </summary>
        public static ConversationTitle CreateTitle(string? value = null)
        {
            return ConversationTitle.From(value ?? TestConstants.Conversations.DefaultTitle);
        }

        /// <summary>
        /// Creates valid message content.
        /// </summary>
        public static MessageContent CreateContent(string? value = null)
        {
            return MessageContent.From(value ?? TestConstants.Messages.DefaultUserMessage);
        }

        /// <summary>
        /// Creates user message role.
        /// </summary>
        public static MessageRole CreateUserRole()
        {
            return MessageRole.User;
        }

        /// <summary>
        /// Creates assistant message role.
        /// </summary>
        public static MessageRole CreateAssistantRole()
        {
            return MessageRole.Assistant;
        }
    }

    /// <summary>
    /// Creates strongly-typed IDs for testing.
    /// </summary>
    public static class Ids
    {
        public static ConversationId CreateConversationId() => ConversationId.New();
        public static MessageId CreateMessageId() => MessageId.New();
        public static UserId CreateUserId() => UserId.New();
        public static AiResponseId CreateAiResponseId() => AiResponseId.From(TestDataGenerator.GenerateAiResponseId());

        /// <summary>
        /// Creates related IDs for testing relationships.
        /// </summary>
        public static (ConversationId conversationId, MessageId messageId, UserId userId) CreateRelatedIds()
        {
            return (ConversationId.New(), MessageId.New(), UserId.New());
        }
    }

    /// <summary>
    /// Creates test scenarios for comprehensive testing.
    /// </summary>
    public static class Scenarios
    {
        /// <summary>
        /// Creates a complete conversation scenario with full lifecycle.
        /// </summary>
        public static (Conversation conversation, List<IDomainEvent> events) CreateCompleteConversationScenario()
        {
            var timeProvider = new FakeTimeProvider();
            var conversation = ConversationBuilder.New()
                .WithTimeProvider(timeProvider)
                .WithAlternatingMessages(4)
                .ThatShouldBeCompleted()
                .Build();

            var events = conversation.DomainEvents.ToList();
            return (conversation, events);
        }

        /// <summary>
        /// Creates an edge case scenario with boundary conditions.
        /// </summary>
        public static Conversation CreateEdgeCaseScenario()
        {
            return ConversationBuilder.New()
                .WithTitle(TestConstants.EdgeCases.ExactMaxTitle)
                .WithUserMessage(TestConstants.EdgeCases.ExactMaxMessage)
                .Build();
        }

        /// <summary>
        /// Creates a scenario for testing business rule violations.
        /// </summary>
        public static class Violations
        {
            public static Action CreateTitleTooLongScenario()
            {
                return () => ConversationBuilder.New()
                    .WithTooLongTitle()
                    .Build();
            }

            public static Action CreateMessageTooLongScenario()
            {
                return () => ConversationBuilder.New()
                    .WithUserMessage(TestConstants.EdgeCases.OneOverMaxMessage)
                    .Build();
            }

            public static Action CreateInvalidOwnerScenario()
            {
                return () => ConversationBuilder.Invalid().Build();
            }
        }
    }

    /// <summary>
    /// Creates test doubles and mocks.
    /// </summary>
    public static class TestDoubles
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
            return FakeTimeProvider.WithAutoAdvance(interval);
        }
    }

    /// <summary>
    /// Creates collections of test data for performance and load testing.
    /// </summary>
    public static class Collections
    {
        /// <summary>
        /// Creates a large number of conversations for performance testing.
        /// </summary>
        public static List<Conversation> CreateLargeConversationSet(int count = 1000)
        {
            return Enumerable.Range(0, count)
                .Select(_ => Conversations.CreateWithGeneratedData())
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
}