using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Events;

public sealed class ConversationTitleUpdatedEventTests
{
    public class Constructor
    {
        [Fact]
        public void Should_CreateEvent_With_AllRequiredProperties()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Updated Conversation Title").Value;
            var isDefaultTitle = false;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, isDefaultTitle);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.Title.ShouldBe(title);
            @event.IsDefaultTitle.ShouldBe(isDefaultTitle);
        }

        [Fact]
        public void Should_CreateEvent_With_UserProvidedTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("My Custom Title").Value;
            var isDefaultTitle = false;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, isDefaultTitle);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.Title.ShouldBe(title);
            @event.Title.Value.ShouldBe("My Custom Title");
            @event.IsDefaultTitle.ShouldBeFalse();
        }

        [Fact]
        public void Should_CreateEvent_With_DefaultTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Auto-generated conversation title").Value;
            var isDefaultTitle = true;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, isDefaultTitle);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.Title.ShouldBe(title);
            @event.Title.Value.ShouldBe("Auto-generated conversation title");
            @event.IsDefaultTitle.ShouldBeTrue();
        }

        [Fact]
        public void Should_CreateEvent_With_EmptyTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("").Value;
            var isDefaultTitle = true;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, isDefaultTitle);

            // Assert
            @event.ConversationId.ShouldBe(conversationId);
            @event.Title.ShouldBe(title);
            @event.Title.IsEmpty.ShouldBeTrue();
            @event.IsDefaultTitle.ShouldBeTrue();
        }

        [Fact]
        public void Should_CreateEvent_With_MaxLengthTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var longTitle = new string('A', 200); // Exactly 200 characters
            var title = ConversationTitle.Create(longTitle).Value;
            var isDefaultTitle = false;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, isDefaultTitle);

            // Assert
            @event.Title.Value.ShouldBe(longTitle);
            @event.Title.Length.ShouldBe(200);
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
            var title = ConversationTitle.Create("Test Title").Value;

            // Act
            var event1 = new ConversationTitleUpdatedEvent(conversationId, title, false);
            var event2 = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            event1.EventId.ShouldNotBe(event2.EventId);
        }

        [Fact]
        public void Should_HaveOccurredAtSet()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Test Title").Value;
            var beforeCreation = DateTime.UtcNow;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);
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
            var title = ConversationTitle.Create("Test Title").Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            @event.Version.ShouldBe(1);
        }

        [Fact]
        public void Should_HaveCorrectDefaultName()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Test Title").Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            @event.Name.ShouldBe("Axon.Modules.Chat.Domain.Events.ConversationTitleUpdatedEvent");
        }
    }

    public class Equality
    {
        [Fact]
        public void Should_NotBeEqual_When_ConversationIdDiffers()
        {
            // Arrange
            var title = ConversationTitle.Create("Test Title").Value;
            var conversationId1 = ConversationId.New();
            var conversationId2 = ConversationId.New();

            // Act
            var event1 = new ConversationTitleUpdatedEvent(conversationId1, title, false);
            var event2 = new ConversationTitleUpdatedEvent(conversationId2, title, false);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_TitleDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title1 = ConversationTitle.Create("Title 1").Value;
            var title2 = ConversationTitle.Create("Title 2").Value;

            // Act
            var event1 = new ConversationTitleUpdatedEvent(conversationId, title1, false);
            var event2 = new ConversationTitleUpdatedEvent(conversationId, title2, false);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_IsDefaultTitleDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Test Title").Value;

            // Act
            var event1 = new ConversationTitleUpdatedEvent(conversationId, title, true);
            var event2 = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            event1.ShouldNotBe(event2);
        }

        [Fact]
        public void Should_NotBeEqual_When_EventIdDiffers()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Test Title").Value;

            // Act
            var event1 = new ConversationTitleUpdatedEvent(conversationId, title, false);
            var event2 = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            event1.ShouldNotBe(event2); // Different EventId and OccurredAt
        }
    }

    public class ValidationScenarios
    {
        [Theory]
        [InlineData("Short")]
        [InlineData("Medium length conversation title")]
        [InlineData("This is a very long conversation title that contains multiple words and should be within the 200 character limit for conversation titles in the chat domain model structure")]
        public void Should_AcceptValidTitles(string titleText)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create(titleText).Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            @event.Title.Value.ShouldBe(titleText);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Should_AcceptBothDefaultTitleFlags(bool isDefaultTitle)
        {
            // Arrange
            var conversationId = ConversationId.New();
            var title = ConversationTitle.Create("Test Title").Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, isDefaultTitle);

            // Assert
            @event.IsDefaultTitle.ShouldBe(isDefaultTitle);
        }

        [Fact]
        public void Should_HandleSpecialCharactersInTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var titleWithSpecialChars = "Title with: @#$%^&*()_+-={}[]|\\:;\"'<>,.?/~`";
            var title = ConversationTitle.Create(titleWithSpecialChars).Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            @event.Title.Value.ShouldBe(titleWithSpecialChars);
        }

        [Fact]
        public void Should_HandleUnicodeCharactersInTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var unicodeTitle = "Conversation avec émojis 🚀🎉 and Chinese characters 中文";
            var title = ConversationTitle.Create(unicodeTitle).Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            @event.Title.Value.ShouldBe(unicodeTitle);
        }

        [Fact]
        public void Should_HandleWhitespaceInTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var titleWithWhitespace = "Title    with    multiple    spaces";
            var title = ConversationTitle.Create(titleWithWhitespace).Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            @event.Title.Value.ShouldBe(titleWithWhitespace);
        }

        [Fact]
        public void Should_HandleTrimmedTitle()
        {
            // Arrange
            var conversationId = ConversationId.New();
            var originalTitle = "   Trimmed Title   ";
            var title = ConversationTitle.Create(originalTitle).Value;

            // Act
            var @event = new ConversationTitleUpdatedEvent(conversationId, title, false);

            // Assert
            @event.Title.Value.ShouldBe("Trimmed Title"); // Should be trimmed
        }
    }
}