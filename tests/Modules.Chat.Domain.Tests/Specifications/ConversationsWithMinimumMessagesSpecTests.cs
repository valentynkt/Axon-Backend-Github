using FluentAssertions;
using Chat.Domain.Specifications;
using Modules.Chat.Domain.Tests.Specifications._Fixtures;

namespace Modules.Chat.Domain.Tests.Specifications;

public class ConversationsWithMinimumMessagesSpecTests
{
    [Fact]
    public void ToExpression_WithMinCount_ShouldFilterCorrectly()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationsWithMinimumMessagesSpec(2);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(2); // Only conversations with 2+ messages
        result.Should().AllSatisfy(c => c.MessageCount.Should().BeGreaterOrEqualTo(2));
    }

    [Fact]
    public void ToExpression_WithZeroMinCount_ShouldReturnAll()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationsWithMinimumMessagesSpec(0);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(conversations.Count);
    }

    [Fact]
    public void Constructor_WithNegativeCount_ShouldClampToZero()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationsWithMinimumMessagesSpec(-5);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert - Should behave as if minCount was 0
        result.Should().HaveCount(conversations.Count);
    }

    [Fact]
    public void ToExpression_WithHighMinCount_ShouldReturnEmpty()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationsWithMinimumMessagesSpec(100);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void IsSatisfiedBy_WithConversationMeetingMinimum_ShouldReturnTrue()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var conversationWithMessages = conversations.First(c => c.MessageCount >= 3);
        var spec = new ConversationsWithMinimumMessagesSpec(3);

        // Act & Assert
        spec.IsSatisfiedBy(conversationWithMessages).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithConversationBelowMinimum_ShouldReturnFalse()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var conversationWithFewMessages = conversations.First(c => c.MessageCount < 3);
        var spec = new ConversationsWithMinimumMessagesSpec(3);

        // Act & Assert
        spec.IsSatisfiedBy(conversationWithFewMessages).Should().BeFalse();
    }
}