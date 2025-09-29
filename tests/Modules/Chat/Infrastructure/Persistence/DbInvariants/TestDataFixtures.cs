using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbInvariants;

/// <summary>
/// Standard test data fixtures for Chat database invariant tests.
/// Provides consistent, predictable test data following the Identity module pattern.
/// </summary>
public static class TestDataFixtures
{
    private static readonly FakeTimeProvider DefaultTimeProvider =
        new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    #region Conversations

    /// <summary>
    /// Creates a basic conversation with no messages.
    /// </summary>
    public static Conversation CreateBasicConversation(
        AxonUserId? ownerId = null,
        string? title = null,
        TimeProvider? timeProvider = null)
    {
        var owner = ownerId ?? AxonUserId.New();
        var provider = timeProvider ?? DefaultTimeProvider;

        var result = Conversation.StartNewConversation(owner, title, provider);
        result.IsSuccess.ShouldBeTrue("Failed to create basic conversation");
        return result.Value;
    }

    /// <summary>
    /// Creates a conversation with one user message.
    /// </summary>
    public static Conversation CreateConversationWithUserMessage(
        AxonUserId? ownerId = null,
        string? messageContent = null,
        TimeProvider? timeProvider = null)
    {
        var conversation = CreateBasicConversation(ownerId, "Test Chat", timeProvider);
        var provider = timeProvider ?? DefaultTimeProvider;

        var content = MessageContent.From(messageContent ?? "Hello, AI assistant!");
        var result = conversation.AppendUserMessageToConversation(content, provider);
        result.IsSuccess.ShouldBeTrue("Failed to append user message");

        return conversation;
    }

    /// <summary>
    /// Creates a conversation with a user message and assistant response.
    /// </summary>
    public static Conversation CreateConversationWithMessagePair(
        AxonUserId? ownerId = null,
        AiResponseId? aiResponseId = null,
        TimeProvider? timeProvider = null)
    {
        var conversation = CreateConversationWithUserMessage(ownerId, "User question", timeProvider);
        var provider = timeProvider ?? DefaultTimeProvider;

        var assistantContent = MessageContent.From("Assistant response");
        var responseId = aiResponseId ?? new AiResponseId(Guid.NewGuid().ToString());
        var result = conversation.AppendAssistantResponseToConversation(assistantContent, responseId, provider);
        result.IsSuccess.ShouldBeTrue("Failed to append assistant message");

        return conversation;
    }

    /// <summary>
    /// Creates a conversation with multiple message exchanges.
    /// </summary>
    public static Conversation CreateConversationWithMultipleExchanges(
        AxonUserId? ownerId = null,
        int exchangeCount = 3,
        TimeProvider? timeProvider = null)
    {
        var conversation = CreateBasicConversation(ownerId, "Multi-exchange Chat", timeProvider);
        var provider = timeProvider ?? DefaultTimeProvider;

        for (int i = 0; i < exchangeCount; i++)
        {
            // Add user message
            var userContent = MessageContent.From($"User message {i + 1}");
            var userResult = conversation.AppendUserMessageToConversation(userContent, provider);
            userResult.IsSuccess.ShouldBeTrue($"Failed to append user message {i + 1}");

            // Add assistant response
            var assistantContent = MessageContent.From($"Assistant response {i + 1}");
            var responseId = new AiResponseId($"response-{Guid.NewGuid()}");
            var assistantResult = conversation.AppendAssistantResponseToConversation(
                assistantContent, responseId, provider);
            assistantResult.IsSuccess.ShouldBeTrue($"Failed to append assistant message {i + 1}");
        }

        return conversation;
    }

    /// <summary>
    /// Creates a completed conversation with messages.
    /// </summary>
    public static Conversation CreateCompletedConversation(
        AxonUserId? ownerId = null,
        TimeProvider? timeProvider = null)
    {
        var conversation = CreateConversationWithMultipleExchanges(ownerId, 2, timeProvider);
        var provider = timeProvider ?? DefaultTimeProvider;

        var completeResult = conversation.Complete(provider);
        completeResult.IsSuccess.ShouldBeTrue("Failed to complete conversation");

        return conversation;
    }

    #endregion

    #region Messages

    /// <summary>
    /// Creates a standalone user message (for direct DB manipulation tests).
    /// Note: Messages should normally only be created through the aggregate.
    /// </summary>
    public static Message CreateStandaloneUserMessage(
        ConversationId conversationId,
        int sequence = 1,
        string? content = null)
    {
        var messageContent = MessageContent.From(content ?? "Standalone user message");

        // Using reflection to create message for invariant testing
        // This bypasses domain rules to test database constraints
        var messageType = typeof(Message);
        var constructor = messageType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(MessageId), typeof(ConversationId), typeof(MessageRole),
                   typeof(MessageContent), typeof(int), typeof(AiResponseId) },
            null);

        var message = (Message)constructor!.Invoke(new object?[]
        {
            MessageId.New(),
            conversationId,
            MessageRole.User,
            messageContent,
            sequence,
            null // No AI response ID for user messages
        });

        return message;
    }

    /// <summary>
    /// Creates a standalone assistant message (for direct DB manipulation tests).
    /// </summary>
    public static Message CreateStandaloneAssistantMessage(
        ConversationId conversationId,
        AiResponseId aiResponseId,
        int sequence = 2,
        string? content = null)
    {
        var messageContent = MessageContent.From(content ?? "Standalone assistant message");

        var messageType = typeof(Message);
        var constructor = messageType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(MessageId), typeof(ConversationId), typeof(MessageRole),
                   typeof(MessageContent), typeof(int), typeof(AiResponseId) },
            null);

        var message = (Message)constructor!.Invoke(new object?[]
        {
            MessageId.New(),
            conversationId,
            MessageRole.Assistant,
            messageContent,
            sequence,
            aiResponseId
        });

        return message;
    }

    #endregion

    #region AI Response IDs

    /// <summary>
    /// Creates a predictable AI response ID for testing idempotency.
    /// </summary>
    public static AiResponseId CreateFixedAiResponseId(string suffix = "fixed")
    {
        return new AiResponseId($"ai-response-{suffix}-12345");
    }

    /// <summary>
    /// Creates multiple unique AI response IDs.
    /// </summary>
    public static List<AiResponseId> CreateUniqueAiResponseIds(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new AiResponseId($"ai-response-{Guid.NewGuid()}"))
            .ToList();
    }

    #endregion

    #region Scenarios

    /// <summary>
    /// Creates a scenario with two conversations having the same AI response ID
    /// (for testing global uniqueness constraint).
    /// </summary>
    public static (Conversation first, Conversation second, AiResponseId sharedId)
        CreateDuplicateAiResponseIdScenario(TimeProvider? timeProvider = null)
    {
        var provider = timeProvider ?? DefaultTimeProvider;
        var sharedResponseId = CreateFixedAiResponseId("shared");

        // First conversation with the AI response ID
        var conv1 = CreateConversationWithUserMessage(AxonUserId.New(), "Question 1", provider);
        var content1 = MessageContent.From("Response 1");
        conv1.AppendAssistantResponseToConversation(content1, sharedResponseId, provider);

        // Second conversation attempting to use the same AI response ID
        var conv2 = CreateConversationWithUserMessage(AxonUserId.New(), "Question 2", provider);

        return (conv1, conv2, sharedResponseId);
    }

    /// <summary>
    /// Creates a scenario for testing message sequence uniqueness within a conversation.
    /// </summary>
    public static (Conversation conversation, Message duplicateMessage)
        CreateDuplicateSequenceScenario(TimeProvider? timeProvider = null)
    {
        var conversation = CreateConversationWithUserMessage(timeProvider: timeProvider);

        // Create a message with duplicate sequence number (1, which already exists)
        var duplicateMessage = CreateStandaloneUserMessage(
            conversation.Id,
            sequence: 1, // Duplicate sequence
            content: "This should violate sequence uniqueness");

        return (conversation, duplicateMessage);
    }

    /// <summary>
    /// Creates a scenario for testing orphaned messages (messages without conversation).
    /// </summary>
    public static Message CreateOrphanMessageScenario()
    {
        var nonExistentConversationId = ConversationId.New();
        return CreateStandaloneUserMessage(nonExistentConversationId, 1, "Orphan message");
    }

    #endregion
}