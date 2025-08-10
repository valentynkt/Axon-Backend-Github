using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Events;

public sealed class AssistantMessageAppendedEventTests
{
    public class Constructor
    {
        [Fact]
        public void Should_CreateEvent_With_AllRequiredProperties()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 2;
            var contentPreview = "I understand your request. Here's my response which is exactly 100 characters long for testing.";
            var contentLength = 500;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.MessageId.ShouldBe(messageId);
            @event.Sequence.ShouldBe(sequence);
            @event.ContentPreview.ShouldBe(contentPreview);
            @event.ContentLength.ShouldBe(contentLength);
        }

        [Fact]
        public void Should_CreateEvent_With_ResponseMessage()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 2;
            var contentPreview = "Thank you for your question. Let me help you with that.";
            var contentLength = 56;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.MessageId.ShouldBe(messageId);
            @event.Sequence.ShouldBe(2);
            @event.ContentPreview.ShouldBe("Thank you for your question. Let me help you with that.");
            @event.ContentLength.ShouldBe(56);
        }

        [Fact]
        public void Should_CreateEvent_With_LongAssistantResponse()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 4;
            // Exactly 100 characters
            var contentPreview = "Here's a detailed explanation of the concept you asked about. This response contains multiple";
            var contentLength = 2500;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.Sequence.ShouldBe(4);
            @event.ContentPreview.ShouldBe(contentPreview);
            @event.ContentPreview.Length.ShouldBe(100);
            @event.ContentLength.ShouldBe(2500);
        }

        [Fact]
        public void Should_CreateEvent_With_CodeResponse()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 6;
            var contentPreview = "Here's the C# code you requested:\n\npublic class Example\n{\n    public void Method() { }\n}";
            var contentLength = 85;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ContentPreview.ShouldBe("Here's the C# code you requested:\n\npublic class Example\n{\n    public void Method() { }\n}");
            @event.ContentLength.ShouldBe(85);
        }

        [Fact]
        public void Should_CreateEvent_With_ShortAssistantMessage()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 2;
            var contentPreview = "Yes.";
            var contentLength = 4;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ContentPreview.ShouldBe("Yes.");
            @event.ContentLength.ShouldBe(4);
        }
    }

    public class DomainEventProperties
    {
        [Fact]
        public void Should_HaveUniqueEventId()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var event1 = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 8);
            var event2 = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 8);

            // Assert
            event1.EventId.ShouldNotBe(event2.EventId);
        }

        [Fact]
        public void Should_HaveOccurredAtSet()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var beforeCreation = DateTime.UtcNow;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 8);
            var afterCreation = DateTime.UtcNow;

            // Assert
            @event.OccurredAt.ShouldBeGreaterThan(beforeCreation.AddMilliseconds(-1));
            @event.OccurredAt.ShouldBeLessThan(afterCreation.AddMilliseconds(1));
        }

        [Fact]
        public void Should_HaveDefaultVersionOf1()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 8);

            // Assert
            @event.Version.ShouldBe(1);
        }

        [Fact]
        public void Should_HaveCorrectDefaultName()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 8);

            // Assert
            @event.Name.ShouldBe("Axon.Modules.Chat.Domain.Events.AssistantMessageAppendedEvent");
        }
    }

    public class Equality
    {
        [Fact]
        public void Should_NotBeEqual_When_ConversationIdDiffers()
        {
            // Arrange
            var messageId = MessageId.New();
            var conversationId1 = ConversationId.New();
            var conversationId2 = ConversationId.New();

            // Act
            var event1 = new AssistantMessageAppendedEvent(conversationId1, messageId, 2, "Response", 8);
            var event2 = new AssistantMessageAppendedEvent(conversationId2, messageId, 2, "Response", 8);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_MessageIdDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId1 = MessageId.New();
            var messageId2 = MessageId.New();

            // Act
            var event1 = new AssistantMessageAppendedEvent(conversationId, messageId1, 2, "Response", 8);
            var event2 = new AssistantMessageAppendedEvent(conversationId, messageId2, 2, "Response", 8);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_SequenceDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var event1 = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 8);
            var event2 = new AssistantMessageAppendedEvent(conversationId, messageId, 4, "Response", 8);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_ContentPreviewDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var event1 = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response A", 10);
            var event2 = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response B", 10);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_ContentLengthDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var event1 = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 8);
            var event2 = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", 10);

            // Assert
            event1.ShouldNotBe(event2);
        }
    }

    public class ValidationScenarios
    {
        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(6)]
        [InlineData(100)]
        [InlineData(1000)]
        public void Should_AcceptValidEvenSequenceNumbers(int sequence)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, "Response", 8);

            // Assert
            @event.Sequence.ShouldBe(sequence);
        }

        [Theory]
        [InlineData("")]
        [InlineData("OK")]
        [InlineData("This is a comprehensive response that explains the topic in detail and provides exactly 100 c")]
        public void Should_AcceptVariousContentPreviewLengths(string contentPreview)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, 2, contentPreview, contentPreview.Length);

            // Assert
            @event.ContentPreview.ShouldBe(contentPreview);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(1000)]
        [InlineData(5000)]
        [InlineData(100000)]
        public void Should_AcceptVariousContentLengths(int contentLength)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, 2, "Response", contentLength);

            // Assert
            @event.ContentLength.ShouldBe(contentLength);
        }

        [Fact]
        public void Should_HandleMultilineContent()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 2;
            var contentPreview = "Here's a multiline response:\n\n1. First point\n2. Second point\n3. Third point";
            var contentLength = contentPreview.Length;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ContentPreview.ShouldBe(contentPreview);
            @event.ContentLength.ShouldBe(contentLength);
        }

        [Fact]
        public void Should_HandleSpecialCharacters()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 2;
            var contentPreview = "Special chars: @#$%^&*()_+-={}[]|\\:;\"'<>,.?/~`";
            var contentLength = contentPreview.Length;

            // Act
            var @event = new AssistantMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ContentPreview.ShouldBe(contentPreview);
            @event.ContentLength.ShouldBe(contentLength);
        }
    }
}