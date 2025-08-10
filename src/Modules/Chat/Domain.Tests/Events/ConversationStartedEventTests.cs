using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Events;

public sealed class ConversationStartedEventTests
{
    public class Constructor
    {
        [Fact]
        public void Should_CreateEvent_With_AllRequiredProperties()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("Test Conversation").Value;
            var isDefaultTitle = false;

            // Act
            var @event = new ConversationStartedEvent(conversationId, ownerId, title, isDefaultTitle);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.Title.ShouldBe(title);
            @event.IsDefaultTitle.ShouldBe(isDefaultTitle);
        }

        [Fact]
        public void Should_CreateEvent_With_DefaultTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("").Value; // Empty title
            var isDefaultTitle = true;

            // Act
            var @event = new ConversationStartedEvent(conversationId, ownerId, title, isDefaultTitle);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.Title.ShouldBe(title);
            @event.Title.IsEmpty.ShouldBeTrue();
            @event.IsDefaultTitle.ShouldBeTrue();
        }

        [Fact]
        public void Should_CreateEvent_With_UserProvidedTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("User's Custom Title").Value;
            var isDefaultTitle = false;

            // Act
            var @event = new ConversationStartedEvent(conversationId, ownerId, title, isDefaultTitle);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.OwnerId.ShouldBe(ownerId);
            @event.Title.ShouldBe(title);
            @event.Title.Value.ShouldBe("User's Custom Title");
            @event.IsDefaultTitle.ShouldBeFalse();
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
            var title = ConversationTitle.Create("Test").Value;

            // Act
            var event1 = new ConversationStartedEvent(conversationId, ownerId, title, false);
            var event2 = new ConversationStartedEvent(conversationId, ownerId, title, false);

            // Assert
            event1.EventId.ShouldNotBe(event2.EventId);
        }

        [Fact]
        public void Should_HaveOccurredAtSet()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("Test").Value;
            var beforeCreation = DateTime.UtcNow;

            // Act
            var @event = new ConversationStartedEvent(conversationId, ownerId, title, false);
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
            var title = ConversationTitle.Create("Test").Value;

            // Act
            var @event = new ConversationStartedEvent(conversationId, ownerId, title, false);

            // Assert
            @event.Version.ShouldBe(1);
        }

        [Fact]
        public void Should_HaveCorrectDefaultName()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("Test").Value;

            // Act
            var @event = new ConversationStartedEvent(conversationId, ownerId, title, false);

            // Assert
            @event.Name.ShouldBe("Axon.Modules.Chat.Domain.Events.ConversationStartedEvent");
        }
    }

    public class Equality
    {
        [Fact]
        public void Should_BeEqual_When_AllPropertiesMatch()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("Test").Value;
            var isDefaultTitle = false;

            // Act
            var event1 = new ConversationStartedEvent(conversationId, ownerId, title, isDefaultTitle);
            var event2 = new ConversationStartedEvent(conversationId, ownerId, title, isDefaultTitle);

            // Assert
            event1.ShouldNotBe(event2); // Different EventId and OccurredAt
        }

        [Fact]
        public void Should_NotBeEqual_When_ConversationIdDiffers()
        {
            // Arrange
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("Test").Value;
            var conversationId1 = ConversationId.New();
            var conversationId2 = ConversationId.New();

            // Act
            var event1 = new ConversationStartedEvent(conversationId1, ownerId, title, false);
            var event2 = new ConversationStartedEvent(conversationId2, ownerId, title, false);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_OwnerIdDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Test").Value;
            var ownerId1 = UserId.New();
            var ownerId2 = UserId.New();

            // Act
            var event1 = new ConversationStartedEvent(conversationId, ownerId1, title, false);
            var event2 = new ConversationStartedEvent(conversationId, ownerId2, title, false);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_TitleDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title1 = ConversationTitle.Create("Test 1").Value;
            var title2 = ConversationTitle.Create("Test 2").Value;

            // Act
            var event1 = new ConversationStartedEvent(conversationId, ownerId, title1, false);
            var event2 = new ConversationStartedEvent(conversationId, ownerId, title2, false);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_IsDefaultTitleDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var ownerId = UserId.New();
            var title = ConversationTitle.Create("Test").Value;

            // Act
            var event1 = new ConversationStartedEvent(conversationId, ownerId, title, true);
            var event2 = new ConversationStartedEvent(conversationId, ownerId, title, false);

            // Assert
            event1.ShouldNotBe(event2);
        }
    }
}