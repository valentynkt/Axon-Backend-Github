using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Internal.Text;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Shouldly;

namespace Axon.Modules.Chat.Integration;

/// <summary>
/// Advanced business logic tests for Chat module.
/// Tests complex edge cases, business rule interactions,
/// message sequence integrity, and advanced conversation scenarios.
/// </summary>
[TestFixture]
public class ChatBusinessLogicTests : ApplicationTestBase
{
    #region Complex Message Sequence Tests

    [Test]
    public async Task MessageSequence_AlternatingUserAssistant_ShouldMaintainIntegrity()
    {
        // Arrange: Create conversation with complex alternating pattern
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        var messageSequence = new List<(MessageRole Role, string Content)>
        {
            (MessageRole.User, "First user message"),
            (MessageRole.Assistant, "First assistant response"),
            (MessageRole.User, "Second user message"),
            (MessageRole.Assistant, "Second assistant response"),
            (MessageRole.User, "Third user message"),
            (MessageRole.Assistant, "Third assistant response")
        };

        // Act: Add messages following proper sequence
        var addedMessages = new List<Message>();
        foreach (var (role, content) in messageSequence)
        {
            var messageContent = MessageContent.Create(content).Value;

            if (role == MessageRole.User)
            {
                var result = conversation.AppendUserMessageToConversation(messageContent, TimeProvider);
                result.ShouldBeSuccess();
                addedMessages.Add(result.Value);
            }
            else
            {
                var aiResponseId = CreateAiResponseId();
                var result = conversation.AppendAssistantResponseToConversation(messageContent, aiResponseId, TimeProvider);
                result.ShouldBeSuccess();
                addedMessages.Add(result.Value);
            }
        }

        // Assert: Verify sequence integrity
        conversation.MessageCount.ShouldBe(messageSequence.Count);

        var orderedMessages = conversation.MessagesOrdered;
        for (int i = 0; i < orderedMessages.Count; i++)
        {
            orderedMessages[i].Role.ShouldBe(messageSequence[i].Role);
            orderedMessages[i].Content.Value.ShouldBe(messageSequence[i].Content);
            orderedMessages[i].Sequence.ShouldBe(i + 1);
        }

        // Verify last AI response ID is tracked correctly
        var lastAssistantMessage = orderedMessages.LastOrDefault(m => m.Role == MessageRole.Assistant);
        if (lastAssistantMessage != null)
        {
            conversation.LastAiResponseId.ShouldBe(lastAssistantMessage.AiResponseId);
        }
    }

    [Test]
    public async Task MessageSequence_TurnTakingViolation_ShouldEnforceRules()
    {
        // Arrange: Start with user message
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithUserMessage("Initial user message")
            .Build();

        // Act & Assert: Try to add another user message (should fail)
        var invalidContent = MessageContent.Create("Second user message without assistant response").Value;
        var result = conversation.AppendUserMessageToConversation(invalidContent, TimeProvider);

        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        conversation.MessageCount.ShouldBe(1); // Should remain unchanged
    }

    [Test]
    public async Task MessageSequence_AssistantWithoutUser_ShouldEnforceRules()
    {
        // Arrange: Empty conversation
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        // Act & Assert: Try to add assistant message first (should fail)
        var assistantContent = MessageContent.Create("Assistant message without user message").Value;
        var aiResponseId = CreateAiResponseId();
        var result = conversation.AppendAssistantResponseToConversation(assistantContent, aiResponseId, TimeProvider);

        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        conversation.MessageCount.ShouldBe(0);
    }

    #endregion

    #region Complex Conversation State Transitions

    [Test]
    public async Task ConversationLifecycle_CompleteWorkflow_ShouldTransitionCorrectly()
    {
        // Arrange: Create conversation
        var ownerId = CreateAxonUserId();
        var conversationResult = Conversation.StartNewConversation(ownerId, "Lifecycle test", TimeProvider);
        conversationResult.ShouldBeSuccess();
        var conversation = conversationResult.Value;

        // Act & Assert: Complete lifecycle

        // 1. Initial state
        conversation.Status.ShouldBe(ConversationStatus.Active);
        conversation.IsActive.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(0);

        // 2. Add user message
        var userMessage = MessageContent.Create("User message for lifecycle test").Value;
        var userResult = conversation.AppendUserMessageToConversation(userMessage, TimeProvider);
        userResult.ShouldBeSuccess();
        conversation.MessageCount.ShouldBe(1);
        conversation.IsActive.ShouldBeTrue();

        // 3. Add assistant response
        var assistantMessage = MessageContent.Create("Assistant response for lifecycle test").Value;
        var aiResponseId = CreateAiResponseId();
        var assistantResult = conversation.AppendAssistantResponseToConversation(assistantMessage, aiResponseId, TimeProvider);
        assistantResult.ShouldBeSuccess();
        conversation.MessageCount.ShouldBe(2);
        conversation.LastAiResponseId.ShouldBe(aiResponseId);

        // 4. Update title
        var titleResult = conversation.UpdateTitle("Updated lifecycle title", TimeProvider);
        titleResult.ShouldBeSuccess();
        conversation.Title.ShouldBe("Updated lifecycle title");

        // 5. Complete conversation
        var completeResult = conversation.Complete(TimeProvider);
        completeResult.ShouldBeSuccess();
        conversation.Status.ShouldBe(ConversationStatus.Completed);
        conversation.IsActive.ShouldBeFalse();

        // 6. Verify operations fail on completed conversation
        var invalidUserMessage = MessageContent.Create("Should fail").Value;
        var invalidUserResult = conversation.AppendUserMessageToConversation(invalidUserMessage, TimeProvider);
        invalidUserResult.ShouldBeFailure();

        var invalidTitleResult = conversation.UpdateTitle("Should fail", TimeProvider);
        invalidTitleResult.ShouldBeFailure();

        var invalidCompleteResult = conversation.Complete(TimeProvider);
        invalidCompleteResult.ShouldBeFailure();
    }

    [Test]
    public async Task ConversationCompletion_EdgeCases_ShouldValidateCorrectly()
    {
        // Test 1: Complete conversation with only user message (no assistant response)
        var conversation1 = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithUserMessage("Lonely user message")
            .Build();

        var result1 = conversation1.Complete(TimeProvider);
        result1.ShouldBeSuccess("Should allow completion with only user message");

        // Test 2: Try to complete empty conversation
        var conversation2 = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        var result2 = conversation2.Complete(TimeProvider);
        result2.ShouldBeFailure("Should not allow completion of empty conversation");
        result2.Error.Type.ShouldBe(ErrorType.BusinessRule);

        // Test 3: Complete conversation with many messages
        var conversation3 = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithAlternatingMessages(10) // 5 user + 5 assistant messages
            .Build();

        var result3 = conversation3.Complete(TimeProvider);
        result3.ShouldBeSuccess("Should allow completion with many messages");
    }

    #endregion

    #region Advanced Message Content Scenarios

    [Test]
    public async Task MessageContent_SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange: Test various special characters and edge cases
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        var specialContents = new[]
        {
            "Message with emoji: 🚀💬🤖",
            "Message with unicode: Hällo Wörld ñiño",
            "Message with quotes: \"Hello\" and 'World'",
            "Message with newlines:\nLine 1\nLine 2\nLine 3",
            "Message with tabs:\tTabbed\tContent",
            "Message with special chars: @#$%^&*()_+-=[]{}|;:,.<>?",
            "Message with numbers: 123456789 and decimals: 3.14159",
            "Message with URLs: https://example.com/path?param=value",
            "Message with code: function() { return 'hello'; }",
            "Mixed content: 🌟 \"Code: var x = 42;\" https://test.com 日本語"
        };

        // Act: Add all special content messages
        foreach (var content in specialContents)
        {
            var messageContent = MessageContent.Create(content).Value;
            var result = conversation.AppendUserMessageToConversation(messageContent, TimeProvider);

            // Assert: Each message should be added successfully
            result.ShouldBeSuccess($"Should handle special content: {content}");
            result.Value.Content.Value.ShouldBe(content, "Content should be preserved exactly");
        }

        // Final verification
        conversation.MessageCount.ShouldBe(specialContents.Length);

        var messages = conversation.MessagesOrdered;
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Content.Value.ShouldBe(specialContents[i]);
        }
    }

    [Test]
    public async Task MessageContent_BoundaryConditions_ShouldValidateCorrectly()
    {
        // Arrange
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        // Test minimum valid length (1 character)
        var minContent = MessageContent.Create("A").Value;
        var minResult = conversation.AppendUserMessageToConversation(minContent, TimeProvider);
        minResult.ShouldBeSuccess("Should accept minimum valid content");

        // Test content at maximum allowed length
        var maxAllowedLength = 4000; // Assuming this is the limit
        var maxContent = new string('A', maxAllowedLength);
        var maxContentResult = MessageContent.Create(maxContent);

        if (maxContentResult.IsSuccess)
        {
            var maxResult = conversation.AppendUserMessageToConversation(maxContentResult.Value, TimeProvider);
            maxResult.ShouldBeSuccess("Should accept maximum valid content");
        }

        // Test various whitespace scenarios
        var whitespaceTests = new[]
        {
            "  Content with leading spaces",
            "Content with trailing spaces  ",
            "  Content with both  ",
            "Content\nwith\nmultiple\nlines",
            "Content\t\twith\t\ttabs"
        };

        foreach (var content in whitespaceTests)
        {
            var contentResult = MessageContent.Create(content);
            if (contentResult.IsSuccess)
            {
                var result = conversation.AppendUserMessageToConversation(contentResult.Value, TimeProvider);
                result.ShouldBeSuccess($"Should handle whitespace content: '{content}'");
            }
        }
    }

    #endregion

    #region AI Response ID Management

    [Test]
    public async Task AiResponseId_Uniqueness_ShouldEnforceConstraints()
    {
        // Arrange: Conversation with alternating messages
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithUserMessage("First user message")
            .Build();

        var firstAiResponseId = CreateAiResponseId();
        var firstAssistantContent = MessageContent.Create("First assistant response").Value;

        // Act: Add first assistant message
        var result1 = conversation.AppendAssistantResponseToConversation(firstAssistantContent, firstAiResponseId, TimeProvider);
        result1.ShouldBeSuccess();

        // Add another user message
        var secondUserContent = MessageContent.Create("Second user message").Value;
        var result2 = conversation.AppendUserMessageToConversation(secondUserContent, TimeProvider);
        result2.ShouldBeSuccess();

        // Try to add assistant message with same AI response ID (should fail)
        var duplicateAssistantContent = MessageContent.Create("Duplicate AI response").Value;
        var result3 = conversation.AppendAssistantResponseToConversation(duplicateAssistantContent, firstAiResponseId, TimeProvider);

        // Assert: Should fail due to duplicate AI response ID
        result3.ShouldBeFailure();
        result3.Error.Type.ShouldBe(ErrorType.BusinessRule);
        conversation.MessageCount.ShouldBe(3); // Should remain unchanged

        // Verify original AI response ID is still tracked
        conversation.LastAiResponseId.ShouldBe(firstAiResponseId);
    }

    [Test]
    public async Task AiResponseId_IdempotencyScenario_ShouldReturnExistingMessage()
    {
        // Arrange: Conversation with assistant message
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithUserMessage("User message")
            .Build();

        var aiResponseId = CreateAiResponseId();
        var assistantContent = MessageContent.Create("Assistant response").Value;

        // Act: Add assistant message
        var result1 = conversation.AppendAssistantResponseToConversation(assistantContent, aiResponseId, TimeProvider);
        result1.ShouldBeSuccess();
        var originalMessage = result1.Value;

        // Try to add assistant message with same AI response ID again
        var sameContent = MessageContent.Create("Assistant response").Value;
        var result2 = conversation.AppendAssistantResponseToConversation(sameContent, aiResponseId, TimeProvider);

        // Assert: Should return the existing message (idempotency)
        result2.ShouldBeSuccess();
        result2.Value.Id.ShouldBe(originalMessage.Id);
        result2.Value.AiResponseId.ShouldBe(aiResponseId);
        conversation.MessageCount.ShouldBe(2); // Should not increase
    }

    #endregion

    #region Complex Domain Event Scenarios

    [Test]
    public async Task DomainEvents_ComplexWorkflow_ShouldRaiseCorrectEvents()
    {
        // Arrange
        var ownerId = CreateAxonUserId();
        var initialTitle = "Initial title";

        // Act: Complete workflow with event tracking
        var conversationResult = Conversation.StartNewConversation(ownerId, initialTitle, TimeProvider);
        conversationResult.ShouldBeSuccess();
        var conversation = conversationResult.Value;

        // Verify ConversationStartedEvent
        AssertDomainEventRaised<ConversationStartedEvent>(conversation);
        conversation.ClearDomainEvents();

        // Add user message
        var userContent = MessageContent.Create("User message").Value;
        var userResult = conversation.AppendUserMessageToConversation(userContent, TimeProvider);
        userResult.ShouldBeSuccess();

        // Verify UserMessageAppendedEvent
        AssertDomainEventRaised<UserMessageAppendedEvent>(conversation);
        conversation.ClearDomainEvents();

        // Add assistant message
        var assistantContent = MessageContent.Create("Assistant response").Value;
        var aiResponseId = CreateAiResponseId();
        var assistantResult = conversation.AppendAssistantResponseToConversation(assistantContent, aiResponseId, TimeProvider);
        assistantResult.ShouldBeSuccess();

        // Verify AssistantMessageAppendedEvent
        AssertDomainEventRaised<AssistantMessageAppendedEvent>(conversation);
        conversation.ClearDomainEvents();

        // Update title
        var newTitle = "Updated title";
        var titleResult = conversation.UpdateTitle(newTitle, TimeProvider);
        titleResult.ShouldBeSuccess();

        // Verify ConversationTitleUpdatedEvent
        AssertDomainEventRaised<ConversationTitleUpdatedEvent>(conversation);
        conversation.ClearDomainEvents();

        // Complete conversation
        var completeResult = conversation.Complete(TimeProvider);
        completeResult.ShouldBeSuccess();

        // Verify ConversationCompletedEvent
        AssertDomainEventRaised<ConversationCompletedEvent>(conversation);
    }

    [Test]
    public async Task DomainEvents_EventDataIntegrity_ShouldContainCorrectInformation()
    {
        // Arrange
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithUserMessage("Initial message")
            .Build();

        conversation.ClearDomainEvents();

        // Act: Add assistant message with specific data
        var assistantContent = MessageContent.Create("Detailed assistant response for testing").Value;
        var aiResponseId = CreateAiResponseId();
        var beforeTime = TimeProvider.GetUtcNow();

        var result = conversation.AppendAssistantResponseToConversation(assistantContent, aiResponseId, TimeProvider);
        result.ShouldBeSuccess();
        var message = result.Value;

        // Assert: Verify event data
        var events = conversation.GetDomainEvents();
        events.Count.ShouldBe(1);

        var assistantEvent = events.First() as AssistantMessageAppendedEvent;
        assistantEvent.ShouldNotBeNull();
        assistantEvent.ConversationId.ShouldBe(conversation.Id);
        assistantEvent.MessageId.ShouldBe(message.Id);
        assistantEvent.Sequence.ShouldBe(message.Sequence);
        assistantEvent.AiResponseId.ShouldBe(aiResponseId);
        assistantEvent.Timestamp.ShouldBeGreaterThanOrEqualTo(beforeTime);

        // Verify content preview
        var expectedPreview = TextSlices.Preview(assistantContent.Value, 100); // Assuming 100 char limit
        assistantEvent.ContentPreview.ShouldBe(expectedPreview);
    }

    #endregion

    #region Business Rule Edge Cases

    [Test]
    public async Task BusinessRules_ComplexInteractions_ShouldEnforceConsistently()
    {
        // Test complex business rule interactions

        // Scenario 1: Message at exact limit boundary
        var conversation1 = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithAlternatingMessages(98) // Assuming 100 is the limit
            .Build();

        // Should allow one more message pair
        var userContent = MessageContent.Create("At boundary user message").Value;
        var userResult = conversation1.AppendUserMessageToConversation(userContent, TimeProvider);
        userResult.ShouldBeSuccess("Should allow message within limit");

        var assistantContent = MessageContent.Create("At boundary assistant message").Value;
        var assistantResult = conversation1.AppendAssistantResponseToConversation(assistantContent, CreateAiResponseId(), TimeProvider);
        assistantResult.ShouldBeSuccess("Should allow message within limit");

        // Now should be at limit - next message should fail
        var overLimitContent = MessageContent.Create("Over limit message").Value;
        var overLimitResult = conversation1.AppendUserMessageToConversation(overLimitContent, TimeProvider);
        overLimitResult.ShouldBeFailure("Should not allow message over limit");

        // Scenario 2: Complex ownership validation
        var user1 = CreateAxonUserId();
        var user2 = CreateAxonUserId();

        var conversation2 = CreateConversationBuilder()
            .WithOwner(user1)
            .WithUserMessage("User 1 message")
            .Build();

        // Verify proper ownership
        conversation2.BelongsTo(user1).ShouldBeTrue();
        conversation2.BelongsTo(user2).ShouldBeFalse();

        var accessResult1 = conversation2.ValidateAccess(user1);
        accessResult1.ShouldBeSuccess();

        var accessResult2 = conversation2.ValidateAccess(user2);
        accessResult2.ShouldBeFailure();
        accessResult2.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Test]
    public async Task BusinessRules_StateTransitionEdgeCases_ShouldValidateCorrectly()
    {
        // Test state transition edge cases

        // Case 1: Title update on conversation with no title
        var conversation1 = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithTitle(null) // No title
            .WithUserMessage("Message")
            .Build();

        conversation1.HasDefaultTitle.ShouldBeTrue();

        var titleResult1 = conversation1.UpdateTitle("New title", TimeProvider);
        titleResult1.ShouldBeSuccess("Should allow setting title on conversation without title");
        conversation1.HasDefaultTitle.ShouldBeFalse();

        // Case 2: Title update with same title (should not raise event)
        conversation1.ClearDomainEvents();
        var titleResult2 = conversation1.UpdateTitle("New title", TimeProvider);
        titleResult2.ShouldBeSuccess("Should succeed with same title");
        AssertNoDomainEventsRaised(conversation1);

        // Case 3: Validation with empty/null user ID
        var emptyUserId = new AxonUserId(Guid.Empty);
        var accessResult = conversation1.ValidateAccess(emptyUserId);
        accessResult.ShouldBeFailure("Should not allow access with empty user ID");
    }

    #endregion

    #region Data Consistency and Integrity

    [Test]
    public async Task DataConsistency_ComplexOperations_ShouldMaintainIntegrity()
    {
        // Create conversation with complex data
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        var operations = new List<(string Operation, Func<Result<object, Error>>)>
        {
            ("Add user message 1", () =>
            {
                var content = MessageContent.Create("User message 1").Value;
                var result = conversation.AppendUserMessageToConversation(content, TimeProvider);
                return result.IsSuccess ? Result.Success<object, Error>(result.Value) : Result.Failure<object, Error>(result.Error);
            }),
            ("Add assistant message 1", () =>
            {
                var content = MessageContent.Create("Assistant message 1").Value;
                var result = conversation.AppendAssistantResponseToConversation(content, CreateAiResponseId(), TimeProvider);
                return result.IsSuccess ? Result.Success<object, Error>(result.Value) : Result.Failure<object, Error>(result.Error);
            }),
            ("Update title", () =>
            {
                var result = conversation.UpdateTitle("Complex operation title", TimeProvider);
                return result.IsSuccess ? Result.Success<object, Error>(Unit.Value) : Result.Failure<object, Error>(result.Error);
            }),
            ("Add user message 2", () =>
            {
                var content = MessageContent.Create("User message 2").Value;
                var result = conversation.AppendUserMessageToConversation(content, TimeProvider);
                return result.IsSuccess ? Result.Success<object, Error>(result.Value) : Result.Failure<object, Error>(result.Error);
            }),
            ("Add assistant message 2", () =>
            {
                var content = MessageContent.Create("Assistant message 2").Value;
                var result = conversation.AppendAssistantResponseToConversation(content, CreateAiResponseId(), TimeProvider);
                return result.IsSuccess ? Result.Success<object, Error>(result.Value) : Result.Failure<object, Error>(result.Error);
            })
        };

        // Execute all operations
        foreach (var (operation, func) in operations)
        {
            var result = func();
            result.ShouldBeSuccess($"Operation '{operation}' should succeed");
        }

        // Verify final state integrity
        conversation.MessageCount.ShouldBe(4);
        conversation.Title.ShouldBe("Complex operation title");
        conversation.IsActive.ShouldBeTrue();
        conversation.LastAiResponseId.ShouldNotBeNull();

        // Verify message sequence integrity
        var messages = conversation.MessagesOrdered;
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1);
            messages[i].ConversationId.ShouldBe(conversation.Id);
        }

        // Verify alternating pattern
        messages[0].Role.ShouldBe(MessageRole.User);
        messages[1].Role.ShouldBe(MessageRole.Assistant);
        messages[2].Role.ShouldBe(MessageRole.User);
        messages[3].Role.ShouldBe(MessageRole.Assistant);
    }

    #endregion
}