using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Tests.Extensions;

/// <summary>
/// Custom Shouldly extensions for Chat Domain testing.
/// Provides fluent, domain-specific assertions that improve test readability and error messages.
/// </summary>
public static class ChatDomainShouldlyExtensions
{
    /// <summary>
    /// Asserts that a conversation is in the expected status.
    /// </summary>
    public static void ShouldHaveStatus(this Conversation conversation, ConversationStatus expectedStatus)
    {
        conversation.Status.ShouldBe(expectedStatus, 
            $"Conversation should have status {expectedStatus} but was {conversation.Status}");
    }

    /// <summary>
    /// Asserts that a conversation is active.
    /// </summary>
    public static void ShouldBeActive(this Conversation conversation)
    {
        conversation.ShouldHaveStatus(ConversationStatus.Active);
        conversation.IsActive.ShouldBeTrue("Conversation should be active");
    }

    /// <summary>
    /// Asserts that a conversation is completed.
    /// </summary>
    public static void ShouldBeCompleted(this Conversation conversation)
    {
        conversation.ShouldHaveStatus(ConversationStatus.Completed);
        conversation.IsActive.ShouldBeFalse("Completed conversation should not be active");
    }

    /// <summary>
    /// Asserts that a conversation belongs to the expected owner.
    /// </summary>
    public static void ShouldBelongTo(this Conversation conversation, UserId expectedOwner)
    {
        conversation.OwnerId.ShouldBe(expectedOwner, 
            $"Conversation should belong to {expectedOwner} but belongs to {conversation.OwnerId}");
        conversation.BelongsTo(expectedOwner).ShouldBeTrue(
            $"Conversation.BelongsTo({expectedOwner}) should return true");
    }

    /// <summary>
    /// Asserts that a conversation has the expected number of messages.
    /// </summary>
    public static void ShouldHaveMessageCount(this Conversation conversation, int expectedCount)
    {
        conversation.MessageCount.ShouldBe(expectedCount, 
            $"Conversation should have {expectedCount} messages but has {conversation.MessageCount}");
        conversation.Messages.Count.ShouldBe(expectedCount, 
            "Message collection count should match MessageCount property");
    }

    /// <summary>
    /// Asserts that a conversation has no messages.
    /// </summary>
    public static void ShouldHaveNoMessages(this Conversation conversation)
    {
        conversation.ShouldHaveMessageCount(0);
        conversation.Messages.ShouldBeEmpty("Conversation should have no messages");
    }

    /// <summary>
    /// Asserts that a conversation has the expected title.
    /// </summary>
    public static void ShouldHaveTitle(this Conversation conversation, string? expectedTitle)
    {
        conversation.Title.ShouldBe(expectedTitle, 
            $"Conversation should have title '{expectedTitle}' but has '{conversation.Title}'");
    }

    /// <summary>
    /// Asserts that a conversation has a default (null) title.
    /// </summary>
    public static void ShouldHaveDefaultTitle(this Conversation conversation)
    {
        conversation.ShouldHaveTitle(null);
        conversation.HasDefaultTitle.ShouldBeTrue("Conversation should have default title");
    }

    /// <summary>
    /// Asserts that a message has the expected role.
    /// </summary>
    public static void ShouldHaveRole(this Message message, MessageRole expectedRole)
    {
        message.Role.ShouldBe(expectedRole, 
            $"Message should have role {expectedRole} but has {message.Role}");
    }

    /// <summary>
    /// Asserts that a message is a user message.
    /// </summary>
    public static void ShouldBeUserMessage(this Message message)
    {
        message.ShouldHaveRole(MessageRole.User);
        message.Role.IsUser.ShouldBeTrue("Message should be a user message");
        message.AiResponseId.ShouldBeNull("User message should not have AI response ID");
    }

    /// <summary>
    /// Asserts that a message is an assistant message with the expected AI response ID.
    /// </summary>
    public static void ShouldBeAssistantMessage(this Message message, AiResponseId? expectedAiResponseId = null)
    {
        message.ShouldHaveRole(MessageRole.Assistant);
        message.Role.IsAssistant.ShouldBeTrue("Message should be an assistant message");
        message.AiResponseId.ShouldNotBeNull("Assistant message should have AI response ID");

        if (expectedAiResponseId != null)
        {
            message.AiResponseId.ShouldBe(expectedAiResponseId, 
                $"Assistant message should have AI response ID {expectedAiResponseId}");
        }
    }

    /// <summary>
    /// Asserts that a message has the expected sequence number.
    /// </summary>
    public static void ShouldHaveSequence(this Message message, int expectedSequence)
    {
        message.Sequence.ShouldBe(expectedSequence, 
            $"Message should have sequence {expectedSequence} but has {message.Sequence}");
    }

    /// <summary>
    /// Asserts that a message belongs to the expected conversation.
    /// </summary>
    public static void ShouldBelongToConversation(this Message message, ConversationId expectedConversationId)
    {
        message.ConversationId.ShouldBe(expectedConversationId, 
            $"Message should belong to conversation {expectedConversationId} but belongs to {message.ConversationId}");
    }

    /// <summary>
    /// Asserts that a Result represents a successful operation.
    /// </summary>
    public static T ShouldBeSuccess<T, TError>(this Result<T, TError> result, string? message = null)
        where TError : class
    {
        result.IsSuccess.ShouldBeTrue(message ?? $"Expected success but got error: {result.Error}");
        return result.Value;
    }

    /// <summary>
    /// Asserts that a Result represents a failed operation.
    /// </summary>
    public static TError ShouldBeFailure<T, TError>(this Result<T, TError> result, string? message = null)
        where TError : class
    {
        result.IsFailure.ShouldBeTrue(message ?? $"Expected failure but got success: {result.Value}");
        return result.Error;
    }

    /// <summary>
    /// Asserts that a Result failed with an error containing the expected message.
    /// </summary>
    public static void ShouldFailWithMessage<T>(this Result<T, Error> result, string expectedMessage)
    {
        var error = result.ShouldBeFailure();
        error.Message.ShouldContain(expectedMessage, 
            $"Error message should contain '{expectedMessage}' but was '{error.Message}'");
    }

    /// <summary>
    /// Asserts that a Result failed with an error of the expected type.
    /// </summary>
    public static void ShouldFailWithErrorType<T>(this Result<T, Error> result, string expectedErrorType)
    {
        var error = result.ShouldBeFailure();
        error.Type.ShouldBe(expectedErrorType, 
            $"Error should be of type '{expectedErrorType}' but was '{error.Type}'");
    }

    /// <summary>
    /// Asserts that messages in a conversation are in the correct sequence order.
    /// </summary>
    public static void ShouldHaveMessagesInSequentialOrder(this Conversation conversation)
    {
        var messages = conversation.MessagesOrdered.ToList();
        
        for (int i = 0; i < messages.Count; i++)
        {
            var expectedSequence = i + 1;
            messages[i].ShouldHaveSequence(expectedSequence);
        }
    }

    /// <summary>
    /// Asserts that messages follow the turn-taking pattern (alternating user/assistant).
    /// </summary>
    public static void ShouldFollowTurnTakingPattern(this Conversation conversation)
    {
        var messages = conversation.MessagesOrdered.ToList();
        
        for (int i = 0; i < messages.Count; i++)
        {
            var expectedRole = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            messages[i].ShouldHaveRole(expectedRole);
        }
    }
}