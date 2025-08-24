using System.Text.Json;
using Axon.Modules.Chat.Domain.Tests.Common;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests for AssistantMessageAppendedEvent domain event.
/// </summary>
[TestFixture]
public class AssistantMessageAppendedEventTests : EventTestBase
{
    [Test]
    public void Create_WithValidData_ShouldInitializeAllProperties()
    {
        // Arrange
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var sequence = 2;
        var contentPreview = TestConstants.Messages.DefaultAssistantMessage;
        var aiResponseId = CreateAiResponseId();
        var createdAt = CurrentTime;

        // Act
        var domainEvent = new AssistantMessageAppendedEvent(
            conversationId, messageId, sequence, contentPreview, aiResponseId, createdAt);

        // Assert
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.MessageId.ShouldBe(messageId);
        domainEvent.Sequence.ShouldBe(sequence);
        domainEvent.ContentPreview.ShouldBe(contentPreview);
        domainEvent.AiResponseId.ShouldBe(aiResponseId);
        domainEvent.CreatedAt.ShouldBe(createdAt);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Regular assistant response")]
    [TestCase("Response with special chars: !@#$%^&*()")]
    [TestCase("Unicode response: 软件架构 🏗️ ñáéíóú")]
    public void Create_WithVariousContentPreviews_ShouldPreserveContent(string? contentPreview)
    {
        // Act
        var domainEvent = new AssistantMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), 1, contentPreview!, CreateAiResponseId(), CurrentTime);

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
        var domainEvent = new AssistantMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), sequence, "Test", CreateAiResponseId(), CurrentTime);

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
        var contentPreview = TestConstants.Messages.DefaultAssistantMessage;
        var aiResponseId = CreateAiResponseId();
        var createdAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var event1 = new TestableAssistantMessageAppendedEvent(
            eventId, conversationId, messageId, sequence, contentPreview, aiResponseId, createdAt);
        var event2 = new TestableAssistantMessageAppendedEvent(
            eventId, conversationId, messageId, sequence, contentPreview, aiResponseId, createdAt);

        // Assert
        event1.ShouldBe(event2);
        event1.GetHashCode().ShouldBe(event2.GetHashCode());
    }

    [Test]
    public void Events_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var sequence = 1;
        var contentPreview = TestConstants.Messages.DefaultAssistantMessage;
        var createdAt = TestConstants.DateTimes.DefaultTestTime;

        // Act & Assert different conversation IDs
        var event1 = new AssistantMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), sequence, contentPreview, CreateAiResponseId(), createdAt);
        var event2 = new AssistantMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), sequence, contentPreview, CreateAiResponseId(), createdAt);
        
        event1.ShouldNotBe(event2);
    }

    [TestCase("Content1", "Content2")]
    [TestCase(1, 2)]
    public void Events_WithDifferentProperties_ShouldNotBeEqual(object prop1, object prop2)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var aiResponseId = CreateAiResponseId();
        var createdAt = TestConstants.DateTimes.DefaultTestTime;

        // Act & Assert for different content or sequence
        if (prop1 is string content1 && prop2 is string content2)
        {
            var event1 = new AssistantMessageAppendedEvent(conversationId, messageId, 1, content1, aiResponseId, createdAt);
            var event2 = new AssistantMessageAppendedEvent(conversationId, messageId, 1, content2, aiResponseId, createdAt);
            event1.ShouldNotBe(event2);
        }
        else if (prop1 is int seq1 && prop2 is int seq2)
        {
            var event1 = new AssistantMessageAppendedEvent(conversationId, messageId, seq1, "Test", aiResponseId, createdAt);
            var event2 = new AssistantMessageAppendedEvent(conversationId, messageId, seq2, "Test", aiResponseId, createdAt);
            event1.ShouldNotBe(event2);
        }
    }

    [TestCase("")]
    [TestCase("Regular response")]
    [TestCase("Special chars: !@#$%^&*()[]{}|\\:;\"'<>,.?/")]
    [TestCase("Unicode: 软件架构模式 🏗️ ñáéíóú")]
    public void Event_ShouldSerializeAndDeserializeCorrectly(string contentPreview)
    {
        // Arrange
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var sequence = 42;
        var aiResponseId = CreateAiResponseId();
        var createdAt = TestConstants.DateTimes.DefaultTestTime;
        var originalEvent = new AssistantMessageAppendedEvent(
            conversationId, messageId, sequence, contentPreview, aiResponseId, createdAt);

        // Act
        var json = JsonSerializer.Serialize(originalEvent);
        var deserializedEvent = JsonSerializer.Deserialize<AssistantMessageAppendedEvent>(json);

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
        var domainEvent = new AssistantMessageAppendedEvent(
            CreateConversationId(), CreateMessageId(), 1, longContent, CreateAiResponseId(), CurrentTime);

        // Assert
        domainEvent.ContentPreview.ShouldBe(longContent);
        domainEvent.ContentPreview.Length.ShouldBe(TestConstants.Limits.MaxMessageContentLength);
    }

    [Test]
    public void Event_ShouldRepresentAssistantResponse()
    {
        // Arrange - Business scenario: AI assistant responds to user query
        var conversationId = CreateConversationId();
        var messageId = CreateMessageId();
        var sequence = 2; // Assistant response follows user message
        var contentPreview = "Clean Architecture separates concerns into layers...";
        var aiResponseId = CreateAiResponseId();
        var createdAt = TestConstants.DateTimes.DefaultTestTime;

        // Act
        var domainEvent = new AssistantMessageAppendedEvent(
            conversationId, messageId, sequence, contentPreview, aiResponseId, createdAt);

        // Assert - Event captures essential AI response information
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.MessageId.ShouldBe(messageId);
        domainEvent.Sequence.ShouldBe(sequence);
        domainEvent.ContentPreview.ShouldBe(contentPreview);
        domainEvent.AiResponseId.ShouldBe(aiResponseId); // Critical for AI tracking
        domainEvent.CreatedAt.ShouldBe(createdAt);
    }
}