using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Specifications;
using Axon.Modules.Chat.Infrastructure.Persistence.Builders;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Comprehensive tests for ConversationReadRepository read operations.
/// Covers compiled query performance, specification-based queries, and complex filtering scenarios.
/// Focuses on high-value read operations following the 80/20 rule.
/// </summary>
[TestFixture]
public sealed class ConversationReadRepositoryTests : ChatPersistenceTestBase
{
    #region Compiled Query Tests (High Priority - Hot Path Operations)

    [Test]
    public async Task GetConversationsForOwnerOptimizedAsync_WithValidOwner_ShouldReturnConversations()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        var conversations = await SetupMultipleConversationsForOwner(ownerId);

        // Act
        var result = await ConversationReadRepository.GetConversationsForOwnerOptimizedAsync(
            ownerId, skip: 0, take: 10);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2); // Only active conversations

        // Should be ordered by UpdatedAt descending
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].UpdatedAtUtc.ShouldBeGreaterThanOrEqualTo(result[i + 1].UpdatedAtUtc);
        }

        // Verify structure of ConversationListItem
        result.ShouldAllBe(item => item.ConversationId != Guid.Empty);
        result.ShouldAllBe(item => item.CreatedAtUtc != default);
        result.ShouldAllBe(item => item.UpdatedAtUtc != default);
    }

    [Test]
    public async Task GetConversationsForOwnerOptimizedAsync_WithPagination_ShouldRespectSkipAndTake()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        await SetupMultipleConversationsForPagination(ownerId, 5);

        // Act - First page
        var firstPage = await ConversationReadRepository.GetConversationsForOwnerOptimizedAsync(
            ownerId, skip: 0, take: 2);

        // Act - Second page
        var secondPage = await ConversationReadRepository.GetConversationsForOwnerOptimizedAsync(
            ownerId, skip: 2, take: 2);

        // Assert
        firstPage.Count.ShouldBe(2);
        secondPage.Count.ShouldBe(2);

        // No overlap between pages
        var firstPageIds = firstPage.Select(c => c.ConversationId).ToHashSet();
        var secondPageIds = secondPage.Select(c => c.ConversationId).ToHashSet();
        firstPageIds.Intersect(secondPageIds).ShouldBeEmpty();
    }

    [Test]
    public async Task CountConversationsForOwnerOptimizedAsync_WithValidOwner_ShouldReturnCorrectCount()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        await SetupMultipleConversationsForOwner(ownerId);

        // Act
        var count = await ConversationReadRepository.CountConversationsForOwnerOptimizedAsync(ownerId);

        // Assert
        count.ShouldBe(2); // Only active conversations counted
    }

    [Test]
    public async Task CountConversationsWithTitleOptimizedAsync_WithTitleFilter_ShouldReturnMatchingCount()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        var scenario = ChatTestDataBuilder.MultipleConversationsForOwnerScenario(ownerId, TimeProvider);

        await SaveConversationAsync(scenario.ActiveConversation);
        await SaveConversationAsync(scenario.CompletedConversation);
        await SaveConversationAsync(scenario.LongConversation);

        // Act
        var techCount = await ConversationReadRepository.CountConversationsWithTitleOptimizedAsync(
            ownerId, "Technical");
        var activeCount = await ConversationReadRepository.CountConversationsWithTitleOptimizedAsync(
            ownerId, "Active");

        // Assert
        techCount.ShouldBe(1); // "Long Technical Discussion" matches
        activeCount.ShouldBe(1); // "Active Conversation" matches
    }

    [Test]
    public async Task GetConversationsForOwnerOptimizedAsync_WithNoConversations_ShouldReturnEmptyList()
    {
        // Arrange
        var ownerId = AxonUserId.New();

        // Act
        var result = await ConversationReadRepository.GetConversationsForOwnerOptimizedAsync(
            ownerId, skip: 0, take: 10);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    #endregion

    #region Specification-Based Query Tests (High Priority)

    [Test]
    public async Task GetAllAsync_WithConversationsByOwnerSpec_ShouldReturnOwnerConversations()
    {
        // Arrange
        var owner1 = AxonUserId.New();
        var owner2 = AxonUserId.New();

        await SetupMultipleConversationsForOwner(owner1);
        await SetupMultipleConversationsForOwner(owner2);

        var spec = new ConversationsByOwnerSpec(owner1);

        // Act
        var result = await ConversationReadRepository.ListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3); // All conversations for owner1 (including completed)
        result.ShouldAllBe(c => c.OwnerId == owner1);
    }

    [Test]
    public async Task GetAllAsync_WithActiveByOwnerSpec_ShouldReturnOnlyActiveConversations()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        await SetupMultipleConversationsForOwner(ownerId);

        var spec = new ActiveByOwnerSpec(ownerId);

        // Act
        var result = await ConversationReadRepository.ListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2); // Only active conversations
        result.ShouldAllBe(c => c.Status == ConversationStatus.Active);
        result.ShouldAllBe(c => c.OwnerId == ownerId);
    }

    [Test]
    public async Task GetAllAsync_WithConversationsByStatusSpec_ShouldFilterByStatus()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        await SetupMultipleConversationsForOwner(ownerId);

        var activeSpec = new ConversationsByStatusSpec(ConversationStatus.Active);
        var completedSpec = new ConversationsByStatusSpec(ConversationStatus.Completed);

        // Act
        var activeResults = await ConversationReadRepository.ListAsync(activeSpec);
        var completedResults = await ConversationReadRepository.ListAsync(completedSpec);

        // Assert
        activeResults.ShouldAllBe(c => c.Status == ConversationStatus.Active);
        completedResults.ShouldAllBe(c => c.Status == ConversationStatus.Completed);

        activeResults.Count.ShouldBeGreaterThan(0);
        completedResults.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task GetAllAsync_WithConversationTitleContainsSpec_ShouldFilterByTitle()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        var scenario = ChatTestDataBuilder.MultipleConversationsForOwnerScenario(ownerId, TimeProvider);

        await SaveConversationAsync(scenario.ActiveConversation);
        await SaveConversationAsync(scenario.LongConversation);

        var spec = new ConversationTitleContainsSpec("Technical");

        // Act
        var result = await ConversationReadRepository.ListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.First().Title!.ShouldContain("Technical");
    }

    [Test]
    public async Task GetAllAsync_WithConversationsCreatedBetweenSpec_ShouldFilterByDateRange()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        var baseTime = TimeProvider.GetUtcNow();

        // Create conversations at different times
        SetTime(baseTime.AddDays(-10));
        await SaveConversationAsync(CreateTestConversation(ownerId, "Old"));

        SetTime(baseTime.AddDays(-2));
        var recentConversation = await SaveConversationAsync(CreateTestConversation(ownerId, "Recent"));

        var startDate = baseTime.AddDays(-5);
        var endDate = baseTime;
        var spec = new ConversationsCreatedBetweenSpec(startDate, endDate);

        // Act
        var result = await ConversationReadRepository.ListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(recentConversation.Id);
    }

    #endregion

    #region Performance and Index Usage Tests (Medium Priority)

    [Test]
    public async Task GetConversationsForOwnerOptimizedAsync_WithLargeDataset_ShouldPerformEfficiently()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        await SetupMultipleConversationsForPagination(ownerId, 50);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await ConversationReadRepository.GetConversationsForOwnerOptimizedAsync(
            ownerId, skip: 0, take: 20);
        var endTime = DateTime.UtcNow;

        var duration = endTime - startTime;

        // Assert
        result.Count.ShouldBe(20);
        duration.ShouldBeLessThan(TimeSpan.FromSeconds(2)); // Performance threshold
    }

    [Test]
    public async Task CountConversationsForOwnerOptimizedAsync_WithLargeDataset_ShouldPerformEfficiently()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        await SetupMultipleConversationsForPagination(ownerId, 100);

        // Act
        var startTime = DateTime.UtcNow;
        var count = await ConversationReadRepository.CountConversationsForOwnerOptimizedAsync(ownerId);
        var endTime = DateTime.UtcNow;

        var duration = endTime - startTime;

        // Assert
        count.ShouldBe(100);
        duration.ShouldBeLessThan(TimeSpan.FromSeconds(1)); // Performance threshold
    }

    #endregion

    #region Complex Query Scenarios (Medium Priority)

    [Test]
    public async Task GetByIdAsync_WithExistingConversation_ShouldReturnConversationWithMessages()
    {
        // Arrange
        var scenario = ChatTestDataBuilder.BasicConversationScenario(TimeProvider);
        await SaveConversationAsync(scenario.Conversation);

        // Act
        var result = await ConversationReadRepository.GetByIdAsync(scenario.Conversation.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(scenario.Conversation.Id);
        result.Messages.ShouldNotBeNull();
        result.Messages.Count.ShouldBe(4); // 2 user + 2 assistant messages
    }

    [Test]
    public async Task GetAllAsync_WithRecentlyUpdatedByOwnerSpec_ShouldReturnRecentlyUpdated()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        var baseTime = TimeProvider.GetUtcNow();

        // Create old conversation
        SetTime(baseTime.AddHours(-5));
        await SaveConversationAsync(CreateTestConversation(ownerId, "Old"));

        // Create recent conversation
        SetTime(baseTime.AddMinutes(-30));
        var recentConversation = await SaveConversationAsync(CreateTestConversation(ownerId, "Recent"));

        var since = baseTime.AddHours(-1);
        var spec = new RecentlyUpdatedByOwnerSpec(ownerId, since);

        // Act
        var result = await ConversationReadRepository.ListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(recentConversation.Id);
    }

    [Test]
    public async Task GetAllAsync_WithConversationsWithMinimumMessagesSpec_ShouldFilterByMessageCount()
    {
        // Arrange
        var ownerId = AxonUserId.New();

        await SaveConversationAsync(CreateTestConversation(ownerId, "Empty"));
        var conversationWithMessages = await SaveConversationAsync(
            CreateTestConversationWithMessages(ownerId, 3, 2));

        var spec = new ConversationsWithMinimumMessagesSpec(3);

        // Act
        var result = await ConversationReadRepository.ListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(conversationWithMessages.Id);
    }

    #endregion

    #region Edge Cases and Error Handling (Lower Priority - 20%)

    [Test]
    public async Task GetConversationsForOwnerOptimizedAsync_WithInvalidOwner_ShouldReturnEmptyList()
    {
        // Arrange
        var invalidOwnerId = new AxonUserId(Guid.Empty);

        // Act
        var result = await ConversationReadRepository.GetConversationsForOwnerOptimizedAsync(
            invalidOwnerId, skip: 0, take: 10);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task CountConversationsWithTitleOptimizedAsync_WithEmptyTitle_ShouldReturnZero()
    {
        // Arrange
        var ownerId = AxonUserId.New();
        await SetupMultipleConversationsForOwner(ownerId);

        // Act
        var count = await ConversationReadRepository.CountConversationsWithTitleOptimizedAsync(
            ownerId, "");

        // Assert
        count.ShouldBe(0);
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = ConversationId.New();

        // Act
        var result = await ConversationReadRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region Helper Methods

    private async Task<(Conversation active, Conversation completed, Conversation withMessages)> SetupMultipleConversationsForOwner(AxonUserId ownerId)
    {
        var active = CreateTestConversation(ownerId, "Active Conversation", TimeProvider);

        var completed = CreateTestConversationWithMessages(ownerId, 2, 1, TimeProvider);
        var completeResult = completed.Complete(TimeProvider);
        completeResult.IsSuccess.ShouldBeTrue();

        var withMessages = CreateTestConversationWithMessages(ownerId, 3, 2, TimeProvider);

        await SaveConversationAsync(active);
        await SaveConversationAsync(completed);
        await SaveConversationAsync(withMessages);

        return (active, completed, withMessages);
    }

    private async Task SetupMultipleConversationsForPagination(AxonUserId ownerId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var conversation = CreateTestConversation(ownerId, $"Conversation {i + 1}", TimeProvider);
            await SaveConversationAsync(conversation);

            // Advance time to ensure distinct UpdatedAt values
            AdvanceTime(TimeSpan.FromSeconds(1));
        }
    }

    #endregion
}