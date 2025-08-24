using System.Text.Json;
using Axon.Modules.Chat.Domain.Tests.Common;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for UserMessageAppendedEvent domain event.
/// </summary>
[TestFixture]
public class UserMessageAppendedEventTests : EventTestBase
{
    [Test]
    public void Create_WithValidData_ShouldInitializeAllProperties()
    {
        // Arrange
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var sequence = 1;
        var contentPreview = TestConstants.Messages.DefaultUserMessage;
        var createdAt = CurrentTime;

        // Act
        var domainEvent = new UserMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, createdAt);

        // Assert
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.MessageId.ShouldBe(messageId);
        domainEvent.Sequence.ShouldBe(sequence);
        domainEvent.ContentPreview.ShouldBe(contentPreview);
        domainEvent.CreatedAt.ShouldBe(createdAt);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Regular message content")]
    [TestCase("Message with special chars: !@#$%^&*()")]
    [TestCase("Unicode message: 软件架构 🏗️ ñáéíóú")]
    public void Create_WithVariousContentPreviews_ShouldPreserveContent(string? contentPreview)
    {
        // Act
        var domainEvent = new UserMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), 1, contentPreview!, CurrentTime);

        // Assert
        domainEvent.ContentPreview.ShouldBe(contentPreview);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(10)]
    [TestCase(100)]
    [TestCase(int.MaxValue)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Create_WithVariousSequenceNumbers_ShouldPreserveSequence(int sequence)
    {
        // Act
        var domainEvent = new UserMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), sequence, "Test message", CurrentTime);

        // Assert
        domainEvent.Sequence.ShouldBe(sequence);
    }

    [Test]
    public void Event_ShouldHaveValueEquality()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var sequence = 1;
        var contentPreview = TestConstants.Messages.DefaultUserMessage;
        var createdAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new TestableUserMessageAppendedEvent(eventId, conversationId, messageId, sequence, contentPreview, createdAt);
        var event2 = new TestableUserMessageAppendedEvent(eventId, conversationId, messageId, sequence, contentPreview, createdAt);

        // Assert
        event1.ShouldBe(event2);
        event1.GetHashCode().ShouldBe(event2.GetHashCode());
    }

    [Test]
    public void Events_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var sequence = 1;
        var contentPreview = TestConstants.Messages.DefaultUserMessage;
        var createdAt = TestConstants.DateTimes.DefaultTestTime;

        // Act & Assert different conversation IDs  
        var event1 = new UserMessageAppendedEvent(CreateConversationId(), CreateMessageId(), sequence, contentPreview, createdAt);
        var event2 = new UserMessageAppendedEvent(CreateConversationId(), CreateMessageId(), sequence, contentPreview, createdAt);
        
        event1.ShouldNotBe(event2);
    }

    [TestCase("Content1", "Content2")]
    [TestCase(1, 2)]
    public void Events_WithDifferentProperties_ShouldNotBeEqual(object prop1, object prop2)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var createdAt = TestConstants.DateTimes.DefaultTestTime;

        // Act & Assert for different content or sequence
        if (prop1 is string content1 && prop2 is string content2)
        {
            var event1 = new UserMessageAppendedEvent(conversationId, messageId, 1, content1, createdAt);
            var event2 = new UserMessageAppendedEvent(conversationId, messageId, 1, content2, createdAt);
            event1.ShouldNotBe(event2);
        }
        else if (prop1 is int seq1 && prop2 is int seq2)
        {
            var event1 = new UserMessageAppendedEvent(conversationId, messageId, seq1, "Test", createdAt);
            var event2 = new UserMessageAppendedEvent(conversationId, messageId, seq2, "Test", createdAt);
            event1.ShouldNotBe(event2);
        }
    }

    [TestCase("")]
    [TestCase("Regular message")]
    [TestCase("Special chars: !@#$%^&*()[]{}|\\:;\"'<>,.?/")]
    [TestCase("Unicode: 软件架构模式 🏗️ ñáéíóú")]
    public void Event_ShouldSerializeAndDeserializeCorrectly(string contentPreview)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var sequence = 42;
        var createdAt = TestConstants.DateTimes.DefaultTestTime;
        var originalEvent = new UserMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, createdAt);

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<UserMessageAppendedEvent>(json);

        // Assert
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.ShouldBe(originalEvent);
    }

    [Test]
    public void Event_WithLongContentPreview_ShouldHandleCorrectly()
    {
        // Arrange
        var longContent = TestConstants.EdgeCases.ExactMaxMessage;

        // Act
        var domainEvent = new UserMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), 1, longContent, CurrentTime);

        // Assert
        domainEvent.ContentPreview.ShouldBe(longContent);
        domainEvent.ContentPreview.Length.ShouldBe(TestConstants.Limits.MaxMessageContentLength);
    }
}