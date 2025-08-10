using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Base;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Mothers;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Extensions;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

namespace Axon.Modules.Chat.Domain.Tests.Aggregates;

/// <summary>
/// World-class tests for Conversation aggregate following DDD best practices.
/// Uses Given-When-Then pattern with fluent builders and comprehensive assertions.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Aggregate")]
[Category("Critical")]
public sealed class ConversationTests : AggregateTestBase<Conversation>
{
    #region Test Setup

    private ConversationBuilder _builder = null!;
    private UserId _defaultOwner = null!;
    private ConversationTitle _validTitle = null!;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        _builder = ConversationBuilder.Create();
        _defaultOwner = UserMother.DefaultUser();
        _validTitle = ConversationTitle.Create("Test Conversation").Value;
    }

    #endregion

    #region Start Conversation Tests - Given-When-Then Pattern

    [Test]
    public void Given_ValidOwnerAndNullTitle_When_StartingConversation_Then_ShouldCreateWithDefaultTitle()
    {
        // Given
        var owner = _defaultOwner;
        string? title = null;

        // When
        var result = Conversation.Start(owner, title, Clock);

        // Then
        result.ShouldBeSuccess();
        var conversation = result.Value;
        conversation.ShouldSatisfyAllConditions(
            () => conversation.OwnerId.ShouldBe(owner),
            () => conversation.ShouldBeActive(),
            () => conversation.ShouldHaveDefaultTitle(),
            () => conversation.Title.ShouldBe(string.Empty),
            () => conversation.ShouldHaveMessageCount(0),
            () => conversation.CreatedAtUtc.ShouldBe(TestTime),
            () => conversation.UpdatedAtUtc.ShouldBe(TestTime)
        );
    }

    [Test]
    public void Given_ValidOwnerAndUserTitle_When_StartingConversation_Then_ShouldPreserveTitleExactly()
    {
        // Given
        var owner = _defaultOwner;
        var title = "  My Important Chat  ";

        // When
        var result = Conversation.Start(owner, title, Clock);

        // Then
        result.ShouldBeSuccessWith(conversation =>
        {
            conversation.ShouldHaveUserTitle("My Important Chat");
            conversation.IsDefaultTitle.ShouldBeFalse();
        });
    }

    [Test]
    public void Given_NullOwner_When_StartingConversation_Then_ShouldReturnValidationError()
    {
        // Given
        UserId? nullOwner = null;

        // When
        var result = Conversation.Start(nullOwner!, "Title", Clock);

        // Then
        result.ShouldBeValidationFailure();
        result.ShouldBeFailureWithCode("CHAT.CONVERSATION.OWNER_REQUIRED");
    }

    [Test]
    public void Given_ValidParameters_When_StartingConversation_Then_ShouldRaiseConversationStartedEvent()
    {
        // Given
        var owner = _defaultOwner;
        var title = "Event Test Chat";

        // When
        var result = Conversation.Start(owner, title, Clock);

        // Then
        var conversation = result.ShouldBeSuccessWithValue();
        conversation.ShouldHaveRaisedEvent<ConversationStartedEvent>(evt =>
        {
            evt.ShouldBeValidStartedEvent(conversation.Id, owner);
            evt.Title.ShouldBe(title);
            evt.IsDefaultTitle.ShouldBeFalse();
            evt.StartedAt.ShouldBe(TestTime);
        });
    }

    #endregion

    #region Append User Message Tests

    [Test]
    public void Given_ActiveConversation_When_AppendingValidUserMessage_Then_ShouldSucceed()
    {
        // Given
        var conversation = _builder
            .WithDefaultOwner()
            .Active()
            .Build();
        var content = MessageMother.ValidUserMessage();

        // When
        var result = conversation.AppendUserMessage(content, Clock);

        // Then
        result.ShouldBeSuccessWith(message =>
        {
            message.Role.ShouldBe(MessageRole.User);
            message.Content.ShouldBe(content);
            message.Sequence.ShouldBe(1);
            message.CreatedAtUtc.ShouldBe(TestTime);
        });
        conversation.ShouldHaveMessageCount(1);
        conversation.UpdatedAtUtc.ShouldBe(TestTime);
    }

    [Test]
    public void Given_CompletedConversation_When_AppendingUserMessage_Then_ShouldFailWithNotActiveError()
    {
        // Given
        var conversation = ConversationMother.Completed();
        var content = MessageMother.ValidUserMessage();

        // When
        var result = conversation.AppendUserMessage(content, Clock);

        // Then
        result.ShouldBeFailureWithCode("CHAT.CONVERSATION.NOT_ACTIVE");
    }

    [Test]
    public void Given_ConversationAtMessageLimit_When_AppendingMessage_Then_ShouldFailWithLimitExceeded()
    {
        // Given
        var conversation = ConversationMother.AtMessageLimit();
        var content = MessageMother.ValidUserMessage();

        // When
        var result = conversation.AppendUserMessage(content, Clock);

        // Then
        result.ShouldBeFailureWithCode("CHAT.CONVERSATION.MESSAGE_LIMIT_EXCEEDED");
    }

    [Test]
    public void Given_ActiveConversation_When_AppendingUserMessage_Then_ShouldRaiseUserMessageAppendedEvent()
    {
        Scenario(
            "User message appending raises correct event",
            given: () => _builder.WithDefaultOwner().Build(),
            when: conversation => conversation.AppendUserMessage(
                MessageMother.ValidUserMessage(), Clock),
            then: conversation =>
            {
                conversation.ShouldHaveRaisedEvent<UserMessageAppendedEvent>(evt =>
                {
                    evt.ShouldBeValidUserMessageEvent(conversation.Id, 1);
                    evt.CreatedAt.ShouldBe(TestTime);
                });
            }
        );
    }

    [Test]
    public void Given_EmptyConversation_When_AppendingMultipleUserMessages_Then_ShouldAllowConsecutiveUserMessages()
    {
        // Given
        var conversation = ConversationMother.Empty();

        // When
        var results = new[]
        {
            conversation.AppendUserMessage(MessageMother.Question(), Clock),
            conversation.AppendUserMessage(MessageMother.ValidUserMessage(), Clock),
            conversation.AppendUserMessage(MessageMother.Greeting(), Clock)
        };

        // Then
        results.ShouldAllBeSuccess();
        conversation.ShouldHaveMessageCount(3);
        conversation.MessagesOrdered.ShouldBeInSequentialOrder();
    }

    #endregion

    #region Append Assistant Message Tests

    [Test]
    public void Given_EmptyConversation_When_AppendingAssistantMessage_Then_ShouldSucceedAsFirstMessage()
    {
        // Given
        var conversation = ConversationMother.Empty();
        var content = MessageMother.ValidAssistantMessage();

        // When
        var result = conversation.AppendAssistantMessage(content, Clock);

        // Then
        result.ShouldBeSuccessWith(message =>
        {
            message.Role.ShouldBe(MessageRole.Assistant);
            message.Sequence.ShouldBe(1);
        });
        conversation.ShouldHaveMessageCount(1);
    }

    [Test]
    public void Given_ConversationWithAssistantMessage_When_AppendingAnotherAssistant_Then_ShouldFailWithTurnTakingViolation()
    {
        // Given
        var conversation = ConversationMother.AssistantInitiated();
        var content = MessageMother.ValidAssistantMessage();

        // When
        var result = conversation.AppendAssistantMessage(content, Clock);

        // Then
        result.ShouldBeFailureWithCode("CHAT.MESSAGE.TURN_TAKING_VIOLATION");
        result.Error.Message.ShouldContain("Assistant cannot reply twice in a row");
    }

    [Test]
    public void Given_ConversationWithUserMessage_When_AppendingAssistant_Then_ShouldSucceed()
    {
        // Given
        var conversation = ConversationMother.WithSingleUserMessage();
        var content = MessageMother.ValidAssistantMessage();

        // When
        var result = conversation.AppendAssistantMessage(content, Clock);

        // Then
        result.ShouldBeSuccess();
        conversation.ShouldHaveMessageCount(2);
        conversation.MessagesOrdered[1].Role.ShouldBe(MessageRole.Assistant);
    }

    #endregion

    #region Update Title Tests

    [Test]
    public void Given_ActiveConversationWithDefaultTitle_When_UpdatingTitle_Then_ShouldSucceed()
    {
        // Given
        var conversation = ConversationMother.Empty();
        var newTitle = ConversationTitle.Create("Updated Title").Value;

        // When
        var result = conversation.UpdateTitle(newTitle, Clock);

        // Then
        result.ShouldBeSuccess();
        conversation.Title.ShouldBe("Updated Title");
        conversation.IsDefaultTitle.ShouldBeFalse();
        conversation.UpdatedAtUtc.ShouldBe(TestTime);
    }

    [Test]
    public void Given_CompletedConversation_When_UpdatingTitle_Then_ShouldFailWithNotActiveError()
    {
        // Given
        var conversation = ConversationMother.Completed();
        var newTitle = ConversationTitle.Create("New Title").Value;

        // When
        var result = conversation.UpdateTitle(newTitle, Clock);

        // Then
        result.ShouldBeFailureWithCode("CHAT.CONVERSATION.NOT_ACTIVE");
    }

    [Test]
    public void Given_ActiveConversation_When_UpdatingTitle_Then_ShouldRaiseTitleUpdatedEvent()
    {
        ExecuteCommandAndAssertEvents<ConversationTitleUpdatedEvent>(
            ConversationMother.Empty(),
            conversation => conversation.UpdateTitle(
                ConversationTitle.Create("New Title").Value, Clock),
            evt =>
            {
                evt.Title.ShouldBe("New Title");
                evt.IsDefaultTitle.ShouldBeFalse();
                evt.UpdatedAt.ShouldBe(TestTime);
            }
        );
    }

    #endregion

    #region Complete Conversation Tests

    [Test]
    public void Given_ConversationWithMessages_When_Completing_Then_ShouldSucceed()
    {
        // Given
        var conversation = ConversationMother.SimpleQA();

        // When
        var result = conversation.Complete(Clock);

        // Then
        result.ShouldBeSuccess();
        conversation.ShouldBeCompleted();
        conversation.UpdatedAtUtc.ShouldBe(TestTime);
    }

    [Test]
    public void Given_EmptyConversation_When_Completing_Then_ShouldFailWithRequiresMessagesError()
    {
        // Given
        var conversation = ConversationMother.Empty();

        // When
        var result = conversation.Complete(Clock);

        // Then
        result.ShouldBeFailureWithCode("CHAT.CONVERSATION.COMPLETE_REQUIRES_MESSAGES");
    }

    [Test]
    public void Given_AlreadyCompletedConversation_When_CompletingAgain_Then_ShouldFailWithNotActiveError()
    {
        // Given
        var conversation = ConversationMother.Completed();

        // When
        var result = conversation.Complete(Clock);

        // Then
        result.ShouldBeFailureWithCode("CHAT.CONVERSATION.NOT_ACTIVE");
    }

    [Test]
    public void Given_ConversationWithMessages_When_Completing_Then_ShouldRaiseCompletedEvent()
    {
        // Given
        var conversation = ConversationMother.LongConversation();
        var messageCount = conversation.MessageCount;

        // When
        conversation.Complete(Clock);

        // Then
        conversation.ShouldHaveRaisedEvent<ConversationCompletedEvent>(evt =>
        {
            evt.ShouldBeValidCompletedEvent(conversation.Id, messageCount);
            evt.CompletedAt.ShouldBe(TestTime);
        });
    }

    #endregion

    #region Message Sequencing Tests

    [Test]
    public void Given_ConversationWithMultipleMessages_Then_MessagesShouldHaveSequentialNumbers()
    {
        // Given & When
        var conversation = _builder
            .WithConversationFlow(
                "Message 1",
                "Response 1",
                "Message 2",
                "Response 2",
                "Message 3")
            .Build();

        // Then
        var messages = conversation.MessagesOrdered;
        messages.ShouldBeInSequentialOrder();
        messages.ShouldBeInChronologicalOrder();
    }

    #endregion

    #region Business Rule Tests

    [Test]
    public void Given_Conversation_Then_ShouldMaintainInvariants()
    {
        AssertInvariantsHold(
            ConversationMother.Empty(),
            c => c.AppendUserMessage(MessageMother.ValidUserMessage(), Clock),
            c => c.AppendAssistantMessage(MessageMother.ValidAssistantMessage(), Clock),
            c => c.UpdateTitle(ConversationTitle.Create("New").Value, Clock)
        );
    }

    protected override void AssertInvariants(Conversation aggregate)
    {
        base.AssertInvariants(aggregate);
        
        // Conversation-specific invariants
        aggregate.MessageCount.ShouldBeGreaterThanOrEqualTo(0);
        aggregate.MessageCount.ShouldBeLessThanOrEqualTo(10000);
        aggregate.CreatedAtUtc.ShouldBeLessThanOrEqualTo(aggregate.UpdatedAtUtc);
        
        if (aggregate.Status == ConversationStatus.Completed)
        {
            aggregate.MessageCount.ShouldBeGreaterThan(0);
        }
        
        if (aggregate.MessagesOrdered.Any())
        {
            aggregate.MessagesOrdered.ShouldBeInSequentialOrder();
        }
    }

    #endregion

    #region Helper Method Tests

    [Test]
    public void Given_Conversation_When_CheckingOwnership_Then_ShouldReturnCorrectResult()
    {
        // Given
        var owner = UserMother.Named.Alice();
        var otherUser = UserMother.Named.Bob();
        var conversation = _builder.WithOwner(owner).Build();

        // Then
        conversation.BelongsTo(owner).ShouldBeTrue();
        conversation.BelongsTo(otherUser).ShouldBeFalse();
    }

    [Test]
    public void Given_ActiveConversation_When_CheckingIsActive_Then_ShouldReturnTrue()
    {
        // Given
        var conversation = ConversationMother.Empty();

        // Then
        conversation.IsActive.ShouldBeTrue();
    }

    [Test]
    public void Given_CompletedConversation_When_CheckingIsActive_Then_ShouldReturnFalse()
    {
        // Given
        var conversation = ConversationMother.Completed();

        // Then
        conversation.IsActive.ShouldBeFalse();
    }

    #endregion

    #region Time Discipline Tests

    [Test]
    public void Given_DifferentClocks_When_PerformingOperations_Then_ShouldUseProvidedTime()
    {
        // Given
        var conversation = ConversationMother.Empty();
        var time1 = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var time2 = new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero);
        var time3 = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // When
        conversation.AppendUserMessage(
            MessageMother.ValidUserMessage(),
            new FixedClock(time1));
        
        conversation.UpdateTitle(
            ConversationTitle.Create("Title").Value,
            new FixedClock(time2));
        
        conversation.Complete(new FixedClock(time3));

        // Then
        conversation.MessagesOrdered[0].CreatedAtUtc.ShouldBe(time1);
        conversation.UpdatedAtUtc.ShouldBe(time3);
        
        var events = conversation.DomainEvents;
        events.OfType<UserMessageAppendedEvent>().First().CreatedAt.ShouldBe(time1);
        events.OfType<ConversationTitleUpdatedEvent>().First().UpdatedAt.ShouldBe(time2);
        events.OfType<ConversationCompletedEvent>().First().CompletedAt.ShouldBe(time3);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Given_InvalidMessageContent_When_Creating_Then_ShouldFailValidation(string? invalidContent)
    {
        // Given
        var contentResult = MessageContent.Create(invalidContent!);

        // Then
        contentResult.ShouldBeFailure();
    }

    [Test]
    public void Given_MaxLengthTitle_When_Creating_Then_ShouldSucceed()
    {
        // Given
        var maxTitle = new string('a', 200); // Max length
        
        // When
        var result = ConversationTitle.Create(maxTitle);

        // Then
        result.ShouldBeSuccess();
        result.Value.Value.Length.ShouldBe(200);
    }

    [Test]
    public void Given_TooLongTitle_When_Creating_Then_ShouldFail()
    {
        // Given
        var tooLongTitle = new string('a', 201); // Over max length
        
        // When
        var result = ConversationTitle.Create(tooLongTitle);

        // Then
        result.ShouldBeFailure();
    }

    #endregion

    #region Performance Tests

    [Test]
    [Timeout(1000)] // Should complete within 1 second
    public void Given_LargeNumberOfMessages_When_Accessing_Then_ShouldBePerformant()
    {
        // Given
        var conversation = _builder
            .WithManyMessages(1000)
            .Build();

        // When & Then
        conversation.MessageCount.ShouldBe(1000);
        conversation.MessagesOrdered.Count.ShouldBe(1000);
    }

    #endregion
}