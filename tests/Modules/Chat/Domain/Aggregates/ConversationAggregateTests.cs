using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.Builders;
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Extensions;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Tests.Aggregates;

[TestFixture]
public class ConversationAggregateTests : DomainTestBase
{
    private ConversationBuilder _builder = null!;

    protected override void OnSetUp()
    {
        _builder = new ConversationBuilder().WithTimeProvider(TimeProvider);
    }

    #region StartNewConversation Tests

    [Test]
    public void StartNewConversation_WithValidInputs_CreatesConversation()
    {
        // Arrange
        var ownerId = CreateUserId();
        var title = "Test Conversation";

        // Act
        var result = Conversation.StartNewConversation(ownerId, title, TimeProvider);

        // Assert
        var conversation = result.ShouldBeSuccess();
        conversation.ShouldBelongTo(ownerId);
        conversation.ShouldHaveTitle(title);
        conversation.ShouldBeActive();
        conversation.ShouldHaveMessageCount(0);
        
        AssertDomainEventRaised<ConversationStartedEvent>(conversation);
    }

    [TestCase(null, TestName = "StartNewConversation_WithNullTitle_UsesDefaultTitle")]
    [TestCase("", TestName = "StartNewConversation_WithEmptyTitle_UsesDefaultTitle")]
    [TestCase("   ", TestName = "StartNewConversation_WithWhitespaceTitle_UsesDefaultTitle")]
    public void StartNewConversation_WithInvalidTitle_UsesDefaultTitle(string? invalidTitle)
    {
        // Arrange
        var ownerId = CreateUserId();

        // Act
        var result = Conversation.StartNewConversation(ownerId, invalidTitle, TimeProvider);

        // Assert
        var conversation = result.ShouldBeSuccess();
        conversation.ShouldHaveDefaultTitle();
    }

    [TestCase(true, TestName = "StartNewConversation_WithValidMaxTitle_Succeeds")]
    [TestCase(false, TestName = "StartNewConversation_WithTooLongTitle_Fails")]
    public void StartNewConversation_TitleValidation(bool isValid)
    {
        // Arrange
        var ownerId = CreateUserId();
        var title = isValid ? TestConstants.EdgeCases.ExactMaxTitle : TestConstants.EdgeCases.OneOverMaxTitle;

        // Act
        var result = Conversation.StartNewConversation(ownerId, title, TimeProvider);

        // Assert
        if (isValid)
            result.ShouldBeSuccess();
        else
            result.ShouldBeFailure();
    }

    #endregion

    #region AppendUserMessage Tests

    [Test]
    public void AppendUserMessage_ToActiveConversation_AddsMessage()
    {
        // Arrange
        var conversation = _builder.WithAlternatingMessages(2).Build();
        var newMessage = "New user message";
        var originalCount = conversation.MessageCount;
        conversation.ClearDomainEvents();

        // Act
        var messageContent = MessageContent.Create(newMessage).Value;
        var result = conversation.AppendUserMessageToConversation(messageContent, TimeProvider);

        // Assert
        result.ShouldBeSuccess();
        conversation.ShouldHaveMessageCount(originalCount + 1);
        conversation.MessagesOrdered.Last().ShouldBeUserMessage();
        conversation.MessagesOrdered.Last().Content.Value.ShouldBe(newMessage);
        AssertDomainEventRaised<UserMessageAppendedEvent>(conversation);
    }

    [TestCase("completed", TestName = "AppendUserMessage_ToCompletedConversation_Fails")]
    [TestCase("after-user", TestName = "AppendUserMessage_AfterUserMessage_Fails")]
    [TestCase("at-limit", TestName = "AppendUserMessage_AtMessageLimit_Fails")]
    public void AppendUserMessage_InvalidState_Fails(string scenario)
    {
        // Arrange
        var conversation = scenario switch
        {
            "completed" => _builder.WithAlternatingMessages(2).ThatShouldBeCompleted().Build(),
            "after-user" => _builder.WithUserMessage().Build(),
            "at-limit" => _builder.WithMaximumMessages().Build(),
            _ => throw new ArgumentException($"Unknown scenario: {scenario}")
        };

        // Act
        var messageContent = MessageContent.Create("New message").Value;
        var result = conversation.AppendUserMessageToConversation(messageContent, TimeProvider);

        // Assert
        result.ShouldBeFailure();
    }

    [TestCase("", false, TestName = "AppendUserMessage_EmptyContent_Fails")]
    [TestCase("   ", false, TestName = "AppendUserMessage_WhitespaceContent_Fails")]
    [TestCase("valid-max", true, TestName = "AppendUserMessage_MaxLengthContent_Succeeds")]
    [TestCase("invalid-long", false, TestName = "AppendUserMessage_TooLongContent_Fails")]
    public void AppendUserMessage_ContentValidation(string contentType, bool shouldSucceed)
    {
        // Arrange
        var conversation = _builder.WithAlternatingMessages(2).Build();
        var content = GetTestContent(contentType);

        // Act
        Result<Message, Error> result;
        if (shouldSucceed)
        {
            var messageContent = MessageContent.Create(content).Value;
            result = conversation.AppendUserMessageToConversation(messageContent, TimeProvider);
        }
        else
        {
            // For invalid content, MessageContent.Create will fail
            var contentResult = MessageContent.Create(content);
            if (contentResult.IsFailure)
            {
                // Test that MessageContent validation catches invalid content
                contentResult.ShouldBeFailure();
                return;
            }
            result = conversation.AppendUserMessageToConversation(contentResult.Value, TimeProvider);
        }

        // Assert
        if (shouldSucceed)
        {
            result.ShouldBeSuccess();
            conversation.MessagesOrdered.Last().Content.Value.ShouldBe(content);
        }
        else
        {
            result.ShouldBeFailure();
        }
    }

    #endregion

    #region AppendAssistantResponse Tests

    [Test]
    public void AppendAssistantResponse_AfterUserMessage_AddsMessage()
    {
        // Arrange
        var conversation = _builder.WithUserMessage().Build();
        var assistantResponse = TestConstants.Messages.DefaultAssistantMessage;
        var aiResponseId = CreateAiResponseId();
        var originalCount = conversation.MessageCount;

        // Act
        var messageContent = MessageContent.Create(assistantResponse).Value;
        var result = conversation.AppendAssistantResponseToConversation(messageContent, aiResponseId, TimeProvider);

        // Assert
        result.ShouldBeSuccess();
        conversation.ShouldHaveMessageCount(originalCount + 1);
        conversation.MessagesOrdered.Last().ShouldBeAssistantMessage(aiResponseId);
        conversation.MessagesOrdered.Last().Content.Value.ShouldBe(assistantResponse);
        conversation.LastAiResponseId.ShouldBe(aiResponseId);
        AssertDomainEventRaised<AssistantMessageAppendedEvent>(conversation);
    }

    [TestCase("completed", TestName = "AppendAssistantResponse_ToCompletedConversation_Fails")]
    [TestCase("after-assistant", TestName = "AppendAssistantResponse_AfterAssistantMessage_Fails")]
    [TestCase("duplicate-id", TestName = "AppendAssistantResponse_WithDuplicateAiResponseId_Fails")]
    public void AppendAssistantResponse_InvalidState_Fails(string scenario)
    {
        // Arrange
        var aiResponseId = CreateAiResponseId();
        var conversation = scenario switch
        {
            "completed" => _builder.WithAlternatingMessages(2).ThatShouldBeCompleted().Build(),
            "after-assistant" => _builder.WithAlternatingMessages(2).Build(),
            "duplicate-id" => _builder.WithUserMessage().WithAssistantMessage(aiResponseId: aiResponseId).WithUserMessage().Build(),
            _ => throw new ArgumentException($"Unknown scenario: {scenario}")
        };

        // Act
        var messageContent = MessageContent.Create("New response").Value;
        var result = conversation.AppendAssistantResponseToConversation(
            messageContent, 
            scenario == "duplicate-id" ? aiResponseId : CreateAiResponseId(), 
            TimeProvider);

        // Assert
        result.ShouldBeFailure();
    }

    [TestCase("", false, TestName = "AppendAssistantResponse_EmptyContent_Fails")]
    [TestCase("   ", false, TestName = "AppendAssistantResponse_WhitespaceContent_Fails")]
    [TestCase("valid-max", true, TestName = "AppendAssistantResponse_MaxLengthContent_Succeeds")]
    [TestCase("invalid-long", false, TestName = "AppendAssistantResponse_TooLongContent_Fails")]
    public void AppendAssistantResponse_ContentValidation(string contentType, bool shouldSucceed)
    {
        // Arrange
        var conversation = _builder.WithUserMessage().Build();
        var content = GetTestContent(contentType);

        // Act
        Result<Message, Error> result;
        if (shouldSucceed)
        {
            var messageContent = MessageContent.Create(content).Value;
            result = conversation.AppendAssistantResponseToConversation(messageContent, CreateAiResponseId(), TimeProvider);
        }
        else
        {
            // For invalid content, MessageContent.Create will fail
            var contentResult = MessageContent.Create(content);
            if (contentResult.IsFailure)
            {
                contentResult.ShouldBeFailure();
                return;
            }
            result = conversation.AppendAssistantResponseToConversation(contentResult.Value, CreateAiResponseId(), TimeProvider);
        }

        // Assert
        if (shouldSucceed)
        {
            result.ShouldBeSuccess();
            conversation.MessagesOrdered.Last().Content.Value.ShouldBe(content);
        }
        else
        {
            result.ShouldBeFailure();
        }
    }

    #endregion

    #region UpdateTitle Tests

    [Test]
    public void UpdateTitle_WithValidTitle_UpdatesTitle()
    {
        // Arrange
        var conversation = _builder.WithTitle("Original Title").WithUserMessage().Build();
        var newTitle = "Updated Title";
        conversation.ClearDomainEvents();

        // Act
        var result = conversation.UpdateTitle(newTitle, TimeProvider);

        // Assert
        result.ShouldBeSuccess();
        conversation.ShouldHaveTitle(newTitle);
        conversation.HasDefaultTitle.ShouldBeFalse();
        AssertDomainEventRaised<ConversationTitleUpdatedEvent>(conversation);
    }

    [Test]
    public void UpdateTitle_WithSameTitle_SucceedsWithoutEvent()
    {
        // Arrange
        var title = "Same Title";
        var conversation = _builder.WithTitle(title).WithUserMessage().Build();
        conversation.ClearDomainEvents();

        // Act
        var result = conversation.UpdateTitle(title, TimeProvider);

        // Assert
        result.ShouldBeSuccess();
        conversation.ShouldHaveTitle(title);
        AssertNoDomainEventsRaised(conversation);
    }

    [Test]
    public void UpdateTitle_ToCompletedConversation_Fails()
    {
        // Arrange
        var conversation = _builder.WithUserMessage().ThatShouldBeCompleted().Build();

        // Act
        var result = conversation.UpdateTitle("New Title", TimeProvider);

        // Assert
        result.ShouldBeFailure();
    }

    [TestCase("", false, TestName = "UpdateTitle_EmptyTitle_Fails")]
    [TestCase("   ", false, TestName = "UpdateTitle_WhitespaceTitle_Fails")]
    [TestCase("valid-max", true, TestName = "UpdateTitle_MaxLengthTitle_Succeeds")]
    [TestCase("invalid-long", false, TestName = "UpdateTitle_TooLongTitle_Fails")]
    public void UpdateTitle_ValidationScenarios(string titleType, bool shouldSucceed)
    {
        // Arrange
        var conversation = _builder.WithUserMessage().Build();
        var title = GetTestTitle(titleType);

        // Act
        var result = conversation.UpdateTitle(title, TimeProvider);

        // Assert
        if (shouldSucceed)
        {
            result.ShouldBeSuccess();
            conversation.ShouldHaveTitle(title);
        }
        else
        {
            result.ShouldBeFailure();
        }
    }

    #endregion

    #region Complete Tests

    [TestCase(true, TestName = "Complete_ConversationWithAssistantMessage_Succeeds")]
    [TestCase(false, TestName = "Complete_ConversationWithOnlyUserMessage_Succeeds")]
    public void Complete_ActiveConversation_Succeeds(bool includeAssistantMessage)
    {
        // Arrange
        var builder = _builder.WithUserMessage();
        if (includeAssistantMessage)
            builder.WithAssistantMessage();
        var conversation = builder.Build();

        // Act
        var result = conversation.Complete(TimeProvider);

        // Assert
        result.ShouldBeSuccess();
        conversation.ShouldBeCompleted();
        AssertDomainEventRaised<ConversationCompletedEvent>(conversation);
    }

    [Test]
    public void Complete_AlreadyCompletedConversation_Fails()
    {
        // Arrange
        var conversation = _builder.WithUserMessage().ThatShouldBeCompleted().Build();

        // Act
        var result = conversation.Complete(TimeProvider);

        // Assert
        result.ShouldBeFailure();
    }

    #endregion

    #region Operations Update Timestamps

    [TestCase("AppendUserMessage", TestName = "AppendUserMessage_UpdatesTimestamp")]
    [TestCase("AppendAssistantResponse", TestName = "AppendAssistantResponse_UpdatesTimestamp")]
    [TestCase("UpdateTitle", TestName = "UpdateTitle_UpdatesTimestamp")]
    [TestCase("Complete", TestName = "Complete_UpdatesTimestamp")]
    public void Operations_UpdateTimestamp(string operation)
    {
        // Arrange
        var conversation = operation switch
        {
            "Complete" => _builder.WithUserMessage().Build(),
            "AppendAssistantResponse" => _builder.WithUserMessage().Build(),  // Assistant response needs to follow user message
            "AppendUserMessage" => _builder.WithAlternatingMessages(2).Build(),  // User message needs to follow assistant message
            _ => _builder.WithAlternatingMessages(2).Build()  // Default for UpdateTitle
        };
        
        var originalUpdateTime = conversation.UpdatedAt;
        AdvanceTime(TimeSpan.FromMinutes(5));

        // Act
        switch (operation)
        {
            case "AppendUserMessage":
                var userMessageContent = MessageContent.Create("New message").Value;
                var userResult = conversation.AppendUserMessageToConversation(userMessageContent, TimeProvider);
                userResult.ShouldBeSuccess();
                break;
            case "AppendAssistantResponse":
                var assistantMessageContent = MessageContent.Create("Response").Value;
                var assistantResult = conversation.AppendAssistantResponseToConversation(assistantMessageContent, CreateAiResponseId(), TimeProvider);
                assistantResult.ShouldBeSuccess();
                break;
            case "UpdateTitle":
                var titleResult = conversation.UpdateTitle("New Title", TimeProvider);
                titleResult.ShouldBeSuccess();
                break;
            case "Complete":
                var completeResult = conversation.Complete(TimeProvider);
                completeResult.ShouldBeSuccess();
                break;
            default:
                throw new ArgumentException($"Unknown operation: {operation}");
        }

        // Assert
        conversation.UpdatedAt.ShouldNotBeNull();
        originalUpdateTime.ShouldNotBeNull();
        conversation.UpdatedAt.Value.ShouldBeGreaterThan(originalUpdateTime.Value);
    }

    #endregion

    #region BelongsTo Tests

    [TestCase(true, TestName = "BelongsTo_MatchingOwnerId_ReturnsTrue")]
    [TestCase(false, TestName = "BelongsTo_DifferentOwnerId_ReturnsFalse")]
    public void BelongsTo_WithOwnerId_ReturnsExpectedResult(bool useSameOwnerId)
    {
        // Arrange
        var ownerId = CreateUserId();
        var testOwnerId = useSameOwnerId ? ownerId : CreateUserId();
        var conversation = _builder.WithOwner(ownerId).WithUserMessage().Build();

        // Act & Assert
        conversation.BelongsTo(testOwnerId).ShouldBe(useSameOwnerId);
        if (useSameOwnerId)
            conversation.ShouldBelongTo(testOwnerId);
    }

    #endregion

    #region State and Properties Tests

    [TestCase(4, new[] { "Message 1", "Message 2", "Message 3", "Message 4" }, TestName = "MessageCount_FourMessages")]
    [TestCase(3, new[] { "First", "Second", "Third" }, TestName = "MessageCount_ThreeMessages")]
    public void MessageCount_And_MessagesOrdered_WorkCorrectly(int expectedCount, string[] messages)
    {
        // Arrange & Act
        var builder = _builder;
        for (int i = 0; i < messages.Length; i++)
        {
            if (i % 2 == 0)
                builder.WithUserMessage(messages[i]);
            else
                builder.WithAssistantMessage(messages[i]);
        }
        var conversation = builder.Build();

        // Assert
        conversation.ShouldHaveMessageCount(expectedCount);
        var orderedMessages = conversation.MessagesOrdered.ToList();
        for (int i = 0; i < messages.Length; i++)
        {
            orderedMessages[i].Content.Value.ShouldBe(messages[i]);
            orderedMessages[i].Sequence.ShouldBe(i + 1); // Sequence is 1-based
        }
    }

    [TestCase(true, TestName = "LastAiResponseId_WithAssistantMessages_ReturnsLatestId")]
    [TestCase(false, TestName = "LastAiResponseId_WithoutAssistantMessages_ReturnsNull")]
    public void LastAiResponseId_ReturnsExpectedValue(bool hasAssistantMessages)
    {
        // Arrange
        AiResponseId? expectedId = null;
        var builder = _builder.WithUserMessage();
        
        if (hasAssistantMessages)
        {
            var firstId = CreateAiResponseId();
            expectedId = CreateAiResponseId();
            builder.WithAssistantMessage(aiResponseId: firstId)
                   .WithUserMessage()
                   .WithAssistantMessage(aiResponseId: expectedId);
        }
        
        var conversation = builder.Build();

        // Act & Assert
        conversation.LastAiResponseId.ShouldBe(expectedId);
    }

    [TestCase(null, true, TestName = "HasDefaultTitle_WithNullTitle_ReturnsTrue")]
    [TestCase("Custom Title", false, TestName = "HasDefaultTitle_WithCustomTitle_ReturnsFalse")]
    public void HasDefaultTitle_ReturnsExpectedResult(string? title, bool expectedResult)
    {
        // Arrange & Act
        var conversation = _builder.WithTitle(title).WithUserMessage().Build();

        // Assert
        conversation.HasDefaultTitle.ShouldBe(expectedResult);
    }

    [TestCase(true, true, TestName = "IsActive_WithActiveStatus_ReturnsTrue")]
    [TestCase(false, false, TestName = "IsActive_WithCompletedStatus_ReturnsFalse")]
    public void IsActive_ReturnsExpectedResult(bool isActive, bool expectedResult)
    {
        // Arrange & Act
        var builder = _builder.WithUserMessage();
        if (!isActive)
            builder.ThatShouldBeCompleted();
        var conversation = builder.Build();

        // Assert
        conversation.IsActive.ShouldBe(expectedResult);
        if (expectedResult)
            conversation.ShouldBeActive();
        else
            conversation.ShouldBeCompleted();
    }

    #endregion

    #region Helper Methods

    private static string GetTestContent(string contentType) => contentType switch
    {
        "" => "",
        "   " => "   ",
        "valid-max" => TestConstants.EdgeCases.ExactMaxMessage,
        "invalid-long" => TestConstants.EdgeCases.OneOverMaxMessage,
        _ => contentType
    };

    private static string GetTestTitle(string titleType) => titleType switch
    {
        "" => "",
        "   " => "   ",
        "valid-max" => TestConstants.EdgeCases.ExactMaxTitle,
        "invalid-long" => TestConstants.EdgeCases.OneOverMaxTitle,
        _ => titleType
    };

    #endregion
}