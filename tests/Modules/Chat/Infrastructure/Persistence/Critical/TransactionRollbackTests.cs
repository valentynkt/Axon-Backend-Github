using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Critical;

/// <summary>
/// Critical tests for transaction rollback scenarios.
/// Validates data integrity when multi-operation transactions fail.
/// These are high-value tests covering the 20% of scenarios that cause 80% of data corruption issues.
/// </summary>
[TestFixture]
[Category("Critical")]
[Category("Transaction")]
public class TransactionRollbackTests : ChatPersistenceTestBase
{
    private TestDataSnapshot _snapshotTool = null!;

    protected override Task SetUpDerived()
    {
        _snapshotTool = new TestDataSnapshot();
        return Task.CompletedTask;
    }

    #region Critical: Multi-Operation Transaction Rollback

    [Test]
    public async Task Transaction_MultipleMessageAppends_RollbackOnFailure()
    {
        // Arrange: Create conversation and capture initial state
        var conversation = CreateTestConversation(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        var initialSnapshot = _snapshotTool.CaptureConversation(conversation);

        // Act: Start transaction with multiple operations that will fail
        using var transaction = await DbContext.Database.BeginTransactionAsync();

        try
        {
            // Load conversation in transaction
            var loaded = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
            loaded.ShouldNotBeNull();

            // Add first message - should succeed locally
            var content1 = MessageContent.From("First message in transaction");
            var result1 = loaded.AppendUserMessageToConversation(content1, TimeProvider);
            result1.IsSuccess.ShouldBeTrue();

            // Add assistant response - should succeed locally
            var content2 = MessageContent.From("Assistant response in transaction");
            var aiResponseId = new AiResponseId($"tx-{Guid.NewGuid()}");
            var result2 = loaded.AppendAssistantResponseToConversation(content2, aiResponseId, TimeProvider);
            result2.IsSuccess.ShouldBeTrue();

            // Update and save changes
            await ConversationWriteRepository.UpdateAsync(loaded);
            await UnitOfWork.SaveChangesAsync();

            // Simulate failure by forcing a constraint violation
            await ForceConstraintViolation(conversation.Id);

            // This should never be reached
            await transaction.CommitAsync();
            Assert.Fail("Transaction should have failed");
        }
        catch (Exception)
        {
            // Transaction should rollback
            await transaction.RollbackAsync();
        }

        // Assert: Verify no changes were persisted
        ClearChangeTracker();
        var afterRollback = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
        afterRollback.ShouldNotBeNull();

        var finalSnapshot = _snapshotTool.CaptureConversation(afterRollback);
        var comparison = _snapshotTool.Compare(initialSnapshot, finalSnapshot);

        comparison.HasDifferences.ShouldBeFalse(
            $"Transaction rollback should restore original state. Differences: {string.Join(", ", comparison.Differences)}");

        // Verify message count unchanged
        afterRollback.GetMessageCount().ShouldBe(0, "No messages should be persisted after rollback");
    }

    [Test]
    public async Task Transaction_ConversationStateChanges_AtomicRollback()
    {
        // Arrange: Create conversation with messages
        var conversation = CreateTestConversationWithMessages(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        var initialMessageCount = conversation.GetMessageCount();
        var initialTitle = conversation.Title;

        // Act: Perform multiple state changes in a transaction
        using var transaction = await DbContext.Database.BeginTransactionAsync();

        try
        {
            var loaded = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
            loaded.ShouldNotBeNull();

            // Change 1: Update title
            var titleResult = loaded.UpdateTitle("Transaction Title", TimeProvider);
            titleResult.IsSuccess.ShouldBeTrue();

            // Change 2: Add user message
            var userContent = MessageContent.From("Transaction message");
            var messageResult = loaded.AppendUserMessageToConversation(userContent, TimeProvider);
            messageResult.IsSuccess.ShouldBeTrue();

            // Change 3: Add AI response
            var aiContent = MessageContent.From("Transaction AI response");
            var aiResponseId = new AiResponseId($"tx-ai-{Guid.NewGuid()}");
            var aiResult = loaded.AppendAssistantResponseToConversation(aiContent, aiResponseId, TimeProvider);
            aiResult.IsSuccess.ShouldBeTrue();

            // Save changes
            await ConversationWriteRepository.UpdateAsync(loaded);
            await UnitOfWork.SaveChangesAsync();

            // Simulate business rule violation that requires rollback
            throw new InvalidOperationException("Simulated business rule violation");
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync();
        }

        // Assert: All changes rolled back atomically
        ClearChangeTracker();
        var afterRollback = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
        afterRollback.ShouldNotBeNull();

        afterRollback.Title.ShouldBe(initialTitle, "Title should be restored");
        afterRollback.GetMessageCount().ShouldBe(initialMessageCount, "Message count should be restored");
        var hasTransactionId = afterRollback.LastAiResponseId?.Value?.Contains("tx-ai") ?? false;
        hasTransactionId.ShouldBeFalse("Transaction AI response should not exist");
    }

    #endregion

    #region Critical: Nested Transaction Behavior

    [Test]
    public async Task NestedTransaction_InnerFailure_OuterRollback()
    {
        // Arrange: Create test conversation
        var conversation = CreateTestConversation(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        // Act: Nested transaction scenario
        using var outerTransaction = await DbContext.Database.BeginTransactionAsync();

        try
        {
            // Outer transaction operation
            var loaded = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
            loaded.ShouldNotBeNull();

            loaded.UpdateTitle("Outer Transaction Title", TimeProvider);
            await ConversationWriteRepository.UpdateAsync(loaded);
            await UnitOfWork.SaveChangesAsync();

            // Create savepoint for inner transaction
            await CreateSavepointAsync(outerTransaction, "inner_operation");

            try
            {
                // Inner transaction operations
                var userContent = MessageContent.From("Inner transaction message");
                loaded.AppendUserMessageToConversation(userContent, TimeProvider);

                await ConversationWriteRepository.UpdateAsync(loaded);
                await UnitOfWork.SaveChangesAsync();

                // Force inner failure
                await ForceConstraintViolation(conversation.Id);
            }
            catch
            {
                // Rollback to savepoint
                await RollbackToSavepointAsync(outerTransaction, "inner_operation");
                throw; // Propagate to outer
            }
        }
        catch
        {
            await outerTransaction.RollbackAsync();
        }

        // Assert: Everything rolled back
        ClearChangeTracker();
        var final = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
        final.ShouldNotBeNull();

        final.Title.ShouldNotBe("Outer Transaction Title");
        final.GetMessageCount().ShouldBe(0);
    }

    #endregion

    #region Critical: Concurrent Transaction Conflicts

    [Test]
    public async Task ConcurrentTransactions_ConflictDetection_ProperRollback()
    {
        // Arrange: Create conversation
        var conversation = CreateTestConversation(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        // Act: Start two concurrent transactions
        await using var context1 = new ChatDbContext(
            ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(ConnectionString),
            TimeProvider);

        await using var context2 = new ChatDbContext(
            ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(ConnectionString),
            TimeProvider);

        var transaction1 = await context1.Database.BeginTransactionAsync();
        var transaction2 = await context2.Database.BeginTransactionAsync();

        var succeeded = 0;
        var failed = 0;

        // Transaction 1 - Update the same field (title) to create actual conflict
        var task1 = Task.Run(async () =>
        {
            try
            {
                var conv = await context1.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversation.Id);
                conv.ShouldNotBeNull();

                // Update title - this modifies the Conversation row itself
                conv.UpdateTitle("Transaction 1 Title", TimeProvider);

                await context1.SaveChangesAsync();
                await transaction1.CommitAsync();
                Interlocked.Increment(ref succeeded);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction1.RollbackAsync();
                Interlocked.Increment(ref failed);
            }
            catch
            {
                await transaction1.RollbackAsync();
                Interlocked.Increment(ref failed);
            }
            finally
            {
                await transaction1.DisposeAsync();
            }
        });

        // Transaction 2 - Update the same field (title) to create conflict
        var task2 = Task.Run(async () =>
        {
            try
            {
                var conv = await context2.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversation.Id);
                conv.ShouldNotBeNull();

                // Update title - same field as transaction 1, creates conflict
                conv.UpdateTitle("Transaction 2 Title", TimeProvider);

                await context2.SaveChangesAsync();
                await transaction2.CommitAsync();
                Interlocked.Increment(ref succeeded);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction2.RollbackAsync();
                Interlocked.Increment(ref failed);
            }
            catch
            {
                await transaction2.RollbackAsync();
                Interlocked.Increment(ref failed);
            }
            finally
            {
                await transaction2.DisposeAsync();
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert: One succeeded, one failed due to concurrency conflict
        succeeded.ShouldBe(1, "Exactly one transaction should succeed");
        failed.ShouldBe(1, "Exactly one transaction should fail");

        // Verify final state - one of the title updates should have persisted
        ClearChangeTracker();
        var final = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
        final.ShouldNotBeNull();
        (final.Title?.Contains("Transaction") ?? false).ShouldBeTrue("One transaction's title update should have succeeded");
    }

    #endregion

    #region Critical: Partial Failure Scenarios

    [Test]
    public async Task PartialOperation_Failure_NoPartialState()
    {
        // Arrange: Create conversation
        var conversation = CreateTestConversation(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        // Act: Attempt operation that partially succeeds then fails
        var operationCompleted = false;

        try
        {
            using var transaction = await DbContext.Database.BeginTransactionAsync();

            var loaded = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
            loaded.ShouldNotBeNull();

            // Step 1: Add messages (succeeds)
            for (int i = 0; i < 5; i++)
            {
                if (i % 2 == 0)
                {
                    var userContent = MessageContent.From($"User message {i}");
                    loaded.AppendUserMessageToConversation(userContent, TimeProvider);
                }
                else
                {
                    var aiContent = MessageContent.From($"AI response {i}");
                    var aiId = new AiResponseId($"ai-{i}");
                    loaded.AppendAssistantResponseToConversation(aiContent, aiId, TimeProvider);
                }
            }

            await ConversationWriteRepository.UpdateAsync(loaded);
            await UnitOfWork.SaveChangesAsync();

            // Step 2: Try to complete (will fail due to business rules if we break them)
            // Simulate by trying to add duplicate AI response ID
            var duplicateId = new AiResponseId("ai-1"); // Already used
            var duplicateResult = loaded.AppendAssistantResponseToConversation(
                MessageContent.From("Duplicate"), duplicateId, TimeProvider);

            if (!duplicateResult.IsSuccess)
            {
                throw new InvalidOperationException("Operation failed - rollback required");
            }

            await ConversationWriteRepository.UpdateAsync(loaded);
            await UnitOfWork.SaveChangesAsync();

            await transaction.CommitAsync();
            operationCompleted = true;
        }
        catch
        {
            // Transaction auto-rollback on dispose
        }

        // Assert: No partial state persisted
        operationCompleted.ShouldBeFalse("Operation should have failed");

        ClearChangeTracker();
        var final = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
        final.ShouldNotBeNull();
        final.GetMessageCount().ShouldBe(0, "No messages should be persisted after failed transaction");
    }

    #endregion

    #region Critical: Deadlock Recovery

    [Test]
    [CancelAfter(30000)] // 30 seconds timeout for deadlock detection
    public async Task Transaction_Deadlock_ProperRecovery()
    {
        // Arrange: Create two conversations
        var conv1 = CreateTestConversation(timeProvider: TimeProvider);
        var conv2 = CreateTestConversation(timeProvider: TimeProvider);
        await SaveConversationAsync(conv1);
        await SaveConversationAsync(conv2);

        // Act: Create potential deadlock scenario
        // Note: NpgsqlExecutionStrategy automatically retries transient failures including deadlocks
        var task1Completed = false;
        var task2Completed = false;
        Exception? task1Exception = null;
        Exception? task2Exception = null;

        var task1 = Task.Run(async () =>
        {
            try
            {
                await using var context = new ChatDbContext(
                    ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(ConnectionString),
                    TimeProvider);

                // Use execution strategy which handles transient failures (including deadlocks)
                var strategy = context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();

                    // Lock conv1 first
                    var c1 = await context.Conversations.FirstAsync(c => c.Id == conv1.Id);
                    c1.UpdateTitle("Task1 - Conv1", TimeProvider);
                    await context.SaveChangesAsync();

                    // Small delay to increase deadlock probability
                    await Task.Delay(50);

                    // Then try to lock conv2
                    var c2 = await context.Conversations.FirstAsync(c => c.Id == conv2.Id);
                    c2.UpdateTitle("Task1 - Conv2", TimeProvider);
                    await context.SaveChangesAsync();

                    await transaction.CommitAsync();
                });

                task1Completed = true;
            }
            catch (Exception ex)
            {
                task1Exception = ex;
                // Execution strategy will retry transient errors automatically
                // If we get here, either it's not transient or retries exhausted
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                await using var context = new ChatDbContext(
                    ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(ConnectionString),
                    TimeProvider);

                // Use execution strategy which handles transient failures (including deadlocks)
                var strategy = context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();

                    // Lock conv2 first (opposite order)
                    var c2 = await context.Conversations.FirstAsync(c => c.Id == conv2.Id);
                    c2.UpdateTitle("Task2 - Conv2", TimeProvider);
                    await context.SaveChangesAsync();

                    // Small delay to increase deadlock probability
                    await Task.Delay(50);

                    // Then try to lock conv1
                    var c1 = await context.Conversations.FirstAsync(c => c.Id == conv1.Id);
                    c1.UpdateTitle("Task2 - Conv1", TimeProvider);
                    await context.SaveChangesAsync();

                    await transaction.CommitAsync();
                });

                task2Completed = true;
            }
            catch (Exception ex)
            {
                task2Exception = ex;
                // Execution strategy will retry transient errors automatically
                // If we get here, either it's not transient or retries exhausted
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert: With execution strategy retries, at least one should complete successfully
        var anyCompleted = task1Completed || task2Completed;
        anyCompleted.ShouldBeTrue(
            $"At least one transaction should complete. Task1: {task1Completed}, Task2: {task2Completed}. " +
            $"Task1 Exception: {task1Exception?.Message}, Task2 Exception: {task2Exception?.Message}");

        // Log any exceptions for debugging
        if (task1Exception != null || task2Exception != null)
        {
            TestContext.Out.WriteLine($"Task1 completed: {task1Completed}, Exception: {task1Exception?.Message}");
            TestContext.Out.WriteLine($"Task2 completed: {task2Completed}, Exception: {task2Exception?.Message}");
        }

        // Verify data integrity maintained
        ClearChangeTracker();
        var finalConv1 = await ConversationWriteRepository.GetByIdAsync(conv1.Id);
        var finalConv2 = await ConversationWriteRepository.GetByIdAsync(conv2.Id);

        finalConv1.ShouldNotBeNull();
        finalConv2.ShouldNotBeNull();

        // At least one conversation should have been updated
        var conv1Updated = finalConv1!.Title?.Contains("Task") ?? false;
        var conv2Updated = finalConv2!.Title?.Contains("Task") ?? false;

        (conv1Updated || conv2Updated).ShouldBeTrue(
            "At least one update should have succeeded. " +
            $"Conv1 Title: {finalConv1.Title}, Conv2 Title: {finalConv2.Title}");
    }

    #endregion

    #region Helper Methods

    private async Task ForceConstraintViolation(ConversationId conversationId)
    {
        // Try to insert duplicate sequence number to force constraint violation
        await DbContext.Database.ExecuteSqlRawAsync(
            @"INSERT INTO chat.""Messages""
              (conversation_id, id, role, content, sequence, created_at, updated_at, is_deleted)
              VALUES ({0}, {1}, 'User', 'Force fail', 1, NOW(), NOW(), false)",
            conversationId.Value, Guid.NewGuid());
    }

    private static async Task CreateSavepointAsync(IDbContextTransaction transaction, string name)
    {
        await transaction.CreateSavepointAsync(name);
    }

    private static async Task RollbackToSavepointAsync(IDbContextTransaction transaction, string name)
    {
        await transaction.RollbackToSavepointAsync(name);
    }

    private static bool IsDeadlockException(Exception ex)
    {
        // PostgreSQL deadlock detection - check both direct and inner exceptions
        var message = ex.Message + " " + (ex.InnerException?.Message ?? "");

        // Check for PostgreSQL deadlock error code or message
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == "40P01"; // PostgreSQL deadlock code
        }

        return message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
    }

    // Removed SavepointWrapper - not needed with simplified approach

    #endregion
}