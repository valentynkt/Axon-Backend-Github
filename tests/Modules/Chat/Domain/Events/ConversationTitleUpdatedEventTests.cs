using System.Text.Json;
using Axon.Modules.Chat.Domain.Tests.Common;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for ConversationTitleUpdatedEvent domain event.
/// </summary>
[TestFixture]
public class ConversationTitleUpdatedEventTests : EventTestBase
{
    [Test]
    public void Create_WithValidData_ShouldInitializeAllProperties()
    {
        // Arrange
        var conversationId = CreateConversationId();
        var title = TestConstants.Conversations.DefaultTitle;
        var updatedAt = CurrentTime;

        // Act
        var domainEvent = new ConversationTitleUpdatedEvent(conversationId, title, updatedAt);

        // Assert
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.Title.ShouldBe(title);
        domainEvent.UpdatedAt.ShouldBe(updatedAt);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Regular Title")]
    [TestCase("Title with special chars: !@#$%^&*()")]
    [TestCase("Unicode: 软件架构模式 🏗️")]
    public void Create_WithVariousTitleInputs_ShouldPreserveTitle(string? title)
    {
        // Act
        var domainEvent = new ConversationTitleUpdatedEvent(
            CreateConversationId(), title!, CurrentTime);

        // Assert
        domainEvent.Title.ShouldBe(title);
    }

    [Test]
    public void Event_ShouldHaveValueEquality()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var conversationId = CreateConversationId();
        var title = TestConstants.Conversations.DefaultTitle;
        var updatedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new TestableConversationTitleUpdatedEvent(eventId, conversationId, title, updatedAt);
        var event2 = new TestableConversationTitleUpdatedEvent(eventId, conversationId, title, updatedAt);

        // Assert
        event1.ShouldBe(event2);
        event1.GetHashCode().ShouldBe(event2.GetHashCode());
    }

    [Test]
    public void Events_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var title = TestConstants.Conversations.DefaultTitle;
        var updatedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new ConversationTitleUpdatedEvent(CreateConversationId(), title, updatedAt);
        var event2 = new ConversationTitleUpdatedEvent(CreateConversationId(), title, updatedAt);

        // Assert
        event1.ShouldNotBe(event2);
    }

    [TestCase("Title1", "Title2", false)]
    [TestCase("Same", "Same", true)]
    [TestCase(null, "", false)]
    [TestCase("", "", true)]
    public void Events_WithDifferentProperties_ShouldHaveExpectedEquality(
        string? title1, 
        string? title2, 
        bool shouldBeEqual)
    {
        // Arrange
        var eventId = shouldBeEqual ? Guid.NewGuid() : Guid.NewGuid();
        var secondEventId = shouldBeEqual ? eventId : Guid.NewGuid();
        var conversationId = CreateConversationId();
        var updatedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new TestableConversationTitleUpdatedEvent(eventId, conversationId, title1!, updatedAt);
        var event2 = new TestableConversationTitleUpdatedEvent(secondEventId, conversationId, title2!, updatedAt);

        // Assert
        if (shouldBeEqual)
        {
            event1.ShouldBe(event2);
        }
        else
        {
            event1.ShouldNotBe(event2);
        }
    }

    [TestCase("")]
    [TestCase("Regular Title")]
    [TestCase("Special chars: !@#$%^&*()[]{}|\\:;\"'<>,.?/")]
    [TestCase("Unicode: 软件架构模式 🏗️ ñáéíóú")]
    public void Event_ShouldSerializeAndDeserializeCorrectly(string title)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var updatedAt = TestConstants.DateTimes.DefaultTestTime;
        var originalEvent = new ConversationTitleUpdatedEvent(conversationId, title, updatedAt);

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<ConversationTitleUpdatedEvent>(json);

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
        var domainEvent = new ConversationTitleUpdatedEvent(
            CreateConversationId(), longTitle, CurrentTime);

        // Assert
        domainEvent.Title.ShouldBe(longTitle);
        domainEvent.Title.Length.ShouldBe(TestConstants.Limits.MaxConversationTitleLength);
    }
}