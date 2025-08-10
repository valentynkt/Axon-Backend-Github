using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Events;

public sealed class UserMessageAppendedEventTests
{
    public class Constructor
    {
        [Fact]
        public void Should_CreateEvent_With_AllRequiredProperties()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 1;
            var contentPreview = "Hello, this is a test message that is exactly 100 characters long for testing purposes and more.";
            var contentLength = 250;

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.MessageId.ShouldBe(messageId);
            @event.Sequence.ShouldBe(sequence);
            @event.ContentPreview.ShouldBe(contentPreview);
            @event.ContentLength.ShouldBe(contentLength);
        }

        [Fact]
        public void Should_CreateEvent_With_FirstMessage()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 1;
            var contentPreview = "First message";
            var contentLength = 13;

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.MessageId.ShouldBe(messageId);
            @event.Sequence.ShouldBe(1);
            @event.ContentPreview.ShouldBe("First message");
            @event.ContentLength.ShouldBe(13);
        }

        [Fact]
        public void Should_CreateEvent_With_LaterSequenceNumber()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 5;
            var contentPreview = "Fifth message in conversation";
            var contentLength = 29;

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.Sequence.ShouldBe(5);
            @event.ContentPreview.ShouldBe("Fifth message in conversation");
            @event.ContentLength.ShouldBe(29);
        }

        [Fact]
        public void Should_CreateEvent_With_ExactlyTruncatedPreview()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 1;
            // Exactly 100 characters
            var contentPreview = "This is a test message that is exactly one hundred characters long for testing preview truncati";
            var contentLength = 150;

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ContentPreview.ShouldBe(contentPreview);
            @event.ContentPreview.Length.ShouldBe(100);
            @event.ContentLength.ShouldBe(150);
        }

        [Fact]
        public void Should_CreateEvent_With_ShortMessage()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();
            var sequence = 1;
            var contentPreview = "Hi";
            var contentLength = 2;

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, sequence, contentPreview, contentLength);

            // Assert
            @event.ContentPreview.ShouldBe("Hi");
            @event.ContentLength.ShouldBe(2);
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
            var event1 = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 4);
            var event2 = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 4);

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
            var @event = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 4);
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
            var @event = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 4);

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
            var @event = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 4);

            // Assert
            @event.Name.ShouldBe("Axon.Modules.Chat.Domain.Events.UserMessageAppendedEvent");
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
            var event1 = new UserMessageAppendedEvent(conversationId1, messageId, 1, "Test", 4);
            var event2 = new UserMessageAppendedEvent(conversationId2, messageId, 1, "Test", 4);

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
            var event1 = new UserMessageAppendedEvent(conversationId, messageId1, 1, "Test", 4);
            var event2 = new UserMessageAppendedEvent(conversationId, messageId2, 1, "Test", 4);

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
            var event1 = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 4);
            var event2 = new UserMessageAppendedEvent(conversationId, messageId, 2, "Test", 4);

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
            var event1 = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test 1", 6);
            var event2 = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test 2", 6);

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
            var event1 = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 4);
            var event2 = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", 5);

            // Assert
            event1.ShouldNotBe(event2);
        }
    }

    public class ValidationScenarios
    {
        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void Should_AcceptValidSequenceNumbers(int sequence)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, sequence, "Test", 4);

            // Assert
            @event.Sequence.ShouldBe(sequence);
        }

        [Theory]
        [InlineData("")]
        [InlineData("A")]
        [InlineData("This is exactly one hundred characters long message content for testing the maximum preview leng")]
        public void Should_AcceptVariousContentPreviewLengths(string contentPreview)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, 1, contentPreview, contentPreview.Length);

            // Assert
            @event.ContentPreview.ShouldBe(contentPreview);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(100000)]
        public void Should_AcceptVariousContentLengths(int contentLength)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var messageId = MessageId.New();

            // Act
            var @event = new UserMessageAppendedEvent(conversationId, messageId, 1, "Test", contentLength);

            // Assert
            @event.ContentLength.ShouldBe(contentLength);
        }
    }
}