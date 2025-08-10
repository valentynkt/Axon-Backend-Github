using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Tests.Events._Fixtures;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for ConversationCompletedEvent contract and behavior.
/// Validates completion conditions, message count accuracy, and timestamp discipline.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
public class ConversationCompletedEventTests
{
    [Test]
    public void CompleteConversation_WithOneMessage_ShouldRaiseEventWithCorrectMessageCount()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.ClearEvents(); // Clear creation and message events

        // Act
        var result = conversation.Complete(EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var events = conversation.GetUncommittedEvents();
        events.ShouldHaveSingleItem();
        
        var completedEvent = events.Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.ConversationId.ShouldBe(conversation.Id);
        completedEvent.MessageCount.ShouldBe(1);
        completedEvent.CompletedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void CompleteConversation_WithMultipleMessages_ShouldRaiseEventWithCorrectMessageCount()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(5);
        conversation.ClearEvents();

        // Act
        var result = conversation.Complete(EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.MessageCount.ShouldBe(5);
    }

    [Test]
    public void CompleteConversation_WithNoMessages_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation(); // Empty conversation
        conversation.ClearEvents();

        // Act
        var result = conversation.Complete(EventTestFixture.TestClock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        conversation.GetUncommittedEvents().ShouldBeEmpty(); // No event on failure
    }

    [Test]
    public void CompleteConversation_EventTimestampShouldMatchAggregateUpdateTime()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.ClearEvents();

        // Act
        conversation.Complete(EventTestFixture.TestClock);

        // Assert
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.CompletedAt.ShouldBe(conversation.UpdatedAtUtc);
        completedEvent.CompletedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void CompleteConversation_AlreadyCompleted_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.Complete(EventTestFixture.TestClock); // Complete once
        conversation.ClearEvents(); // Clear the completion event

        // Act - Try to complete again
        var result = conversation.Complete(EventTestFixture.TestClock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        conversation.GetUncommittedEvents().ShouldBeEmpty(); // No event on repeated completion
    }

    [Test]
    public void CompleteConversation_ShouldHaveCorrectBaseEventProperties()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.ClearEvents();

        // Act
        conversation.Complete(EventTestFixture.TestClock);

        // Assert
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.EventId.ShouldNotBe(Guid.Empty);
        completedEvent.Version.ShouldBe(1);
        completedEvent.Name.ShouldContain("ConversationCompletedEvent");
    }

    [Test]
    public void CompleteConversation_WithExactlyOneMessage_ShouldSucceed()
    {
        // Arrange - Test boundary condition
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.AppendUserMessage("Single message", EventTestFixture.TestClock);
        conversation.ClearEvents();

        // Act
        var result = conversation.Complete(EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.MessageCount.ShouldBe(1);
    }

    [Test]
    public void CompleteConversation_WithManyMessages_ShouldReportAccurateCount()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        const int messageCount = 10;
        
        // Add alternating user and assistant messages
        for (int i = 0; i < messageCount; i++)
        {
            if (i % 2 == 0)
            {
                conversation.AppendUserMessage($"User message {i}", EventTestFixture.TestClock);
            }
            else
            {
                conversation.AppendAssistantMessage($"Assistant message {i}", EventTestFixture.TestClock);
            }
        }
        conversation.ClearEvents();

        // Act
        var result = conversation.Complete(EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.MessageCount.ShouldBe(messageCount);
    }

    [Test]
    public void CompleteConversation_MessageCountShouldMatchActualMessages()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.AppendUserMessage("Message 1", EventTestFixture.TestClock);
        conversation.AppendAssistantMessage("Message 2", EventTestFixture.TestClock);
        conversation.AppendUserMessage("Message 3", EventTestFixture.TestClock);
        
        var actualMessageCount = conversation.MessageCount;
        conversation.ClearEvents();

        // Act
        conversation.Complete(EventTestFixture.TestClock);

        // Assert
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.MessageCount.ShouldBe(actualMessageCount);
        completedEvent.MessageCount.ShouldBe(3);
    }

    [Test]
    public void CompleteConversation_WithCustomTimestamp_ShouldUseProvidedClock()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.ClearEvents();
        
        var customTime = new DateTimeOffset(2024, 6, 15, 16, 45, 30, TimeSpan.Zero);
        var customClock = new Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time.FixedClock(customTime);

        // Act
        conversation.Complete(customClock);

        // Assert
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.CompletedAt.ShouldBe(customTime);
        completedEvent.CompletedAt.ShouldBe(conversation.UpdatedAtUtc);
    }

    [Test]
    public void CompleteConversation_AfterTitleUpdate_ShouldStillSucceed()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.UpdateTitle("Updated Title", EventTestFixture.TestClock);
        conversation.ClearEvents();

        // Act
        var result = conversation.Complete(EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var completedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationCompletedEvent>();
        completedEvent.MessageCount.ShouldBe(1);
    }

    [Test]
    public void CompleteConversation_ShouldPreventFurtherModifications()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.Complete(EventTestFixture.TestClock);
        conversation.ClearEvents();

        // Act & Assert - All modification attempts should fail
        var messageResult = conversation.AppendUserMessage("New message", EventTestFixture.TestClock);
        messageResult.IsFailure.ShouldBeTrue();
        
        var titleResult = conversation.UpdateTitle("New title", EventTestFixture.TestClock);
        titleResult.IsFailure.ShouldBeTrue();

        // No events should be raised for failed operations
        conversation.GetUncommittedEvents().ShouldBeEmpty();
    }
}