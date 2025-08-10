using FluentAssertions;
using Chat.Domain.Specifications;
using Modules.Chat.Domain.Tests.Specifications._Fixtures;

namespace Modules.Chat.Domain.Tests.Specifications;

public class ConversationsUpdatedSinceSpecTests
{
    [Fact]
    public void ToExpression_WithSinceTime_ShouldIncludeBoundary()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var conversation = conversations.First();
        var since = conversation.UpdatedAtUtc; // Exact boundary
        var spec = new ConversationsUpdatedSinceSpec(since);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().Contain(conversation);
        result.Should().AllSatisfy(c => c.UpdatedAtUtc.Should().BeOnOrAfter(since));
    }

    [Fact]
    public void ToExpression_WithFutureTime_ShouldReturnEmpty()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var futureTime = DateTimeOffset.UtcNow.AddDays(10);
        var spec = new ConversationsUpdatedSinceSpec(futureTime);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToExpression_WithPastTime_ShouldReturnAll()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var pastTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var spec = new ConversationsUpdatedSinceSpec(pastTime);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(conversations.Count);
    }

    [Fact]
    public void IsSatisfiedBy_WithConversationUpdatedAfterSince_ShouldReturnTrue()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var conversation = conversations.First();
        var since = conversation.UpdatedAtUtc.AddMinutes(-1);
        var spec = new ConversationsUpdatedSinceSpec(since);

        // Act & Assert
        spec.IsSatisfiedBy(conversation).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithConversationUpdatedBeforeSince_ShouldReturnFalse()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var conversation = conversations.First();
        var since = conversation.UpdatedAtUtc.AddMinutes(1);
        var spec = new ConversationsUpdatedSinceSpec(since);

        // Act & Assert
        spec.IsSatisfiedBy(conversation).Should().BeFalse();
    }
}