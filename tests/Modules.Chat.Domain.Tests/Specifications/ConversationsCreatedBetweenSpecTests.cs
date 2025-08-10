using FluentAssertions;
using Chat.Domain.Specifications;
using Modules.Chat.Domain.Tests.Specifications._Fixtures;

namespace Modules.Chat.Domain.Tests.Specifications;

public class ConversationsCreatedBetweenSpecTests
{
    [Fact]
    public void ToExpression_WithValidRange_ShouldIncludeBothBoundaries()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var from = new DateTimeOffset(2023, 12, 25, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var spec = new ConversationsCreatedBetweenSpec(from, to);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(3); // All test conversations fall within this range
        result.Should().AllSatisfy(c => 
        {
            c.CreatedAtUtc.Should().BeOnOrAfter(from);
            c.CreatedAtUtc.Should().BeOnOrBefore(to);
        });
    }

    [Fact]
    public void ToExpression_WithNarrowRange_ShouldFilterCorrectly()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var from = new DateTimeOffset(2023, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2023, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var spec = new ConversationsCreatedBetweenSpec(from, to);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(1); // Only the recent conversation (created 1 day ago from base time)
    }

    [Fact]
    public void Constructor_WithSwappedDates_ShouldSwapThem()
    {
        // Arrange
        var from = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();

        // Act
        var spec = new ConversationsCreatedBetweenSpec(from, to); // from > to
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert - should work as if we passed (to, from)
        result.Should().HaveCount(3); // All conversations should match the corrected range
    }

    [Fact]
    public void IsSatisfiedBy_WithConversationInRange_ShouldReturnTrue()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var conversation = conversations.First();
        var spec = new ConversationsCreatedBetweenSpec(
            conversation.CreatedAtUtc.AddDays(-1), 
            conversation.CreatedAtUtc.AddDays(1));

        // Act & Assert
        spec.IsSatisfiedBy(conversation).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithConversationOutsideRange_ShouldReturnFalse()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var conversation = conversations.First();
        var spec = new ConversationsCreatedBetweenSpec(
            conversation.CreatedAtUtc.AddDays(1), 
            conversation.CreatedAtUtc.AddDays(2));

        // Act & Assert
        spec.IsSatisfiedBy(conversation).Should().BeFalse();
    }
}