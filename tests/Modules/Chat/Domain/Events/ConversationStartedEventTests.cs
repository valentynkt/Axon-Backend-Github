using System.Text.Json;
using Axon.Modules.Chat.Domain.Tests.Common;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for ConversationStartedEvent domain event.
/// </summary>
[TestFixture]
public class ConversationStartedEventTests : EventTestBase
{
    [Test]
    public void Create_WithValidData_ShouldInitializeAllProperties()
    {
        // Arrange
        var conversationId = CreateConversationId();
        var ownerId = CreateAxonUserId();
        var title = TestConstants.Conversations.DefaultTitle;
        var startedAt = CurrentTime;

        // Act
        var domainEvent = new ConversationStartedEvent(conversationId, ownerId, title, startedAt);

        // Assert
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.OwnerId.ShouldBe(ownerId);
        domainEvent.Title.ShouldBe(title);
        domainEvent.StartedAt.ShouldBe(startedAt);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Regular Title")]
    [TestCase("Title with special chars: !@#$%^&*()")]
    [TestCase("Unicode title: 软件架构 🏗️ ñáéíóú")]
    public void Create_WithVariousTitles_ShouldPreserveTitle(string? title)
    {
        // Act
        var domainEvent = new ConversationStartedEvent(
            CreateConversationId(), CreateAxonUserId(), title, CurrentTime);

        // Assert
        domainEvent.Title.ShouldBe(title);
    }

    [Test]
    public void Event_ShouldHaveValueEquality()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var conversationId = CreateConversationId();
        var ownerId = CreateAxonUserId();
        var title = TestConstants.Conversations.DefaultTitle;
        var startedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new TestableConversationStartedEvent(eventId, conversationId, ownerId, title, startedAt);
        var event2 = new TestableConversationStartedEvent(eventId, conversationId, ownerId, title, startedAt);

        // Assert
        event1.ShouldBe(event2);
        event1.GetHashCode().ShouldBe(event2.GetHashCode());
    }

    [Test]
    public void Events_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var ownerId = CreateAxonUserId();
        var title = TestConstants.Conversations.DefaultTitle;
        var startedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new ConversationStartedEvent(CreateConversationId(), ownerId, title, startedAt);
        var event2 = new ConversationStartedEvent(CreateConversationId(), ownerId, title, startedAt);

        // Assert
        event1.ShouldNotBe(event2);
    }

    [TestCase("Title1", "Title2")]
    [TestCase(null, "")]
    [TestCase("", "   ")]
    public void Events_WithDifferentProperties_ShouldNotBeEqual(string? title1, string? title2)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var ownerId = CreateAxonUserId();
        var startedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new ConversationStartedEvent(conversationId, ownerId, title1, startedAt);
        var event2 = new ConversationStartedEvent(conversationId, ownerId, title2, startedAt);

        // Assert
        event1.ShouldNotBe(event2);
    }

    [TestCase("")]
    [TestCase("Regular Title")]
    [TestCase("Special chars: !@#$%^&*()[]{}|\\:;\"'<>,.?/")]
    [TestCase("Unicode: 软件架构模式 🏗️ ñáéíóú")]
    public void Event_ShouldSerializeAndDeserializeCorrectly(string? title)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var ownerId = CreateAxonUserId();
        var startedAt = TestConstants.DateTimes.DefaultTestTime;
        var originalEvent = new ConversationStartedEvent(conversationId, ownerId, title, startedAt);

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<ConversationStartedEvent>(json);

        // Assert
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.ShouldBe(originalEvent);
    }

    [Test]
    public void Event_WithLongTitle_ShouldHandleCorrectly()
    {
        // Arrange
        var longTitle = TestConstants.EdgeCases.ExactMaxTitle;

        // Act
        var domainEvent = new ConversationStartedEvent(
            CreateConversationId(), CreateAxonUserId(), longTitle, CurrentTime);

        // Assert
        domainEvent.Title.ShouldBe(longTitle);
        domainEvent.Title!.Length.ShouldBe(TestConstants.Limits.MaxConversationTitleLength);
    }

    [Test]
    public void Event_ShouldRepresentConversationCreation()
    {
        // Arrange - Business scenario: User starts a new conversation
        var conversationId = CreateConversationId();
        var ownerId = CreateAxonUserId();
        var title = "DDD Architecture Discussion";
        var startedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var domainEvent = new ConversationStartedEvent(conversationId, ownerId, title, startedAt);

        // Assert - Event captures essential conversation creation information
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.OwnerId.ShouldBe(ownerId);
        domainEvent.Title.ShouldBe(title);
        domainEvent.StartedAt.ShouldBe(startedAt);
    }
}