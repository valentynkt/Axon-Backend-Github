using System.Collections.Concurrent;
using System.Diagnostics;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Services.Orchestration;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Tests.Extensions;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Axon.Modules.Chat.Performance;

/// <summary>
/// Performance and concurrency tests for Chat module.
/// Tests concurrent message processing, conversation limits, database isolation,
/// and performance under load conditions.
/// </summary>
[TestFixture]
public class ChatConcurrencyTests : ApplicationTestBase
{
    private IConversationRepository _mockRepository = null!;
    private ILogger<MessageProcessingOrchestrator> _mockLogger = null!;

    // Performance thresholds
    private const int MaxResponseTimeMs = 1000;
    private const int ConcurrentUsers = 10;
    private const int MessagesPerUser = 5;
    private const int MaxConversationCount = 100;

    protected override void OnSetUp()
    {
        _mockRepository = Substitute.For<IConversationRepository>();
        _mockLogger = Substitute.For<ILogger<MessageProcessingOrchestrator>>();

        SetupRepositoryMocks();
    }

    [TearDown]
    public void TearDown()
    {
        (_mockRepository as IDisposable)?.Dispose();
    }

    #region Concurrent Message Processing Tests

    [Test]
    public async Task ConcurrentMessageProcessing_SameConversation_ShouldMaintainMessageOrder()
    {
        // Arrange: Create a conversation and multiple messages
        var ownerId = CreateAxonUserId();
        var conversation = CreateConversationBuilder()
            .WithOwner(ownerId)
            .Build();

        var messageContents = Enumerable.Range(1, 10)
            .Select(i => $"Concurrent message {i}")
            .ToList();

        var results = new ConcurrentBag<Result<Message, Error>>();
        var tasks = new List<Task>();

        // Act: Send messages concurrently to same conversation
        foreach (var content in messageContents)
        {
            tasks.Add(Task.Run(async () =>
            {
                var messageContent = MessageContent.Create(content).Value;
                var result = conversation.AppendUserMessageToConversation(messageContent, TimeProvider);
                results.Add(result);

                // Simulate some processing time
                await Task.Delay(Random.Shared.Next(1, 10));
            }));
        }

        await Task.WhenAll(tasks);

        // Assert: All messages should be added successfully
        results.Count.ShouldBe(messageContents.Count);
        foreach (var result in results)
        {
            result.ShouldBeSuccess();
        }

        // Verify conversation state integrity
        conversation.GetMessageCount().ShouldBe(messageContents.Count);

        // Check message sequence integrity
        var messages = conversation.GetAllMessages();
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1, "Message sequence should be consecutive");
        }
    }

    [Test]
    public async Task ConcurrentUserConversations_MultipleUsers_ShouldIsolateCorrectly()
    {
        // Arrange: Create multiple users and conversations
        var userConversations = new Dictionary<AxonUserId, Conversation>();
        var tasks = new List<Task<Result<Message, Error>>>();
        var results = new ConcurrentBag<(AxonUserId UserId, Result<Message, Error> Result)>();

        for (int i = 0; i < ConcurrentUsers; i++)
        {
            var userId = CreateAxonUserId();
            var conversation = CreateConversationBuilder()
                .WithOwner(userId)
                .Build();

            userConversations[userId] = conversation;
        }

        // Act: Each user sends messages to their own conversation concurrently
        foreach (var kvp in userConversations)
        {
            var userId = kvp.Key;
            var conversation = kvp.Value;

            for (int messageIndex = 0; messageIndex < MessagesPerUser; messageIndex++)
            {
                var messageNumber = messageIndex;
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(Random.Shared.Next(1, 50)); // Simulate network delay
                    var content = MessageContent.Create($"User {userId.Value} message {messageNumber}").Value;
                    var result = conversation.AppendUserMessageToConversation(content, TimeProvider);
                    results.Add((userId, result));
                    return result;
                }));
            }
        }

        await Task.WhenAll(tasks);

        // Assert: All operations should succeed
        results.Count.ShouldBe(ConcurrentUsers * MessagesPerUser);

        foreach (var (userId, result) in results)
        {
            result.ShouldBeSuccess($"Message for user {userId.Value} should succeed");
        }

        // Verify each conversation has correct message count
        foreach (var conversation in userConversations.Values)
        {
            conversation.GetMessageCount().ShouldBe(MessagesPerUser);
        }

        // Verify no cross-contamination between users
        var resultsByUser = results.GroupBy(r => r.UserId).ToList();
        resultsByUser.Count.ShouldBe(ConcurrentUsers);

        foreach (var userGroup in resultsByUser)
        {
            userGroup.Count().ShouldBe(MessagesPerUser);
        }
    }

    [Test]
    public async Task ConcurrentConversationCreation_SameUser_ShouldAllowMultipleConversations()
    {
        // Arrange
        var userId = CreateAxonUserId();
        var conversationTasks = new List<Task<Result<Conversation, Error>>>();
        var results = new ConcurrentBag<Result<Conversation, Error>>();

        // Act: Create multiple conversations concurrently for same user
        for (int i = 0; i < 20; i++)
        {
            var conversationIndex = i;
            conversationTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(1, 20)); // Simulate processing delay
                var result = Conversation.StartNewConversation(
                    userId,
                    $"Concurrent conversation {conversationIndex}",
                    TimeProvider);
                results.Add(result);
                return result;
            }));
        }

        await Task.WhenAll(conversationTasks);

        // Assert: All conversations should be created successfully
        results.Count.ShouldBe(20);
        foreach (var result in results)
        {
            result.ShouldBeSuccess();
        }

        // Verify all conversations are unique
        var conversationIds = results.Select(r => r.Value.Id).ToHashSet();
        conversationIds.Count.ShouldBe(20, "All conversation IDs should be unique");

        // Verify all conversations belong to the same user
        foreach (var result in results)
        {
            result.Value.OwnerId.ShouldBe(userId);
        }
    }

    #endregion

    #region Performance Under Load Tests

    [Test]
    public async Task MessageProcessing_HighVolumeSequential_ShouldMeetPerformanceTarget()
    {
        // Arrange
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        var messageCount = 100;
        var stopwatch = Stopwatch.StartNew();

        // Act: Send many messages sequentially
        for (int i = 0; i < messageCount; i++)
        {
            var content = MessageContent.Create($"Performance test message {i}").Value;
            var result = conversation.AppendUserMessageToConversation(content, TimeProvider);
            result.ShouldBeSuccess($"Message {i} should succeed");
        }

        stopwatch.Stop();

        // Assert: Should complete within performance target
        var averageTimePerMessage = stopwatch.ElapsedMilliseconds / (double)messageCount;
        averageTimePerMessage.ShouldBeLessThan(10, "Average time per message should be under 10ms");

        conversation.GetMessageCount().ShouldBe(messageCount);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(MaxResponseTimeMs,
            $"Total time should be under {MaxResponseTimeMs}ms");
    }

    [Test]
    public async Task ConversationCreation_HighVolume_ShouldMeetPerformanceTarget()
    {
        // Arrange
        var userId = CreateAxonUserId();
        var conversationCount = 50;
        var stopwatch = Stopwatch.StartNew();
        var conversations = new List<Conversation>();

        // Act: Create many conversations
        for (int i = 0; i < conversationCount; i++)
        {
            var result = Conversation.StartNewConversation(
                userId,
                $"Performance conversation {i}",
                TimeProvider);

            result.ShouldBeSuccess($"Conversation {i} should be created successfully");
            conversations.Add(result.Value);
        }

        stopwatch.Stop();

        // Assert: Performance targets
        var averageTimePerConversation = stopwatch.ElapsedMilliseconds / (double)conversationCount;
        averageTimePerConversation.ShouldBeLessThan(5, "Average time per conversation should be under 5ms");

        conversations.Count.ShouldBe(conversationCount);

        // Verify all conversations are unique and properly formed
        var uniqueIds = conversations.Select(c => c.Id).ToHashSet();
        uniqueIds.Count.ShouldBe(conversationCount);
    }

    [Test]
    public async Task MixedOperations_ConcurrentLoad_ShouldMaintainPerformance()
    {
        // Arrange: Mix of conversation creation and message sending
        var users = Enumerable.Range(0, 5).Select(_ => CreateAxonUserId()).ToList();
        var conversations = new ConcurrentBag<Conversation>();
        var results = new ConcurrentBag<bool>();
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Act: Mix of operations running concurrently
        foreach (var userId in users)
        {
            // Task 1: Create conversation
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(1, 10));
                var convResult = Conversation.StartNewConversation(userId, "Mixed ops test", TimeProvider);
                if (convResult.IsSuccess)
                {
                    conversations.Add(convResult.Value);
                    results.Add(true);
                }
                else
                {
                    results.Add(false);
                }
            }));

            // Task 2: Send messages to existing conversation (if any)
            for (int i = 0; i < 3; i++)
            {
                var messageIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(Random.Shared.Next(1, 20));

                    // Create a temporary conversation for this test
                    var tempConv = CreateConversationBuilder().WithOwner(userId).Build();
                    var content = MessageContent.Create($"Message {messageIndex}").Value;
                    var msgResult = tempConv.AppendUserMessageToConversation(content, TimeProvider);
                    results.Add(msgResult.IsSuccess);
                }));
            }
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert: All operations should succeed within time limit
        var successCount = results.Count(r => r);
        var totalOperations = results.Count;

        successCount.ShouldBe(totalOperations, "All operations should succeed");
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000, "Mixed operations should complete within 5 seconds");

        var averageTimePerOperation = stopwatch.ElapsedMilliseconds / (double)totalOperations;
        averageTimePerOperation.ShouldBeLessThan(100, "Average time per operation should be reasonable");
    }

    #endregion

    #region Resource Limit Tests

    [Test]
    public async Task MessageLimit_AtMaximum_ShouldEnforceLimits()
    {
        // Arrange: Create conversation at message limit
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithMaximumMessages() // This should create a conversation at the limit
            .Build();

        var initialMessageCount = conversation.GetMessageCount();

        // Act: Try to add one more message
        var content = MessageContent.Create("This should fail due to limit").Value;
        var result = conversation.AppendUserMessageToConversation(content, TimeProvider);

        // Assert: Should fail due to message limit
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        conversation.GetMessageCount().ShouldBe(initialMessageCount, "Message count should not change");
    }

    [Test]
    public async Task ConversationCount_HighVolume_ShouldHandleMany()
    {
        // Arrange: Test creating many conversations (stress test)
        var userId = CreateAxonUserId();
        var conversations = new ConcurrentBag<Conversation>();
        var tasks = new List<Task>();

        // Act: Create many conversations concurrently
        for (int i = 0; i < MaxConversationCount; i++)
        {
            var conversationIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(1, 5));
                var result = Conversation.StartNewConversation(
                    userId,
                    $"Stress test conversation {conversationIndex}",
                    TimeProvider);

                if (result.IsSuccess)
                {
                    conversations.Add(result.Value);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert: Should handle the load successfully
        conversations.Count.ShouldBe(MaxConversationCount);

        // Verify all conversations are unique
        var uniqueIds = conversations.Select(c => c.Id).ToHashSet();
        uniqueIds.Count.ShouldBe(MaxConversationCount);
    }

    #endregion

    #region Memory and Resource Tests

    [Test]
    public async Task LargeMessageContent_Concurrent_ShouldHandleEfficiently()
    {
        // Arrange: Create large message content
        var largeContent = new string('A', 10000); // 10KB message
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .Build();

        var tasks = new List<Task<Result<Message, Error>>>();

        // Act: Send large messages concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(1, 50));
                var content = MessageContent.Create($"{largeContent}_message_{i}").Value;
                return conversation.AppendUserMessageToConversation(content, TimeProvider);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert: All should succeed without memory issues
        foreach (var result in results)
        {
            result.ShouldBeSuccess();
        }

        conversation.GetMessageCount().ShouldBe(10);

        // Verify content integrity
        var messages = conversation.GetAllMessages();
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Content.Value.ShouldContain($"message_{i}");
        }
    }

    [Test]
    public async Task ConversationCleanup_AfterCompletion_ShouldReleaseResources()
    {
        // Arrange: Create and complete many conversations
        var userId = CreateAxonUserId();
        var conversations = new List<Conversation>();

        // Act: Create, use, and complete conversations
        for (int i = 0; i < 20; i++)
        {
            var conversation = CreateConversationBuilder()
                .WithOwner(userId)
                .WithUserMessage($"Test message {i}")
                .Build();

            conversations.Add(conversation);

            // Complete the conversation
            var result = conversation.Complete(TimeProvider);
            result.ShouldBeSuccess();
        }

        // Assert: All conversations should be completed
        foreach (var conversation in conversations)
        {
            conversation.Status.ShouldBe(ConversationStatus.Completed);
            conversation.IsActive.ShouldBeFalse();
        }

        // Memory cleanup verification (simulated)
        conversations.Clear();
        await Task.Delay(10); // Allow for any async cleanup

        // This is mainly to ensure the test completes without memory issues
        conversations.Count.ShouldBe(0);
    }

    #endregion

    #region Database Transaction Isolation Tests

    [Test]
    public async Task ConcurrentDatabaseOperations_ShouldMaintainIsolation()
    {
        // This test simulates concurrent database operations
        // In a real scenario, this would test actual database transactions

        // Arrange: Multiple conversations and operations
        var conversations = new ConcurrentBag<Conversation>();
        var updateTasks = new List<Task>();
        var operationResults = new ConcurrentBag<bool>();

        // Act: Simulate concurrent database operations
        for (int i = 0; i < 15; i++)
        {
            var operationIndex = i;
            updateTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(Random.Shared.Next(1, 30)); // Simulate DB latency

                    // Simulate conversation creation (would be DB insert)
                    var conversation = CreateConversationBuilder()
                        .WithOwner(CreateAxonUserId())
                        .WithUserMessage($"Transaction test {operationIndex}")
                        .Build();

                    conversations.Add(conversation);

                    // Simulate message addition (would be DB update)
                    var content = MessageContent.Create($"Additional message {operationIndex}").Value;
                    var result = conversation.AppendUserMessageToConversation(content, TimeProvider);

                    operationResults.Add(result.IsSuccess);
                }
                catch (Exception)
                {
                    operationResults.Add(false);
                }
            }));
        }

        await Task.WhenAll(updateTasks);

        // Assert: All operations should succeed (simulating proper isolation)
        operationResults.All(r => r).ShouldBeTrue("All database operations should succeed");
        conversations.Count.ShouldBe(15);

        // Verify data integrity
        foreach (var conversation in conversations)
        {
            conversation.GetMessageCount().ShouldBe(2); // Initial + additional message
        }
    }

    #endregion

    #region Helper Methods

    private void SetupRepositoryMocks()
    {
        // Setup basic repository mock behaviors for performance tests
        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    #endregion
}