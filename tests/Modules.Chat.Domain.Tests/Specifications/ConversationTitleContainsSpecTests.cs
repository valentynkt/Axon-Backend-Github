using FluentAssertions;
using Chat.Domain.Specifications;
using Modules.Chat.Domain.Tests.Specifications._Fixtures;

namespace Modules.Chat.Domain.Tests.Specifications;

public class ConversationTitleContainsSpecTests
{
    [Fact]
    public void ToExpression_WithMatchingTerm_ShouldReturnMatches()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationTitleContainsSpec("chat");

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(4); // "My Active Chat", "Completed Chat", "Empty Chat"
        result.Should().AllSatisfy(c => 
            c.Title?.ToLower().Should().Contain("chat"));
    }

    [Fact]
    public void ToExpression_CaseInsensitiveSearch_ShouldWork()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationTitleContainsSpec("IMPORTANT");

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Contain("IMPORTANT");
    }

    [Fact]
    public void ToExpression_WithEmptyTerm_ShouldReturnAll()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationTitleContainsSpec("");

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(conversations.Count);
    }

    [Fact]
    public void ToExpression_WithNullTerm_ShouldReturnAll()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationTitleContainsSpec(null);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(conversations.Count);
    }

    [Fact]
    public void ToExpression_WithWhitespaceOnlyTerm_ShouldReturnAll()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationTitleContainsSpec("   ");

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(conversations.Count);
    }

    [Fact]
    public void ToExpression_WithNonMatchingTerm_ShouldReturnEmpty()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationTitleContainsSpec("nonexistent");

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToExpression_IgnoresNullTitles_ShouldNotThrow()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationTitleContainsSpec("test");

        // Act & Assert - should not throw even if some conversations have null titles
        var act = () => conversations.AsQueryable().Where(spec.ToExpression()).ToList();
        act.Should().NotThrow();
    }
}