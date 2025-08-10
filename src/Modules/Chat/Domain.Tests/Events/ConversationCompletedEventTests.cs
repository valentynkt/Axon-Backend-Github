using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Events;

public sealed class ConversationCompletedEventTests
{
    public class Constructor
    {
        [Fact]
        public void Should_CreateEvent_With_AllRequiredProperties()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 5;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(messageCount);
        }

        [Fact]
        public void Should_CreateEvent_With_MinimumMessageCount()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 1;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(1);
        }

        [Fact]
        public void Should_CreateEvent_With_TypicalMessageCount()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 20;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(20);
        }

        [Fact]
        public void Should_CreateEvent_With_LargeMessageCount()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 1000;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(1000);
        }

        [Fact]
        public void Should_CreateEvent_With_MaximumMessageCount()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 10000; // According to domain rules, max is 10k messages

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(10000);
        }
    }

    public class DomainEventProperties
    {
        [Fact]
        public void Should_HaveUniqueEventId()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();

            // Act
            var event1 = new ConversationCompletedEvent(conversationId, ownerId, 5);
            var event2 = new ConversationCompletedEvent(conversationId, ownerId, 5);

            // Assert
            event1.EventId.ShouldNotBe(event2.EventId);
        }

        [Fact]
        public void Should_HaveOccurredAtSet()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var beforeCreation = DateTime.UtcNow;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, 5);
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
            var ownerId = UserId.New();

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, 5);

            // Assert
            @event.Version.ShouldBe(1);
        }

        [Fact]
        public void Should_HaveCorrectDefaultName()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, 5);

            // Assert
            @event.Name.ShouldBe("Axon.Modules.Chat.Domain.Events.ConversationCompletedEvent");
        }
    }

    public class Equality
    {
        [Fact]
        public void Should_NotBeEqual_When_ConversationIdDiffers()
        {
            // Arrange
            var ownerId = UserId.New();
            var conversationId1 = ConversationId.New();
            var conversationId2 = ConversationId.New();

            // Act
            var event1 = new ConversationCompletedEvent(conversationId1, ownerId, 5);
            var event2 = new ConversationCompletedEvent(conversationId2, ownerId, 5);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_OwnerIdDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId1 = UserId.New();
            var ownerId2 = UserId.New();

            // Act
            var event1 = new ConversationCompletedEvent(conversationId, ownerId1, 5);
            var event2 = new ConversationCompletedEvent(conversationId, ownerId2, 5);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_MessageCountDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();

            // Act
            var event1 = new ConversationCompletedEvent(conversationId, ownerId, 5);
            var event2 = new ConversationCompletedEvent(conversationId, ownerId, 10);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_EventIdDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();

            // Act
            var event1 = new ConversationCompletedEvent(conversationId, ownerId, 5);
            var event2 = new ConversationCompletedEvent(conversationId, ownerId, 5);

            // Assert
            event1.ShouldNotBe(event2); // Different EventId and OccurredAt
        }
    }

    public class ValidationScenarios
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        [InlineData(5000)]
        [InlineData(10000)]
        public void Should_AcceptValidMessageCounts(int messageCount)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.MessageCount.ShouldBe(messageCount);
        }

        [Fact]
        public void Should_AcceptZeroMessageCount()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 0;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.MessageCount.ShouldBe(0);
        }

        [Fact]
        public void Should_HandleConversationWithSingleMessage()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 1;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(1);
        }

        [Fact]
        public void Should_HandleConversationWithEvenMessageCount()
        {
            // Arrange - Even message count (ends with assistant message)
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 6; // User(1), Assistant(2), User(3), Assistant(4), User(5), Assistant(6)

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.MessageCount.ShouldBe(6);
        }

        [Fact]
        public void Should_HandleConversationWithOddMessageCount()
        {
            // Arrange - Odd message count (ends with user message)
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 7; // User(1), Assistant(2), User(3), Assistant(4), User(5), Assistant(6), User(7)

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.MessageCount.ShouldBe(7);
        }

        [Fact]
        public void Should_HandleLongRunningConversation()
        {
            // Arrange - Very long conversation approaching the limit
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 9999;

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.MessageCount.ShouldBe(9999);
        }
    }

    public class BusinessLogicScenarios
    {
        [Fact]
        public void Should_RepresentConversationCompletion_For_ShortConversation()
        {
            // Arrange - Quick conversation between user and assistant
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 2; // User question, Assistant answer

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(2);
            @event.OccurredAt.ShouldBeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Should_RepresentConversationCompletion_For_LongConversation()
        {
            // Arrange - Extended conversation with many back-and-forth exchanges
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var messageCount = 42; // Detailed discussion

            // Act
            var @event = new ConversationCompletedEvent(conversationId, ownerId, messageCount);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.MessageCount.ShouldBe(42);
            @event.OccurredAt.ShouldBeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }
    }
}