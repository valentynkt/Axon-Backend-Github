using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Tests.Events._Fixtures;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for ConversationTitleUpdatedEvent contract and behavior.
/// Validates title update validation, isDefaultTitle flag behavior, and timestamp discipline.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
public class ConversationTitleUpdatedEventTests
{
    [Test]
    public void UpdateTitle_WithValidUserTitle_ShouldRaiseEventWithUserTitleFlag()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation(); // Starts with default title
        conversation.ClearEvents();
        const string newTitle = "Updated Title";

        // Act
        var result = conversation.UpdateTitle(newTitle, EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var events = conversation.GetUncommittedEvents();
        events.ShouldHaveSingleItem();
        
        var titleEvent = events.Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.ConversationId.ShouldBe(conversation.Id);
        titleEvent.Title.ShouldBe(newTitle);
        titleEvent.IsDefaultTitle.ShouldBeFalse(); // User-provided title
        titleEvent.UpdatedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void UpdateTitle_WithMaxLengthTitle_ShouldRaiseEventSuccessfully()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        var maxTitle = EventTestFixture.TwoHundredCharTitle;

        // Act
        var result = conversation.UpdateTitle(maxTitle, EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var titleEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.Title.ShouldBe(maxTitle);
        titleEvent.Title.Length.ShouldBe(200);
        titleEvent.IsDefaultTitle.ShouldBeFalse();
    }

    [Test]
    public void UpdateTitle_WithEmptyTitle_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();

        // Act
        var result = conversation.UpdateTitle("", EventTestFixture.TestClock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        conversation.GetUncommittedEvents().ShouldBeEmpty(); // No event on validation failure
    }

    [Test]
    public void UpdateTitle_WithNullTitle_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();

        // Act
        var result = conversation.UpdateTitle(null!, EventTestFixture.TestClock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        conversation.GetUncommittedEvents().ShouldBeEmpty();
    }

    [Test]
    public void UpdateTitle_WithTooLongTitle_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        var tooLongTitle = EventTestFixture.TooLongTitle; // Over 200 characters

        // Act
        var result = conversation.UpdateTitle(tooLongTitle, EventTestFixture.TestClock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        conversation.GetUncommittedEvents().ShouldBeEmpty(); // No event on validation failure
    }

    [Test]
    public void UpdateTitle_WithWhitespacePreservation_ShouldPreserveExactTitle()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        const string titleWithSpaces = "  Spaced Title  ";

        // Act
        var result = conversation.UpdateTitle(titleWithSpaces, EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var titleEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.Title.ShouldBe(titleWithSpaces); // Exact preservation per spec
        titleEvent.IsDefaultTitle.ShouldBeFalse();
    }

    [Test]
    public void UpdateTitle_WithCasePreservation_ShouldPreserveTitleCase()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        const string mixedCaseTitle = "MiXeD CaSe UpDaTe";

        // Act
        var result = conversation.UpdateTitle(mixedCaseTitle, EventTestFixture.TestClock);

        // Assert
        var titleEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.Title.ShouldBe(mixedCaseTitle); // Exact case preservation
    }

    [Test]
    public void UpdateTitle_EventTimestampShouldMatchAggregateUpdateTime()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();

        // Act
        conversation.UpdateTitle("New Title", EventTestFixture.TestClock);

        // Assert
        var titleEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.UpdatedAt.ShouldBe(conversation.UpdatedAtUtc);
        titleEvent.UpdatedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void UpdateTitle_OnCompletedConversation_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var conversation = EventTestFixture.CreateConversationWithMessages(1);
        conversation.Complete(EventTestFixture.TestClock);
        conversation.ClearEvents();

        // Act
        var result = conversation.UpdateTitle("New Title", EventTestFixture.TestClock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        conversation.GetUncommittedEvents().ShouldBeEmpty();
    }

    [Test]
    public void UpdateTitle_ShouldHaveCorrectBaseEventProperties()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();

        // Act
        conversation.UpdateTitle("Test Title", EventTestFixture.TestClock);

        // Assert
        var titleEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.EventId.ShouldNotBe(Guid.Empty);
        titleEvent.Version.ShouldBe(1);
        titleEvent.Name.ShouldContain("ConversationTitleUpdatedEvent");
    }

    [Test]
    public void UpdateTitle_MultipleUpdates_ShouldRaiseMultipleEvents()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();

        // Act
        conversation.UpdateTitle("First Update", EventTestFixture.TestClock);
        conversation.UpdateTitle("Second Update", EventTestFixture.TestClock);

        // Assert
        var events = conversation.GetUncommittedEvents().ToList();
        events.ShouldHaveCount(2);
        
        var firstEvent = events[0].ShouldBeOfType<ConversationTitleUpdatedEvent>();
        firstEvent.Title.ShouldBe("First Update");
        
        var secondEvent = events[1].ShouldBeOfType<ConversationTitleUpdatedEvent>();
        secondEvent.Title.ShouldBe("Second Update");
        
        // Both should be user-provided titles
        firstEvent.IsDefaultTitle.ShouldBeFalse();
        secondEvent.IsDefaultTitle.ShouldBeFalse();
    }

    [Test]
    public void UpdateTitle_FromUserTitleToAnotherUserTitle_ShouldMaintainUserTitleFlag()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation("Initial User Title");
        conversation.ClearEvents(); // Clear the ConversationStartedEvent

        // Act
        conversation.UpdateTitle("Updated User Title", EventTestFixture.TestClock);

        // Assert
        var titleEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.Title.ShouldBe("Updated User Title");
        titleEvent.IsDefaultTitle.ShouldBeFalse(); // Still user-provided
    }

    [Test]
    public void UpdateTitle_WithSpecialCharacters_ShouldPreserveExactly()
    {
        // Arrange
        var conversation = EventTestFixture.CreateTestConversation();
        conversation.ClearEvents();
        const string specialTitle = "Title with émoji 😊 & special chars: @#$%";

        // Act
        var result = conversation.UpdateTitle(specialTitle, EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        var titleEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationTitleUpdatedEvent>();
        titleEvent.Title.ShouldBe(specialTitle);
        titleEvent.IsDefaultTitle.ShouldBeFalse();
    }
}