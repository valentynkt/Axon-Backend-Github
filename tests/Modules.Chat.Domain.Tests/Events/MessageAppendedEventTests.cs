using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Tests.Events._Fixtures;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for User/AssistantMessageAppendedEvent contract and behavior.
/// Validates sequence, preview rules, timestamp discipline, and turn-taking enforcement.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
public class MessageAppendedEventTests
{
    [Test]
    public void AppendUserMessage_ShouldRaiseEventWithCorrectSequence()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents(); // Clear the ConversationStartedEvent
        
        // Act
        var result = conversation.AppendUserMessage("Hello", EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var events = conversation.GetUncommittedEvents();
        events.ShouldHaveSingleItem();
        
        var messageEvent = events.Single().ShouldBeOfType<UserMessageAppendedEvent>();
        messageEvent.ConversationId.ShouldBe(conversation.Id);
        messageEvent.Sequence.ShouldBe(1); // First message
        messageEvent.ContentPreview.ShouldBe("Hello");
        messageEvent.CreatedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void AppendAssistantMessage_ShouldRaiseEventWithCorrectSequence()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act - Assistant can be first message
        var result = conversation.AppendAssistantMessage("Hi there", EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<AssistantMessageAppendedEvent>();
        messageEvent.Sequence.ShouldBe(1);
        messageEvent.ContentPreview.ShouldBe("Hi there");
    }

    [Test]
    public void AppendMessages_ShouldIncrementSequenceFromOne()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act - Add multiple messages
        conversation.AppendUserMessage("First", EventTestFixture.TestClock);
        conversation.AppendAssistantMessage("Second", EventTestFixture.TestClock);
        conversation.AppendUserMessage("Third", EventTestFixture.TestClock);

        // Assert
        var events = conversation.GetUncommittedEvents().ToList();
        events.ShouldHaveCount(3);
        
        var userEvent1 = events[0].ShouldBeOfType<UserMessageAppendedEvent>();
        userEvent1.Sequence.ShouldBe(1);
        
        var assistantEvent = events[1].ShouldBeOfType<AssistantMessageAppendedEvent>();
        assistantEvent.Sequence.ShouldBe(2);
        
        var userEvent2 = events[2].ShouldBeOfType<UserMessageAppendedEvent>();
        userEvent2.Sequence.ShouldBe(3);
    }

    [Test]
    public void AppendMessage_WithShortContent_ShouldUseFullContentAsPreview()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act
        conversation.AppendUserMessage(EventTestFixture.ShortContent, EventTestFixture.TestClock);

        // Assert
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<UserMessageAppendedEvent>();
        messageEvent.ContentPreview.ShouldBe(EventTestFixture.ShortContent);
        EventTestFixture.IsValidPreview(EventTestFixture.ShortContent, messageEvent.ContentPreview).ShouldBeTrue();
    }

    [Test]
    public void AppendMessage_WithExactly100Characters_ShouldUseFullContentAsPreview()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act
        conversation.AppendUserMessage(EventTestFixture.ExactlyHundredChars, EventTestFixture.TestClock);

        // Assert
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<UserMessageAppendedEvent>();
        messageEvent.ContentPreview.ShouldBe(EventTestFixture.ExactlyHundredChars);
        messageEvent.ContentPreview.Length.ShouldBe(100);
        EventTestFixture.IsValidPreview(EventTestFixture.ExactlyHundredChars, messageEvent.ContentPreview).ShouldBeTrue();
    }

    [Test]
    public void AppendMessage_With101Characters_ShouldTruncateAt100WithoutEllipsis()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act
        conversation.AppendUserMessage(EventTestFixture.OnePastHundredChars, EventTestFixture.TestClock);

        // Assert
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<UserMessageAppendedEvent>();
        messageEvent.ContentPreview.Length.ShouldBe(100);
        messageEvent.ContentPreview.ShouldBe(EventTestFixture.OnePastHundredChars[..100]);
        messageEvent.ContentPreview.ShouldNotContain("..."); // No ellipsis per spec
        EventTestFixture.IsValidPreview(EventTestFixture.OnePastHundredChars, messageEvent.ContentPreview).ShouldBeTrue();
    }

    [Test]
    public void AppendMessage_WithVeryLongContent_ShouldTruncateExactlyAt100Characters()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act
        conversation.AppendUserMessage(EventTestFixture.VeryLongContent, EventTestFixture.TestClock);

        // Assert
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<UserMessageAppendedEvent>();
        messageEvent.ContentPreview.Length.ShouldBe(100);
        messageEvent.ContentPreview.ShouldBe(EventTestFixture.VeryLongContent[..100]);
        messageEvent.ContentPreview.ShouldNotEndWith("...");
        EventTestFixture.IsValidPreview(EventTestFixture.VeryLongContent, messageEvent.ContentPreview).ShouldBeTrue();
    }

    [Test]
    public void AppendMessage_WithMaxContentLength_ShouldSucceedAndCreatePreview()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        var maxContent = new string('x', 100_000); // Max allowed content length

        // Act
        var result = conversation.AppendUserMessage(maxContent, EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<UserMessageAppendedEvent>();
        messageEvent.ContentPreview.Length.ShouldBe(100);
        messageEvent.ContentPreview.ShouldBe(new string('x', 100));
    }

    [Test]
    public void AppendConsecutiveAssistantMessages_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.AppendAssistantMessage("First assistant message", EventTestFixture.TestClock);
        conversation.ClearEvents(); // Clear previous events
        
        // Act - Try to append another assistant message (violates turn-taking)
        var result = conversation.AppendAssistantMessage("Second assistant message", EventTestFixture.TestClock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        conversation.GetUncommittedEvents().ShouldBeEmpty(); // No event should be raised on failure
    }

    [Test]
    public void AppendMessage_EventTimestampShouldMatchAggregateUpdateTime()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act
        conversation.AppendUserMessage("Test message", EventTestFixture.TestClock);

        // Assert
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<UserMessageAppendedEvent>();
        messageEvent.CreatedAt.ShouldBe(conversation.UpdatedAtUtc);
        messageEvent.CreatedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void AppendMessage_ShouldHaveUniqueMessageId()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act
        conversation.AppendUserMessage("First", EventTestFixture.TestClock);
        conversation.AppendAssistantMessage("Second", EventTestFixture.TestClock);

        // Assert
        var events = conversation.GetUncommittedEvents().ToList();
        var userEvent = events[0].ShouldBeOfType<UserMessageAppendedEvent>();
        var assistantEvent = events[1].ShouldBeOfType<AssistantMessageAppendedEvent>();
        
        userEvent.MessageId.ShouldNotBe(assistantEvent.MessageId);
        userEvent.MessageId.Value.ShouldNotBe(Guid.Empty);
        assistantEvent.MessageId.Value.ShouldNotBe(Guid.Empty);
    }

    [Test]
    public void AppendAssistantMessage_AfterUser_ShouldSucceed()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.AppendUserMessage("User message", EventTestFixture.TestClock);
        conversation.ClearEvents();
        
        // Act
        var result = conversation.AppendAssistantMessage("Assistant response", EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var messageEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<AssistantMessageAppendedEvent>();
        messageEvent.Sequence.ShouldBe(2); // Second message overall
    }

    [Test]
    public void BothMessageTypes_ShouldHaveCorrectBaseEventProperties()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        
        // Act
        conversation.AppendUserMessage("User msg", EventTestFixture.TestClock);
        conversation.AppendAssistantMessage("Assistant msg", EventTestFixture.TestClock);

        // Assert
        var events = conversation.GetUncommittedEvents().ToList();
        
        foreach (var evt in events)
        {
            evt.EventId.ShouldNotBe(Guid.Empty);
            evt.Version.ShouldBe(1);
            evt.Name.ShouldContain("MessageAppendedEvent");
        }
    }
}