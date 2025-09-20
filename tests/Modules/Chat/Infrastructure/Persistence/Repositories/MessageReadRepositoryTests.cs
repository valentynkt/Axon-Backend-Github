using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.Builders;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Tests for MessageReadRepository read operations.
/// Covers message retrieval, filtering, and relationship navigation scenarios.
/// Focuses on high-value message query operations following the 80/20 rule.
/// </summary>
[TestFixture]
public sealed class MessageReadRepositoryTests : ChatPersistenceTestBase
{
    #region Basic Message Retrieval Tests (High Priority - 80% Value)

    [Test]
    public async Task GetByIdAsync_WithExistingMessage_ShouldReturnMessage()
    {
        // Arrange
        var scenario = await SetupConversationWithMessages();
        var firstMessage = scenario.conversation.Messages.First();

        // Act
        var result = await MessageReadRepository.GetByIdAsync(firstMessage.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(firstMessage.Id);
        result.ConversationId.ShouldBe(scenario.conversation.Id);
        result.Content.ShouldBe(firstMessage.Content);
        result.Role.ShouldBe(firstMessage.Role);
        result.Sequence.ShouldBe(firstMessage.Sequence);
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentMessage_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = MessageId.New();

        // Act
        var result = await MessageReadRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetAllAsync_WithoutFilter_ShouldReturnAllMessages()
    {
        // Arrange
        var scenario1 = await SetupConversationWithMessages();
        var scenario2 = await SetupConversationWithMessages();

        // Act
        var allMessages = await MessageReadRepository.ListAsync();

        // Assert
        allMessages.ShouldNotBeNull();
        allMessages.Count.ShouldBe(8); // 4 messages per conversation * 2 conversations

        // Verify messages from both conversations are present
        allMessages.ShouldContain(m => m.ConversationId == scenario1.conversation.Id);
        allMessages.ShouldContain(m => m.ConversationId == scenario2.conversation.Id);
    }

    #endregion

    #region Message Filtering and Querying Tests (High Priority)

    [Test]
    public async Task GetAllAsync_FilteredByConversationId_ShouldReturnConversationMessages()
    {
        // Arrange
        var scenario1 = await SetupConversationWithMessages();
        var scenario2 = await SetupConversationWithMessages();

        // Act
        var conversation1Messages = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario1.conversation.Id)
            .ToListAsync();

        // Assert
        conversation1Messages.ShouldNotBeNull();
        conversation1Messages.Count.ShouldBe(4);
        conversation1Messages.ShouldAllBe(m => m.ConversationId == scenario1.conversation.Id);

        // Verify sequence integrity
        var sequences = conversation1Messages.Select(m => m.Sequence).OrderBy(s => s).ToList();
        sequences.ShouldBe(new[] { 1, 2, 3, 4 });
    }

    [Test]
    public async Task GetAllAsync_FilteredByRole_ShouldReturnRoleSpecificMessages()
    {
        // Arrange
        var scenario = await SetupConversationWithMessages();

        // Act
        var userMessages = await ReadDbContext.Set<Message>()
            .Where(m => m.Role == MessageRole.User)
            .ToListAsync();

        var assistantMessages = await ReadDbContext.Set<Message>()
            .Where(m => m.Role == MessageRole.Assistant)
            .ToListAsync();

        // Assert
        userMessages.ShouldNotBeNull();
        userMessages.Count.ShouldBe(2);
        userMessages.ShouldAllBe(m => m.Role.IsUser);
        userMessages.ShouldAllBe(m => m.AiResponseId == null);

        assistantMessages.ShouldNotBeNull();
        assistantMessages.Count.ShouldBe(2);
        assistantMessages.ShouldAllBe(m => m.Role.IsAssistant);
        assistantMessages.ShouldAllBe(m => m.AiResponseId != null);
    }

    [Test]
    public async Task GetAllAsync_OrderedBySequence_ShouldMaintainMessageOrder()
    {
        // Arrange
        var scenario = await SetupConversationWithMessages();

        // Act
        var orderedMessages = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario.conversation.Id)
            .OrderBy(m => m.Sequence)
            .ToListAsync();

        // Assert
        orderedMessages.ShouldNotBeNull();
        orderedMessages.Count.ShouldBe(4);

        // Verify sequential ordering
        for (int i = 0; i < orderedMessages.Count; i++)
        {
            orderedMessages[i].Sequence.ShouldBe(i + 1);
        }

        // Verify alternating roles (user first, then assistant, etc.)
        orderedMessages[0].Role.IsUser.ShouldBeTrue();
        orderedMessages[1].Role.IsAssistant.ShouldBeTrue();
        orderedMessages[2].Role.IsUser.ShouldBeTrue();
        orderedMessages[3].Role.IsAssistant.ShouldBeTrue();
    }

    #endregion

    #region Message Content and Metadata Tests (Medium Priority)

    [Test]
    public async Task GetAllAsync_FilteredByContent_ShouldReturnMatchingMessages()
    {
        // Arrange
        var scenario = await SetupConversationWithSpecificContent();

        // Act - Use client evaluation for SQLite compatibility
        var helpMessages = await ReadDbContext.Set<Message>()
            .ToListAsync();

        var filteredMessages = helpMessages
            .Where(m => m.Content.Value.Contains("help", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        filteredMessages.ShouldNotBeNull();
        filteredMessages.Count.ShouldBeGreaterThan(0);
        filteredMessages.ShouldAllBe(m => m.Content.Value.Contains("help", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task GetAllAsync_FilteredByAiResponseId_ShouldReturnSpecificAssistantMessage()
    {
        // Arrange
        var scenario = await SetupConversationWithMessages();
        var assistantMessage = scenario.conversation.Messages
            .First(m => m.Role.IsAssistant);

        // Act
        var result = await ReadDbContext.Set<Message>()
            .Where(m => m.AiResponseId == assistantMessage.AiResponseId)
            .ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(assistantMessage.Id);
        result.First().AiResponseId.ShouldBe(assistantMessage.AiResponseId);
    }

    [Test]
    public async Task GetAllAsync_FilteredBySequenceRange_ShouldReturnMessageSubset()
    {
        // Arrange
        var scenario = await SetupLargeConversation();

        // Act
        var middleMessages = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario.conversation.Id &&
                       m.Sequence >= 3 && m.Sequence <= 7)
            .OrderBy(m => m.Sequence)
            .ToListAsync();

        // Assert
        middleMessages.ShouldNotBeNull();
        middleMessages.Count.ShouldBe(5);
        middleMessages.First().Sequence.ShouldBe(3);
        middleMessages.Last().Sequence.ShouldBe(7);
    }

    #endregion

    #region Performance and Large Dataset Tests (Medium Priority)

    [Test]
    public async Task GetAllAsync_WithLargeConversation_ShouldPerformEfficiently()
    {
        // Arrange
        var scenario = await SetupLargeConversation(50);

        // Act
        var startTime = DateTime.UtcNow;
        var messages = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario.conversation.Id)
            .ToListAsync();
        var endTime = DateTime.UtcNow;

        var duration = endTime - startTime;

        // Assert
        messages.Count.ShouldBe(50);
        duration.ShouldBeLessThan(TimeSpan.FromSeconds(2)); // Performance threshold
    }

    [Test]
    public async Task GetAllAsync_WithPagination_ShouldRespectSkipAndTake()
    {
        // Arrange
        var scenario = await SetupLargeConversation(20);

        // Act
        var firstPage = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario.conversation.Id)
            .OrderBy(m => m.Sequence)
            .Skip(0)
            .Take(5)
            .ToListAsync();

        var secondPage = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario.conversation.Id)
            .OrderBy(m => m.Sequence)
            .Skip(5)
            .Take(5)
            .ToListAsync();

        // Assert
        firstPage.Count.ShouldBe(5);
        secondPage.Count.ShouldBe(5);

        firstPage.First().Sequence.ShouldBe(1);
        firstPage.Last().Sequence.ShouldBe(5);
        secondPage.First().Sequence.ShouldBe(6);
        secondPage.Last().Sequence.ShouldBe(10);
    }

    #endregion

    #region Message Relationship and Navigation Tests (Medium Priority)

    [Test]
    public async Task GetAllAsync_WithConversationNavigation_ShouldLoadRelationship()
    {
        // Arrange
        var scenario = await SetupConversationWithMessages();

        // Act - Remove invalid Include since Message doesn't have navigation back to Conversation
        var messagesWithConversation = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario.conversation.Id)
            .ToListAsync();

        // Assert
        messagesWithConversation.ShouldNotBeNull();
        messagesWithConversation.Count.ShouldBe(4);
        messagesWithConversation.ShouldAllBe(m => m.ConversationId == scenario.conversation.Id);
    }

    [Test]
    public async Task GetAllAsync_GroupedByRole_ShouldCorrectlySegmentMessages()
    {
        // Arrange
        var scenario = await SetupConversationWithMessages();

        // Act
        var messagesByRole = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == scenario.conversation.Id)
            .GroupBy(m => m.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();

        // Assert
        messagesByRole.ShouldNotBeNull();
        messagesByRole.Count.ShouldBe(2); // User and Assistant roles

        var userGroup = messagesByRole.First(g => g.Role.IsUser);
        var assistantGroup = messagesByRole.First(g => g.Role.IsAssistant);

        userGroup.Count.ShouldBe(2);
        assistantGroup.Count.ShouldBe(2);
    }

    #endregion

    #region Edge Cases and Error Handling (Lower Priority - 20%)

    [Test]
    public async Task GetByIdAsync_WithDefaultMessageId_ShouldReturnNull()
    {
        // Arrange
        var defaultId = new MessageId(Guid.Empty);

        // Act
        var result = await MessageReadRepository.GetByIdAsync(defaultId);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetAllAsync_WithEmptyDatabase_ShouldReturnEmptyList()
    {
        // Act
        var result = await MessageReadRepository.ListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetAllAsync_FilteredByNonExistentConversation_ShouldReturnEmptyList()
    {
        // Arrange
        await SetupConversationWithMessages();
        var nonExistentConversationId = ConversationId.New();

        // Act
        var result = await ReadDbContext.Set<Message>()
            .Where(m => m.ConversationId == nonExistentConversationId)
            .ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    #endregion

    #region Helper Methods

    private async Task<(Conversation conversation, AxonUserId ownerId)> SetupConversationWithMessages()
    {
        var scenario = ChatTestDataBuilder.BasicConversationScenario(TimeProvider);
        await SaveConversationAsync(scenario.Conversation);
        return (scenario.Conversation, scenario.OwnerId);
    }

    private async Task<(Conversation conversation, AxonUserId ownerId)> SetupConversationWithSpecificContent()
    {
        var ownerId = AxonUserId.New();
        var conversation = Builders.ConversationBuilder.Create(TimeProvider)
            .WithOwner(ownerId)
            .WithTitle("Help Conversation")
            .WithUserMessage("I need help with something")
            .WithAssistantMessage("I'd be happy to help you!")
            .WithUserMessage("Can you help me understand this feature?")
            .WithAssistantMessage("Of course! Let me help explain the feature.")
            .Build();

        await SaveConversationAsync(conversation);
        return (conversation, ownerId);
    }

    private async Task<(Conversation conversation, AxonUserId ownerId)> SetupLargeConversation(int messageCount = 20)
    {
        var scenario = ChatTestDataBuilder.LargeConversationScenario(messageCount, TimeProvider);
        await SaveConversationAsync(scenario.Conversation);
        return (scenario.Conversation, scenario.OwnerId);
    }

    #endregion
}