using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Tests.Events._Fixtures;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for ConversationStartedEvent contract and behavior.
/// Validates event emission, payload correctness, and timestamp discipline.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
public class ConversationStartedEventTests
{
    [Test]
    public void StartConversation_WithEmptyTitle_ShouldRaiseEventWithDefaultTitleFlag()
    {
        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var conversation = result.Value;
        
        var events = conversation.GetUncommittedEvents();
        events.ShouldHaveSingleItem();
        
        var startedEvent = events.Single().ShouldBeOfType<ConversationStartedEvent>();
        startedEvent.ConversationId.ShouldBe(conversation.Id);
        startedEvent.OwnerId.ShouldBe(EventTestFixture.TestUserId);
        startedEvent.Title.ShouldBe("");
        startedEvent.IsDefaultTitle.ShouldBeTrue();
        startedEvent.StartedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void StartConversation_WithUserProvidedTitle_ShouldRaiseEventWithUserTitleFlag()
    {
        // Arrange
        const string userTitle = "My Important Chat";

        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock, userTitle);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var conversation = result.Value;
        
        var events = conversation.GetUncommittedEvents();
        events.ShouldHaveSingleItem();
        
        var startedEvent = events.Single().ShouldBeOfType<ConversationStartedEvent>();
        startedEvent.ConversationId.ShouldBe(conversation.Id);
        startedEvent.OwnerId.ShouldBe(EventTestFixture.TestUserId);
        startedEvent.Title.ShouldBe(userTitle);
        startedEvent.IsDefaultTitle.ShouldBeFalse();
        startedEvent.StartedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void StartConversation_WithWhitespaceTitle_ShouldPreserveExactTitle()
    {
        // Arrange
        const string titleWithSpaces = "  Spaced Title  ";

        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock, titleWithSpaces);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var conversation = result.Value;
        
        var startedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationStartedEvent>();
        startedEvent.Title.ShouldBe(titleWithSpaces); // Exact preservation per spec
        startedEvent.IsDefaultTitle.ShouldBeFalse();
    }

    [Test]
    public void StartConversation_ShouldHaveMatchingTimestamps()
    {
        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock);

        // Assert
        var conversation = result.Value;
        var startedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationStartedEvent>();
        
        // Event timestamp must match aggregate state timestamp
        startedEvent.StartedAt.ShouldBe(conversation.CreatedAtUtc);
        startedEvent.StartedAt.ShouldBe(EventTestFixture.TestClock.UtcNow);
    }

    [Test]
    public void StartConversation_WithMaxLengthTitle_ShouldRaiseEventSuccessfully()
    {
        // Arrange - Create a title at exactly the maximum allowed length (200 chars)
        var maxLengthTitle = EventTestFixture.TwoHundredCharTitle;

        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock, maxLengthTitle);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var conversation = result.Value;
        
        var startedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationStartedEvent>();
        startedEvent.Title.ShouldBe(maxLengthTitle);
        startedEvent.Title.Length.ShouldBe(200);
        startedEvent.IsDefaultTitle.ShouldBeFalse();
    }

    [Test]
    public void StartConversation_WithTooLongTitle_ShouldFailAndRaiseNoEvent()
    {
        // Arrange
        var tooLongTitle = EventTestFixture.TooLongTitle;

        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock, tooLongTitle);

        // Assert
        result.IsFailure.ShouldBeTrue();
        
        // Since Start is a factory method, no conversation instance is created on failure
        // Therefore, there's no way to check for uncommitted events - the method returns Result.Failure
    }

    [Test]
    public void StartConversation_EventShouldHaveCorrectBaseProperties()
    {
        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock, "Test Title");
        var conversation = result.Value;
        var startedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationStartedEvent>();

        // Assert base DomainEvent properties
        startedEvent.EventId.ShouldNotBe(Guid.Empty);
        startedEvent.Version.ShouldBe(1);
        startedEvent.Name.ShouldContain("ConversationStartedEvent");
    }

    [Test]
    public void StartConversation_WithCasePreservation_ShouldPreserveTitleCase()
    {
        // Arrange
        const string mixedCaseTitle = "MiXeD CaSe TiTlE";

        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, EventTestFixture.TestClock, mixedCaseTitle);

        // Assert
        var conversation = result.Value;
        var startedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationStartedEvent>();
        startedEvent.Title.ShouldBe(mixedCaseTitle); // Exact case preservation
    }

    [Test]
    public void StartConversation_EventTimestamp_ShouldMatchAggregateCreationTime()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero);
        var clock = new Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time.FixedClock(fixedTime);

        // Act
        var result = Conversation.Start(EventTestFixture.TestUserId, clock, "Test");
        var conversation = result.Value;

        // Assert
        var startedEvent = conversation.GetUncommittedEvents().Single().ShouldBeOfType<ConversationStartedEvent>();
        startedEvent.StartedAt.ShouldBe(fixedTime);
        startedEvent.StartedAt.ShouldBe(conversation.CreatedAtUtc);
        startedEvent.StartedAt.ShouldBe(conversation.UpdatedAtUtc); // At creation, both timestamps are the same
    }
}