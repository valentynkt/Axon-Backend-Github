using FluentAssertions;
using Chat.Domain.Aggregates.Conversation;
using Chat.Domain.Specifications;
using Modules.Chat.Domain.Tests.Specifications._Fixtures;

namespace Modules.Chat.Domain.Tests.Specifications;

public class ChatConversationSpecsFactoryTests
{
    [Fact]
    public void ActiveByOwner_ShouldCombineOwnerAndActiveStatus()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = ChatConversationSpecs.ActiveByOwner(ConversationTestFixture.Owner1);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(2); // Owner1 has 3 total, 1 completed, so 2 active
        result.Should().AllSatisfy(c => 
        {
            c.OwnerId.Should().Be(ConversationTestFixture.Owner1);
            c.Status.Should().Be(ConversationStatus.Active);
        });
    }

    [Fact]
    public void RecentlyUpdatedByOwner_ShouldCombineActiveByOwnerAndUpdatedSince()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var sinceTime = new DateTimeOffset(2023, 12, 31, 12, 0, 0, TimeSpan.Zero);
        var spec = ChatConversationSpecs.RecentlyUpdatedByOwner(ConversationTestFixture.Owner1, sinceTime);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().AllSatisfy(c => 
        {
            c.OwnerId.Should().Be(ConversationTestFixture.Owner1);
            c.Status.Should().Be(ConversationStatus.Active);
            c.UpdatedAtUtc.Should().BeOnOrAfter(sinceTime);
        });
    }

    [Fact]
    public void CreatedInRangeByOwner_ShouldCombineOwnerAndCreatedBetween()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var from = new DateTimeOffset(2023, 12, 25, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var spec = ChatConversationSpecs.CreatedInRangeByOwner(ConversationTestFixture.Owner1, from, to);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().AllSatisfy(c => 
        {
            c.OwnerId.Should().Be(ConversationTestFixture.Owner1);
            c.CreatedAtUtc.Should().BeOnOrAfter(from);
            c.CreatedAtUtc.Should().BeOnOrBefore(to);
        });
    }

    [Fact]
    public void Search_WithAllParametersNull_ShouldReturnOnlyByOwner()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = ChatConversationSpecs.Search(ConversationTestFixture.Owner1);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(c => c.OwnerId.Should().Be(ConversationTestFixture.Owner1));
    }

    [Fact]
    public void Search_WithTitleTerm_ShouldIncludeTitleFilter()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = ChatConversationSpecs.Search(ConversationTestFixture.Owner1, term: "Active");

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Contain("Active");
    }

    [Fact]
    public void Search_WithStatus_ShouldIncludeStatusFilter()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = ChatConversationSpecs.Search(ConversationTestFixture.Owner1, status: ConversationStatus.Completed);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().AllSatisfy(c => c.Status.Should().Be(ConversationStatus.Completed));
    }

    [Fact]
    public void Search_WithDateRange_ShouldIncludeCreatedBetweenFilter()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTimeBasedConversations();
        var from = new DateTimeOffset(2023, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var spec = ChatConversationSpecs.Search(ConversationTestFixture.Owner1, from: from, to: to);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().AllSatisfy(c => 
        {
            c.CreatedAtUtc.Should().BeOnOrAfter(from);
            c.CreatedAtUtc.Should().BeOnOrBefore(to);
        });
    }

    [Fact]
    public void Search_WithMinMessages_ShouldIncludeMinimumMessagesFilter()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = ChatConversationSpecs.Search(ConversationTestFixture.Owner1, minMessages: 3);

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Should().AllSatisfy(c => c.MessageCount.Should().BeGreaterOrEqualTo(3));
    }
}