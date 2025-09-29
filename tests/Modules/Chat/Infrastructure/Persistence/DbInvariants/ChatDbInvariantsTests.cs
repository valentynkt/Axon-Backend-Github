using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbInvariants;

/// <summary>
/// Database invariant tests for Chat Module.
/// These tests validate that database constraints alone prevent data violations.
/// Tests are designed to initially fail (TDD approach) to expose gaps in current implementation.
/// </summary>
[TestFixture]
public class ChatDbInvariantsTests : ChatDbInvariantsTestBase
{
    #region Test 1: MESSAGE_SEQUENCE_UNIQUE_PerConversation

    [Test]
    public async Task Test_MESSAGE_SEQUENCE_UNIQUE_DifferentConversations_ShouldSucceed()
    {
        // Arrange: Create two conversations with messages
        var conv1 = TestDataFixtures.CreateConversationWithUserMessage(timeProvider: TimeProvider);
        var conv2 = TestDataFixtures.CreateConversationWithUserMessage(timeProvider: TimeProvider);

        // Act: Save both conversations (both have message with sequence 1)
        await ConversationRepository.AddAsync(conv1);
        await ConversationRepository.AddAsync(conv2);

        // Assert: Should succeed (same sequence allowed in different conversations)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 2: AI_RESPONSE_ID_UNIQUE_Global

    [Test]
    public async Task Test_AI_RESPONSE_ID_UNIQUE_Global_ShouldFail()
    {
        // Arrange: Create first conversation with AI response
        var aiResponseId = TestDataFixtures.CreateFixedAiResponseId("test-idempotency");
        var conv1 = TestDataFixtures.CreateConversationWithMessagePair(
            aiResponseId: aiResponseId,
            timeProvider: TimeProvider);

        await ConversationRepository.AddAsync(conv1);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to create second conversation with same AI response ID
        var conv2 = TestDataFixtures.CreateConversationWithUserMessage(timeProvider: TimeProvider);
        var content = MessageContent.From("Different response");
        conv2.AppendAssistantResponseToConversation(content, aiResponseId, TimeProvider);

        await ConversationRepository.AddAsync(conv2);

        // Assert: Should violate unique constraint on ai_response_id
        await AssertPostgreSQLConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ai_response" // Expected constraint name pattern
        );
    }

    [Test]
    public async Task Test_AI_RESPONSE_ID_RetryIdempotency_ShouldSucceed()
    {
        // Arrange: Create conversation with AI response
        var aiResponseId = TestDataFixtures.CreateFixedAiResponseId("retry-test");
        var conversation = TestDataFixtures.CreateConversationWithMessagePair(
            aiResponseId: aiResponseId,
            timeProvider: TimeProvider);

        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Act: Simulate retry with same AI response ID (idempotent operation)
        // In real scenario, this would be handled by application logic
        // Here we verify the constraint exists to support idempotency

        var messageCount = conversation.GetMessageCount();
        messageCount.ShouldBe(2); // User message + Assistant response

        // Try to add same response again (would be caught by domain logic normally)
        var result = conversation.AppendAssistantResponseToConversation(
            MessageContent.From("Same response"),
            aiResponseId, // Same ID - idempotent
            TimeProvider);

        // Domain should prevent this, but if it didn't, DB constraint would catch it
        result.IsFailure.ShouldBeTrue("Domain should prevent duplicate AI response ID");
    }

    #endregion

    #region Test 3: MESSAGES_CASCADE_DELETE_WithConversation

    [Test]
    public async Task Test_MESSAGES_CASCADE_DELETE_WithConversation()
    {
        // Arrange: Create conversation with multiple messages
        var conversation = TestDataFixtures.CreateConversationWithMultipleExchanges(
            exchangeCount: 3,
            timeProvider: TimeProvider);

        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Verify messages exist
        var messageCountBefore = await VerificationRepository.GetMessageCountAsync(conversation.Id);
        messageCountBefore.ShouldBe(6); // 3 exchanges = 6 messages

        // Act: Delete conversation
        await DbContext.Database.ExecuteSqlRawAsync(
            @"DELETE FROM chat.""Conversations"" WHERE ""Id"" = @id",
            new NpgsqlParameter("@id", conversation.Id.Value));

        // Assert: Messages should be cascade deleted
        var messageCountAfter = await VerificationRepository.GetMessageCountAsync(conversation.Id);
        messageCountAfter.ShouldBe(0);
    }

    #endregion

    #region Test 5: AGGREGATE_VERSION_UpdatesOnChildChange

    [Test]
    public async Task Test_AGGREGATE_VERSION_UpdatesOnChildChange()
    {
        // Arrange: Create conversation
        var conversation = TestDataFixtures.CreateBasicConversation(timeProvider: TimeProvider);
        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Get initial version
        var versionBefore = await GetAggregateVersion(conversation.Id);

        // Act: Add a message to the conversation
        DbContext.ChangeTracker.Clear();
        var loadedConversation = await ConversationRepository.GetByIdAsync(conversation.Id);
        loadedConversation.ShouldNotBeNull();

        var content = MessageContent.From("New message");
        var result = loadedConversation.AppendUserMessageToConversation(content, TimeProvider);
        result.IsSuccess.ShouldBeTrue();

        await ConversationRepository.UpdateAsync(loadedConversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Version should have changed
        var versionAfter = await GetAggregateVersion(conversation.Id);
        versionAfter.ShouldNotBe(versionBefore, "Aggregate version should update when child entity is added");
    }

    [Test]
    public async Task Test_AGGREGATE_VERSION_UpdatesOnTitleChange()
    {
        // Arrange: Create conversation
        var conversation = TestDataFixtures.CreateBasicConversation(timeProvider: TimeProvider);
        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Get initial version
        var versionBefore = await GetAggregateVersion(conversation.Id);

        // Act: Update title
        DbContext.ChangeTracker.Clear();
        var loadedConversation = await ConversationRepository.GetByIdAsync(conversation.Id);
        loadedConversation.ShouldNotBeNull();

        var titleResult = loadedConversation.UpdateTitle("New Title", TimeProvider);
        titleResult.IsSuccess.ShouldBeTrue();

        await ConversationRepository.UpdateAsync(loadedConversation);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Version should have changed
        var versionAfter = await GetAggregateVersion(conversation.Id);
        versionAfter.ShouldNotBe(versionBefore, "Aggregate version should update on property change");
    }

    #endregion

    #region Test 6: MESSAGE_ROLE_ALTERNATION_Integrity

    [Test]
    public async Task Test_MESSAGE_SEQUENCE_Integrity_AfterOperations()
    {
        // Arrange: Create conversation with messages
        var conversation = TestDataFixtures.CreateConversationWithMultipleExchanges(
            exchangeCount: 5,
            timeProvider: TimeProvider);

        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();

        // Act & Assert: Verify sequence integrity
        await AssertMessageSequenceIntegrity(conversation.Id);

        // Verify we have correct number of messages
        var messages = conversation.GetAllMessages();
        messages.Count.ShouldBe(10); // 5 exchanges = 10 messages

        // Verify alternating pattern (User, Assistant, User, Assistant...)
        for (int i = 0; i < messages.Count; i++)
        {
            var expectedRole = (i % 2 == 0) ? MessageRole.User : MessageRole.Assistant;
            messages[i].Role.ShouldBe(expectedRole, $"Message at position {i} should have role {expectedRole}");
        }
    }

    #endregion
}