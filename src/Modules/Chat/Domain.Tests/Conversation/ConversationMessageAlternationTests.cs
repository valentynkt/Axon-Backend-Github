using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Functional.Results;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Conversation;

/// <summary>
/// Tests for message alternation rules, user-first requirements, idempotency, and threading anchors.
/// Covers the strict alternation and user-must-start domain policies.
/// </summary>
public class ConversationMessageAlternationTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void UserMustStart_FirstAssistantRejected()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var content = MessageContent.Create("Assistant response").Value;
        var responseId = AiResponseId.Create("response_123").Value;

        // Act
        var result = conversation.AppendAssistantMessage(content, responseId, _clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.MESSAGE.TURN.VIOLATION");
        result.Error.Message.ShouldBe("Messages must alternate between user and assistant.");
    }

    [Fact]
    public void UserMustStart_FirstUserAllowed()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var content = MessageContent.Create("Hello").Value;

        // Act
        var result = conversation.AppendUserMessage(content, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(1);
        conversation.MessagesOrdered.First().Role.IsUser.ShouldBeTrue();
    }

    [Fact]
    public void StrictAlternation_UserThenUserRejected()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent1 = MessageContent.Create("First message").Value;
        var userContent2 = MessageContent.Create("Second message").Value;

        conversation.AppendUserMessage(userContent1, _clock);

        // Act
        var result = conversation.AppendUserMessage(userContent2, _clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.MESSAGE.TURN.VIOLATION");
        result.Error.Message.ShouldBe("Messages must alternate between user and assistant.");
    }

    [Fact]
    public void StrictAlternation_AssistantThenAssistantRejected()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent = MessageContent.Create("User message").Value;
        var assistantContent1 = MessageContent.Create("First response").Value;
        var assistantContent2 = MessageContent.Create("Second response").Value;
        var responseId1 = AiResponseId.Create("response_1").Value;
        var responseId2 = AiResponseId.Create("response_2").Value;

        conversation.AppendUserMessage(userContent, _clock);
        conversation.AppendAssistantMessage(assistantContent1, responseId1, _clock);

        // Act
        var result = conversation.AppendAssistantMessage(assistantContent2, responseId2, _clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.MESSAGE.TURN.VIOLATION");
        result.Error.Message.ShouldBe("Messages must alternate between user and assistant.");
    }

    [Fact]
    public void StrictAlternation_UserThenAssistantAllowed()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent = MessageContent.Create("User message").Value;
        var assistantContent = MessageContent.Create("Assistant response").Value;
        var responseId = AiResponseId.Create("response_123").Value;

        conversation.AppendUserMessage(userContent, _clock);

        // Act
        var result = conversation.AppendAssistantMessage(assistantContent, responseId, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(2);
        conversation.MessagesOrdered.Last().Role.IsAssistant.ShouldBeTrue();
    }

    [Fact]
    public void StrictAlternation_AssistantThenUserAllowed()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent1 = MessageContent.Create("First user message").Value;
        var assistantContent = MessageContent.Create("Assistant response").Value;
        var userContent2 = MessageContent.Create("Second user message").Value;
        var responseId = AiResponseId.Create("response_123").Value;

        conversation.AppendUserMessage(userContent1, _clock);
        conversation.AppendAssistantMessage(assistantContent, responseId, _clock);

        // Act
        var result = conversation.AppendUserMessage(userContent2, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(3);
        conversation.MessagesOrdered.Last().Role.IsUser.ShouldBeTrue();
    }

    [Fact]
    public void AssistantIdempotency_SameAiResponseIdReturnsExisting_NoNewEvent()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent = MessageContent.Create("User message").Value;
        var assistantContent = MessageContent.Create("Assistant response").Value;
        var responseId = AiResponseId.Create("response_123").Value;

        conversation.AppendUserMessage(userContent, _clock);
        var firstResult = conversation.AppendAssistantMessage(assistantContent, responseId, _clock);
        var initialEventCount = conversation.DomainEvents.Count;
        var initialMessageCount = conversation.MessageCount;

        // Act - append same assistant message again
        var secondResult = conversation.AppendAssistantMessage(assistantContent, responseId, _clock);

        // Assert
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Value.Id.ShouldBe(firstResult.Value.Id); // Same message instance
        conversation.MessageCount.ShouldBe(initialMessageCount); // No new message added
        conversation.DomainEvents.Count.ShouldBe(initialEventCount); // No new events
    }

    [Fact]
    public void LastAiResponseId_IsUpdatedOnlyOnAssistantAppend()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent = MessageContent.Create("User message").Value;
        var assistantContent = MessageContent.Create("Assistant response").Value;
        var responseId = AiResponseId.Create("response_123").Value;

        // Act & Assert - User message doesn't set LastAiResponseId
        conversation.LastAiResponseId.ShouldBeNull();
        conversation.AppendUserMessage(userContent, _clock);
        conversation.LastAiResponseId.ShouldBeNull();

        // Act & Assert - Assistant message sets LastAiResponseId
        conversation.AppendAssistantMessage(assistantContent, responseId, _clock);
        conversation.LastAiResponseId.ShouldNotBeNull();
        conversation.LastAiResponseId!.Value.ShouldBe(responseId.Value);
    }

    [Fact]
    public void GetLastAiResponseId_ReturnsAnchorForNextTurn()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent = MessageContent.Create("User message").Value;
        var assistantContent = MessageContent.Create("Assistant response").Value;
        var responseId = AiResponseId.Create("response_123").Value;

        // Act & Assert - No AI response yet
        conversation.GetLastAiResponseId().ShouldBeNull();

        // Add user message - still no AI response
        conversation.AppendUserMessage(userContent, _clock);
        conversation.GetLastAiResponseId().ShouldBeNull();

        // Add assistant message - now we have an AI response ID
        conversation.AppendAssistantMessage(assistantContent, responseId, _clock);
        conversation.GetLastAiResponseId().ShouldBe(responseId.Value);
    }

    [Fact]
    public void LastAiResponseId_PersistsThroughMultipleTurns()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent1 = MessageContent.Create("First user message").Value;
        var assistantContent1 = MessageContent.Create("First assistant response").Value;
        var responseId1 = AiResponseId.Create("response_1").Value;

        var userContent2 = MessageContent.Create("Second user message").Value;
        var assistantContent2 = MessageContent.Create("Second assistant response").Value;
        var responseId2 = AiResponseId.Create("response_2").Value;

        // Act - First turn
        conversation.AppendUserMessage(userContent1, _clock);
        conversation.AppendAssistantMessage(assistantContent1, responseId1, _clock);
        conversation.GetLastAiResponseId().ShouldBe(responseId1.Value);

        // Act - Second turn (user message shouldn't change last AI response)
        conversation.AppendUserMessage(userContent2, _clock);
        conversation.GetLastAiResponseId().ShouldBe(responseId1.Value); // Still the old one

        // Act - Assistant responds with new ID
        conversation.AppendAssistantMessage(assistantContent2, responseId2, _clock);
        conversation.GetLastAiResponseId().ShouldBe(responseId2.Value); // Now updated

        // Assert final state
        conversation.MessageCount.ShouldBe(4);
        conversation.LastAiResponseId!.Value.ShouldBe(responseId2.Value);
    }

    [Fact]
    public void IdempotencyWithDifferentContent_CreatesNewMessage()
    {
        // Arrange
        var conversation = Domain.Aggregates.Conversation.Conversation
            .Start(_ownerId, "Test Conversation", _clock).Value;

        var userContent = MessageContent.Create("User message").Value;
        var assistantContent1 = MessageContent.Create("First response").Value;
        var assistantContent2 = MessageContent.Create("Different response").Value;
        var responseId = AiResponseId.Create("response_123").Value;

        conversation.AppendUserMessage(userContent, _clock);
        var firstResult = conversation.AppendAssistantMessage(assistantContent1, responseId, _clock);

        // Need a new user message to allow another assistant message
        var userContent2 = MessageContent.Create("Second user message").Value;
        conversation.AppendUserMessage(userContent2, _clock);

        // Act - try to append with same responseId but different content
        var secondResult = conversation.AppendAssistantMessage(assistantContent2, responseId, _clock);

        // Assert - Should return the existing message (idempotency based on responseId, not content)
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Value.Id.ShouldBe(firstResult.Value.Id);
        conversation.MessageCount.ShouldBe(3); // user + assistant + user (second assistant blocked by idempotency)
    }
}