using FluentAssertions;
using Chat.Domain.Aggregates.Conversation;
using Chat.Domain.Specifications;
using Modules.Chat.Domain.Tests.Specifications._Fixtures;

namespace Modules.Chat.Domain.Tests.Specifications;

public class ConversationsByStatusSpecTests
{
    [Fact]
    public void ToExpression_WithActiveStatus_ShouldReturnOnlyActiveConversations()
    {
        // Arrange
        var spec = new ConversationsByStatusSpec(ConversationStatus.Active);
        var conversations = ConversationTestFixture.CreateTestConversations();

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(4); // 3 for Owner1 + 2 for Owner2, minus 1 completed = 4 active
        result.Should().AllSatisfy(c => c.Status.Should().Be(ConversationStatus.Active));
    }

    [Fact]
    public void ToExpression_WithCompletedStatus_ShouldReturnOnlyCompletedConversations()
    {
        // Arrange
        var spec = new ConversationsByStatusSpec(ConversationStatus.Completed);
        var conversations = ConversationTestFixture.CreateTestConversations();

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().AllSatisfy(c => c.Status.Should().Be(ConversationStatus.Completed));
    }

    [Fact]
    public void IsSatisfiedBy_WithMatchingStatus_ShouldReturnTrue()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var activeConversation = conversations.First(c => c.Status == ConversationStatus.Active);
        var spec = new ConversationsByStatusSpec(ConversationStatus.Active);

        // Act & Assert
        spec.IsSatisfiedBy(activeConversation).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithDifferentStatus_ShouldReturnFalse()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var activeConversation = conversations.First(c => c.Status == ConversationStatus.Active);
        var spec = new ConversationsByStatusSpec(ConversationStatus.Completed);

        // Act & Assert
        spec.IsSatisfiedBy(activeConversation).Should().BeFalse();
    }
}