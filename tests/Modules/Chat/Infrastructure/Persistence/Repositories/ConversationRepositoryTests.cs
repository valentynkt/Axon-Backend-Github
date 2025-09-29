using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Infrastructure.Persistence.Builders;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Comprehensive tests for ConversationRepository write operations.
/// Covers CRUD operations, concurrency handling, business rules, and aggregate consistency.
/// Follows TDD principles and focuses on high-value scenarios (80/20 rule).
/// </summary>
[TestFixture]
public sealed class ConversationRepositoryTests : ChatPersistenceTestBase
{
    #region Basic CRUD Operations (High Priority - 80% Value)

    [Test]
    public async Task AddAsync_WithValidConversation_ShouldPersistSuccessfully()
    {
        // Arrange
        var conversation = CreateTestConversation();

        // Act
        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await AssertEntityPersistedAsync<Conversation>();

        var savedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        savedConversation.ShouldNotBeNull();
        savedConversation.Id.ShouldBe(conversation.Id);
        savedConversation.OwnerId.ShouldBe(conversation.OwnerId);
        savedConversation.Status.ShouldBe(conversation.Status);
        savedConversation.Title.ShouldBe(conversation.Title);
    }

    [Test]
    public async Task AddAsync_WithConversationAndMessages_ShouldPersistAggregate()
    {
        // Arrange
        var scenario = ChatTestDataBuilder.BasicConversationScenario(TimeProvider);
        var conversation = scenario.Conversation;

        // Act
        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await AssertConversationCompletelyLoadedAsync(conversation.Id);

        // Load the conversation with its messages through the aggregate root
        var savedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        savedConversation.ShouldNotBeNull();

        // Access messages through the aggregate root's methods
        var savedMessages = savedConversation.GetAllMessages();

        savedMessages.ShouldNotBeNull();
        savedMessages.Count.ShouldBe(4); // 2 user + 2 assistant messages
        savedMessages.ShouldAllBe(m => m.ConversationId == conversation.Id);

        // Verify message sequence integrity
        for (int i = 0; i < savedMessages.Count; i++)
        {
            savedMessages[i].Sequence.ShouldBe(i + 1);
        }
    }

    [Test]
    public async Task GetByIdAsync_WithExistingConversation_ShouldReturnConversation()
    {
        // Arrange
        var conversation = await SaveConversationAsync(CreateTestConversation());

        // Act
        var result = await ConversationRepository.GetByIdAsync(conversation.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(conversation.Id);
        result.OwnerId.ShouldBe(conversation.OwnerId);
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentConversation_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = ConversationId.New();

        // Act
        var result = await ConversationRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task UpdateAsync_WithModifiedConversation_ShouldPersistChanges()
    {
        // Arrange
        var conversation = await SaveConversationAsync(CreateTestConversation());
        var newTitle = "Updated Title";

        ClearChangeTracker();
        var freshConversation = await ConversationRepository.GetByIdAsync(conversation.Id);
        freshConversation.ShouldNotBeNull();

        // Act - advance time to ensure UpdatedAt will be greater
        AdvanceTime(TimeSpan.FromMinutes(1));
        var updateResult = freshConversation.UpdateTitle(newTitle, TimeProvider);
        updateResult.IsSuccess.ShouldBeTrue();

        await ConversationRepository.UpdateAsync(freshConversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        var updatedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        updatedConversation.ShouldNotBeNull();
        updatedConversation.Title.ShouldBe(newTitle);
        updatedConversation.UpdatedAt!.Value.ShouldBeGreaterThan(conversation.UpdatedAt!.Value);
    }

    [Test]
    public async Task DeleteAsync_WithExistingConversation_ShouldRemoveFromDatabase()
    {
        // Arrange
        var conversation = await SaveConversationAsync(CreateTestConversation());

        // Act
        await ConversationRepository.DeleteAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        var deletedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        deletedConversation.ShouldBeNull();
    }

    #endregion

    #region Aggregate Consistency Tests (High Priority)

    [Test]
    public async Task AddAsync_WithConversationAndMessages_ShouldMaintainAggregateIntegrity()
    {
        // Arrange
        var scenario = ChatTestDataBuilder.LargeConversationScenario(10, TimeProvider);
        var conversation = scenario.Conversation;

        // Act
        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        // Load the conversation with its messages through the aggregate root
        var savedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        savedConversation.ShouldNotBeNull();

        // Access messages through the aggregate root's methods
        var savedMessages = savedConversation.GetAllMessages();

        savedMessages.ShouldNotBeNull();
        savedMessages.Count.ShouldBe(10);

        // Verify all messages belong to the conversation
        savedMessages.ShouldAllBe(m => m.ConversationId == conversation.Id);

        // Verify message sequence integrity
        var sequences = savedMessages.Select(m => m.Sequence).OrderBy(s => s).ToList();
        sequences.ShouldBe(Enumerable.Range(1, 10).ToList());

        // Verify AI response IDs are unique for assistant messages
        var assistantMessages = savedMessages.Where(m => m.Role.IsAssistant).ToList();
        var aiResponseIds = assistantMessages.Select(m => m.AiResponseId).ToList();
        aiResponseIds.ShouldAllBe(id => id != null);
        aiResponseIds.Distinct().Count().ShouldBe(aiResponseIds.Count); // All unique

        // Verify user messages don't have AI response IDs
        var userMessages = savedMessages.Where(m => m.Role.IsUser).ToList();
        userMessages.ShouldAllBe(m => m.AiResponseId == null);
    }

    [Test]
    public async Task UpdateAsync_WithAddedMessages_ShouldPersistNewMessages()
    {
        // Arrange
        var conversation = await SaveConversationAsync(CreateTestConversation());

        ClearChangeTracker();
        var freshConversation = await ConversationRepository.GetByIdAsync(conversation.Id);
        freshConversation.ShouldNotBeNull();

        // Act - Add new messages
        var userContent = MessageContent.Create("New user message").Value;
        var userResult = freshConversation.AppendUserMessageToConversation(userContent, TimeProvider);
        userResult.IsSuccess.ShouldBeTrue();

        var assistantContent = MessageContent.Create("New assistant response").Value;
        var assistantResult = freshConversation.AppendAssistantResponseToConversation(
            assistantContent, new AiResponseId(Guid.NewGuid().ToString()), TimeProvider);
        assistantResult.IsSuccess.ShouldBeTrue();

        await ConversationRepository.UpdateAsync(freshConversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        // Load the updated conversation with its messages through the aggregate root
        var updatedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        updatedConversation.ShouldNotBeNull();

        // Access messages through the aggregate root's methods
        var updatedMessages = updatedConversation.GetAllMessages();

        updatedMessages.ShouldNotBeNull();
        updatedMessages.Count.ShouldBe(2);
        updatedMessages.ShouldContain(m => m.Content.Value == "New user message");
        updatedMessages.ShouldContain(m => m.Content.Value == "New assistant response");
    }

    #endregion

    #region Business Rules Enforcement (High Priority)

    [Test]
    public async Task AddAsync_WithCompletedConversation_ShouldPersistCorrectStatus()
    {
        // Arrange
        var scenario = ChatTestDataBuilder.CompletedConversationScenario(TimeProvider);
        var conversation = scenario.Conversation;

        // Act
        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        var savedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        savedConversation.ShouldNotBeNull();
        savedConversation.Status.ShouldBe(ConversationStatus.Completed);
        savedConversation.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_WhenCompletingConversation_ShouldUpdateStatus()
    {
        // Arrange
        var conversation = await SaveConversationAsync(
            CreateTestConversationWithMessages(userMessageCount: 2, assistantMessageCount: 1));

        ClearChangeTracker();
        var freshConversation = await ConversationRepository.GetByIdAsync(conversation.Id);
        freshConversation.ShouldNotBeNull();

        // Act
        var completeResult = freshConversation.Complete(TimeProvider);
        completeResult.IsSuccess.ShouldBeTrue();

        await ConversationRepository.UpdateAsync(freshConversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        var completedConversation = await QueryFreshAsync(
            () => ConversationRepository.GetByIdAsync(conversation.Id));

        completedConversation.ShouldNotBeNull();
        completedConversation.Status.ShouldBe(ConversationStatus.Completed);
        completedConversation.IsActive.ShouldBeFalse();
    }

    #endregion

    #region Concurrency and Transaction Tests (Medium Priority)

    [Test]
    public async Task UpdateAsync_WithConcurrentModifications_ShouldHandleGracefully()
    {
        // Arrange
        var conversation = await SaveConversationAsync(CreateTestConversation());
        var conversationId = conversation.Id;
        var initialVersion = conversation.Version;

        // Simulate first client: Load entity
        ClearChangeTracker();
        var conversation1 = await ConversationRepository.GetByIdAsync(conversationId);
        conversation1.ShouldNotBeNull();
        conversation1.Version.ShouldBe(initialVersion);

        // Simulate second client: Load entity in a detached state
        ClearChangeTracker();
        var conversation2 = await ConversationRepository.GetByIdAsync(conversationId);
        conversation2.ShouldNotBeNull();

        // Detach conversation2 to simulate it being loaded in a separate context
        DbContext.Entry(conversation2).State = EntityState.Detached;

        // Both should have the same Version initially
        conversation2.Version.ShouldBe(initialVersion);

        // First client updates and saves successfully
        var updateResult1 = conversation1.UpdateTitle("First Update", TimeProvider);
        updateResult1.IsSuccess.ShouldBeTrue();
        await ConversationRepository.UpdateAsync(conversation1);
        await UnitOfWork.SaveChangesAsync();

        // Clear the tracker to simulate conversation2 coming from a different context
        ClearChangeTracker();

        // Second client tries to update with stale version - should fail
        var updateResult2 = conversation2.UpdateTitle("Second Update", TimeProvider);
        updateResult2.IsSuccess.ShouldBeTrue();

        // The stale entity still has the original version
        conversation2.Version.ShouldBe(initialVersion);

        AssertConcurrencyConflict(async () =>
        {
            await ConversationRepository.UpdateAsync(conversation2);
            await UnitOfWork.SaveChangesAsync();
        });
    }

    [Test]
    public async Task AddAsync_MultipleConversationsInSameTransaction_ShouldPersistAll()
    {
        // Arrange
        var owner = AxonUserId.New();
        var conversations = new[]
        {
            CreateTestConversation(owner, "First Conversation"),
            CreateTestConversation(owner, "Second Conversation"),
            CreateTestConversation(owner, "Third Conversation")
        };

        // Act
        foreach (var conversation in conversations)
        {
            await ConversationRepository.AddAsync(conversation);
        }
        await UnitOfWork.SaveChangesAsync();

        // Assert
        var savedConversations = await QueryFreshAsync(
            async () => await DbContext.Conversations
                .Where(c => c.OwnerId == owner)
                .ToListAsync());

        savedConversations.ShouldNotBeNull();
        savedConversations.Count.ShouldBe(3);
        savedConversations.Select(c => c.Title).ShouldContain("First Conversation");
        savedConversations.Select(c => c.Title).ShouldContain("Second Conversation");
        savedConversations.Select(c => c.Title).ShouldContain("Third Conversation");
    }

    #endregion

    #region Performance and Batch Operations (Medium Priority)

    [Test]
    public async Task AddAsync_BatchOperations_ShouldPerformEfficiently()
    {
        // Arrange
        var owner = AxonUserId.New();
        var conversations = Enumerable.Range(1, 10)
            .Select(i => CreateTestConversation(owner, $"Batch Conversation {i}"))
            .ToList();

        // Act
        var startTime = DateTime.UtcNow;

        foreach (var conversation in conversations)
        {
            await ConversationRepository.AddAsync(conversation);
        }
        await UnitOfWork.SaveChangesAsync();

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        duration.ShouldBeLessThan(TimeSpan.FromSeconds(5)); // Performance threshold

        var savedConversationsCount = await DbContext.Conversations
            .Where(c => c.OwnerId == owner)
            .CountAsync();

        savedConversationsCount.ShouldBe(10);
    }

    #endregion

    #region Edge Cases and Error Handling (Lower Priority - 20%)

    [Test]
    public async Task AddAsync_WithNullConversation_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => ConversationRepository.AddAsync(null!));
    }

    [Test]
    public async Task GetByIdAsync_WithDefaultConversationId_ShouldReturnNull()
    {
        // Arrange
        var defaultId = new ConversationId(Guid.Empty);

        // Act
        var result = await ConversationRepository.GetByIdAsync(defaultId);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task UpdateAsync_WithDetachedEntity_ShouldHandleGracefully()
    {
        // Arrange
        // First save a conversation
        var conversation = await SaveConversationAsync(CreateTestConversation());
        var originalTitle = conversation.Title;

        // Clear the change tracker to detach all entities
        ClearChangeTracker();

        // Load the conversation again to get a detached copy
        var detachedConversation = await ConversationRepository.GetByIdAsync(conversation.Id);
        detachedConversation.ShouldNotBeNull();

        // Detach it from the context
        DbContext.Entry(detachedConversation).State = EntityState.Detached;

        // Modify the detached entity
        var updateResult = detachedConversation.UpdateTitle("Updated Title", TimeProvider);
        updateResult.IsSuccess.ShouldBeTrue();

        // Act - Update the detached entity
        await ConversationRepository.UpdateAsync(detachedConversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert - Verify it was saved
        ClearChangeTracker();
        var saved = await ConversationRepository.GetByIdAsync(conversation.Id);
        saved.ShouldNotBeNull();
        saved.Title.ShouldBe("Updated Title");
        saved.Title.ShouldNotBe(originalTitle);
    }

    #endregion
}