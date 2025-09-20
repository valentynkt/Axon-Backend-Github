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

        var savedConversation = await QueryFreshAsync(
            () => DbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversation.Id));

        savedConversation.ShouldNotBeNull();

        // Verify messages were saved separately since Messages property is ignored in read context
        var savedMessages = await QueryFreshAsync(async () =>
        {
            var result = await DbContext.Set<Message>()
                .Where(m => m.ConversationId == conversation.Id)
                .OrderBy(m => m.Sequence)
                .ToListAsync();
            return result;
        });

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

        // Act
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
        var savedConversation = await QueryFreshAsync(
            () => DbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversation.Id));

        savedConversation.ShouldNotBeNull();

        // Verify messages were saved separately since Messages property is ignored in read context
        var savedMessages = await QueryFreshAsync(async () =>
        {
            var result = await DbContext.Set<Message>()
                .Where(m => m.ConversationId == conversation.Id)
                .OrderBy(m => m.Sequence)
                .ToListAsync();
            return result;
        });

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
        var updatedConversation = await QueryFreshAsync(
            () => DbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversation.Id));

        updatedConversation.ShouldNotBeNull();

        // Verify messages were saved separately since Messages property is ignored in read context
        var updatedMessages = await QueryFreshAsync(async () =>
        {
            var result = await DbContext.Set<Message>()
                .Where(m => m.ConversationId == conversation.Id)
                .OrderBy(m => m.Sequence)
                .ToListAsync();
            return result;
        });

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

        // Get two instances of the same conversation
        ClearChangeTracker();
        var conversation1 = await ConversationRepository.GetByIdAsync(conversation.Id);
        var conversation2 = await ConversationRepository.GetByIdAsync(conversation.Id);

        conversation1.ShouldNotBeNull();
        conversation2.ShouldNotBeNull();

        // Act & Assert
        // First update should succeed
        var updateResult1 = conversation1.UpdateTitle("First Update", TimeProvider);
        updateResult1.IsSuccess.ShouldBeTrue();
        await ConversationRepository.UpdateAsync(conversation1);
        await UnitOfWork.SaveChangesAsync();

        // Second update might fail due to concurrency (depends on SQLite behavior)
        var updateResult2 = conversation2.UpdateTitle("Second Update", TimeProvider);
        updateResult2.IsSuccess.ShouldBeTrue();

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
        var conversation = CreateTestConversation();

        // Act & Assert - This should either work or throw a specific exception
        // The exact behavior depends on EF Core configuration
        try
        {
            await ConversationRepository.UpdateAsync(conversation);
            await UnitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Expected for detached entities
            ex.ShouldBeOfType<InvalidOperationException>();
        }
    }

    #endregion
}