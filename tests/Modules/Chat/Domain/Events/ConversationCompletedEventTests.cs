using System.Text.Json;
using Axon.Modules.Chat.Domain.Tests.Common;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for ConversationCompletedEvent domain event.
/// </summary>
[TestFixture]
public class ConversationCompletedEventTests : EventTestBase
{
    [Test]
    public void Create_WithValidData_ShouldInitializeAllProperties()
    {
        // Arrange
        var conversationId = CreateConversationId();
        var messageCount = TestConstants.BusinessRules.ValidMessageCount;
        var completedAt = CurrentTime;

        // Act
        var domainEvent = new ConversationCompletedEvent(conversationId, messageCount, completedAt);

        // Assert
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.MessageCount.ShouldBe(messageCount);
        domainEvent.CompletedAt.ShouldBe(completedAt);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(10)]
    [TestCase(100)]
    [TestCase(int.MaxValue)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Create_WithVariousMessageCounts_ShouldPreserveCount(int messageCount)
    {
        // Act
        var domainEvent = new ConversationCompletedEvent(
            CreateConversationId(), messageCount, CurrentTime);

        // Assert
        domainEvent.MessageCount.ShouldBe(messageCount);
    }

    [Test]
    public void Event_ShouldHaveValueEquality()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var conversationId = CreateConversationId();
        var messageCount = 10;
        var completedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new TestableConversationCompletedEvent(eventId, conversationId, messageCount, completedAt);
        var event2 = new TestableConversationCompletedEvent(eventId, conversationId, messageCount, completedAt);

        // Assert
        event1.ShouldBe(event2);
        event1.GetHashCode().ShouldBe(event2.GetHashCode());
    }

    [Test]
    public void Events_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var messageCount = 10;
        var completedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new ConversationCompletedEvent(CreateConversationId(), messageCount, completedAt);
        var event2 = new ConversationCompletedEvent(CreateConversationId(), messageCount, completedAt);

        // Assert
        event1.ShouldNotBe(event2);
    }

    [TestCase(5, 10)]
    [TestCase(0, 1)]
    [TestCase(100, 101)]
    public void Events_WithDifferentMessageCounts_ShouldNotBeEqual(int count1, int count2)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var completedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new ConversationCompletedEvent(conversationId, count1, completedAt);
        var event2 = new ConversationCompletedEvent(conversationId, count2, completedAt);

        // Assert
        event1.ShouldNotBe(event2);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(100)]
    [TestCase(1000)]
    public void Event_ShouldSerializeAndDeserializeCorrectly(int messageCount)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var completedAt = TestConstants.DateTimes.DefaultTestTime;
        var originalEvent = new ConversationCompletedEvent(conversationId, messageCount, completedAt);

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<ConversationCompletedEvent>(json);

        // Assert
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.ShouldBe(originalEvent);
    }

    [Test]
    public void Event_ShouldRepresentConversationCompletion()
    {
        // Arrange - Business scenario: Conversation ends with final message count
        var conversationId = CreateConversationId();
        var messageCount = 15; // Total messages in completed conversation
        var completedAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var domainEvent = new ConversationCompletedEvent(conversationId, messageCount, completedAt);

        // Assert - Event captures completion metrics
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.MessageCount.ShouldBe(messageCount);
        domainEvent.CompletedAt.ShouldBe(completedAt);
    }

    [TestCase(0, "Empty conversation completed")]
    [TestCase(1, "Single message conversation")]
    [TestCase(2, "Minimal exchange (user + assistant)")]
    [TestCase(100, "Long conversation")]
    public void Event_WithDifferentScenarios_ShouldBeValid(int messageCount, string scenario)
    {
        // Act
        var domainEvent = new ConversationCompletedEvent(
            CreateConversationId(), messageCount, TestConstants.DateTimes.DefaultTestTime);

        // Assert
        domainEvent.MessageCount.ShouldBe(messageCount, scenario);
        AssertWellFormedDomainEvent(domainEvent);
    }
}