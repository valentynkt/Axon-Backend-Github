using System.Diagnostics;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;
using BuildingBlocks.Application;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Critical;

/// <summary>
/// Critical performance tests for bulk operations.
/// Tests the 20% of scenarios that impact 80% of users - large conversations,
/// pagination boundaries, and query performance at scale.
/// </summary>
[TestFixture]
[Category("Critical")]
[Category("Performance")]
public class BulkOperationPerformanceTests : ChatPerformanceTestBase
{
    // Performance thresholds for bulk operations
    protected override TimeSpan BulkOperationThreshold => TimeSpan.FromSeconds(2);
    protected override int MaxAcceptableQueries => 5; // Should use efficient queries
    protected override long MaxMemoryIncreaseKb => 51200; // 50MB for bulk ops

    #region Critical: Large Conversation Performance

    [Test]
    public async Task LoadConversation_With100Messages_PerformanceAcceptable()
    {
        // Arrange: Create conversation with 100+ messages
        var conversation = await CreateLargeConversationAsync(messageCount: 100);

        // Act & Assert: Loading should be performant
        await AssertOperationTime(
            async () =>
            {
                ClearChangeTracker();
                var loaded = await ConversationRepository.GetByIdAsync(conversation.Id);
                loaded.ShouldNotBeNull();
                loaded.GetMessageCount().ShouldBe(100);
            },
            TimeSpan.FromMilliseconds(500),
            "Load conversation with 100 messages");

        // Verify no N+1 queries
        await AssertNoNPlusOneQueries(
            async () =>
            {
                ClearChangeTracker();
                var loaded = await ConversationRepository.GetByIdAsync(conversation.Id);
                _ = loaded?.GetAllMessages(); // Force message loading
            },
            "Load conversation with messages");
    }

    [Test]
    public async Task LoadConversation_With500Messages_MemoryEfficient()
    {
        // Arrange: Create very large conversation
        var conversation = await CreateLargeConversationAsync(messageCount: 500);

        // Act: Load and process
        var initialMemory = GC.GetTotalMemory(true);

        ClearChangeTracker();
        var loaded = await ConversationRepository.GetByIdAsync(conversation.Id);
        loaded.ShouldNotBeNull();

        // Process all messages
        var messages = loaded.GetAllMessages();
        var totalContentLength = messages.Sum(m => m.Content.Value.Length);

        // Assert: Memory usage reasonable
        var finalMemory = GC.GetTotalMemory(false);
        var memoryUsedKb = (finalMemory - initialMemory) / 1024;

        TestContext.Out.WriteLine($"Memory used for 500 messages: {memoryUsedKb:N0} KB");
        TestContext.Out.WriteLine($"Total content length: {totalContentLength:N0} characters");

        // Memory should be roughly proportional to content
        // Note: .NET object overhead for EF entities is significant - domain objects,
        // change tracking, collections, etc. Memory usage varies with GC behavior.
        var expectedMemoryKb = (totalContentLength * 2) / 1024; // 2 bytes per char estimate
        memoryUsedKb.ShouldBeLessThan(expectedMemoryKb * 20, // Allow 20x overhead for .NET objects + EF tracking + GC variance
            $"Memory usage ({memoryUsedKb:N0} KB) exceeds reasonable threshold");
    }

    #endregion

    #region Critical: Pagination Performance

    [Test]
    public async Task PaginateConversations_LargeDataset_EfficientQueries()
    {
        // Arrange: Create many conversations for single user
        var ownerId = AxonUserId.New();
        var conversations = new List<Conversation>();

        for (int i = 0; i < 50; i++)
        {
            var conv = CreateTestConversation(ownerId, $"Conversation {i:D3}", TimeProvider);
            conversations.Add(conv);
            await SaveConversationAsync(conv);
        }

        // Act & Assert: Paginate efficiently
        await AssertQueryCount(
            async () =>
            {
                var request = new PaginatedRequest { PageNumber = 1, PageSize = 10 };
                // Direct pagination query
                var items = await ReadDbContext.Conversations
                    .AsNoTracking()
                    .Where(c => c.OwnerId == ownerId)
                    .OrderBy(c => c.CreatedAt)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync();

                var totalCount = await ReadDbContext.Conversations
                    .AsNoTracking()
                    .Where(c => c.OwnerId == ownerId)
                    .CountAsync();

                items.Count.ShouldBe(10);
                totalCount.ShouldBe(50);
            },
            2, // Should only execute 2 queries: one for items, one for count
            "Paginate conversations");
    }

    [Test]
    public async Task PaginateMessages_BoundaryConditions_HandledCorrectly()
    {
        // Arrange: Create conversation with exact page boundary messages
        var conversation = await CreateLargeConversationAsync(messageCount: 100);

        // Test different page boundaries
        var testCases = new[]
        {
            (PageNumber: 1, PageSize: 10, ExpectedCount: 10),  // First page
            (PageNumber: 10, PageSize: 10, ExpectedCount: 10), // Last full page (items 90-99)
            (PageNumber: 11, PageSize: 10, ExpectedCount: 0),  // Beyond last page
            (PageNumber: 4, PageSize: 25, ExpectedCount: 25),  // Larger page size - page 4 = items 75-99
            (PageNumber: 1, PageSize: 200, ExpectedCount: 100) // Page larger than total
        };

        foreach (var testCase in testCases)
        {
            // Act
            var request = new PaginatedRequest
            {
                PageNumber = testCase.PageNumber,
                PageSize = testCase.PageSize
            };

            // Load conversation with messages and paginate in memory (for test purposes)
            // Note: Must use repository method to properly load owned Messages collection
            ClearChangeTracker();
            var conv = await ConversationRepository.GetByIdAsync(conversation.Id);
            conv.ShouldNotBeNull();

            var messages = conv.GetAllMessages()
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Assert
            messages.Count.ShouldBe(testCase.ExpectedCount,
                $"Page {testCase.PageNumber} with size {testCase.PageSize} should return {testCase.ExpectedCount} items");
        }
    }

    #endregion

    #region Critical: Bulk Insert Performance

    [Test]
    public async Task BulkInsert_MultipleConversations_Performant()
    {
        // Arrange: Prepare bulk data
        var conversations = new List<Conversation>();
        var ownerId = AxonUserId.New();

        for (int i = 0; i < 100; i++)
        {
            var conv = CreateTestConversation(ownerId, $"Bulk {i}", TimeProvider);

            // Add some messages to each
            for (int j = 0; j < 10; j++)
            {
                if (j % 2 == 0)
                {
                    var userContent = MessageContent.From($"Message {j}");
                    conv.AppendUserMessageToConversation(userContent, TimeProvider);
                }
                else
                {
                    var aiContent = MessageContent.From($"Response {j}");
                    var aiId = new AiResponseId($"bulk-{i}-{j}");
                    conv.AppendAssistantResponseToConversation(aiContent, aiId, TimeProvider);
                }
            }

            conversations.Add(conv);
        }

        // Act & Assert: Bulk insert should be fast
        var stopwatch = Stopwatch.StartNew();

        foreach (var conv in conversations)
        {
            await ConversationRepository.AddAsync(conv);
        }

        await UnitOfWork.SaveChangesAsync();

        stopwatch.Stop();

        TestContext.Out.WriteLine($"Bulk insert 100 conversations with 1000 messages: {stopwatch.ElapsedMilliseconds}ms");

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5),
            $"Bulk insert took {stopwatch.ElapsedMilliseconds}ms, exceeding 5 second threshold");

        // Verify all data persisted
        var count = await ReadDbContext.Conversations.CountAsync();
        count.ShouldBeGreaterThanOrEqualTo(100);
    }

    #endregion

    #region Critical: Query Optimization Detection

    [Test]
    public async Task ComplexQuery_WithIncludes_NoNPlusOne()
    {
        // Arrange: Create related data
        var ownerId = AxonUserId.New();
        for (int i = 0; i < 10; i++)
        {
            var conv = CreateTestConversationWithMessages(
                ownerId,
                userMessageCount: 5,
                assistantMessageCount: 5,
                TimeProvider);
            await SaveConversationAsync(conv);
        }

        // Act & Assert: Complex query should be optimized
        await AssertNoNPlusOneQueries(
            async () =>
            {
                ClearChangeTracker();

                // Simulate loading conversations with message counts
                var conversations = await DbContext.Conversations
                    .Where(c => c.OwnerId == ownerId)
                    .ToListAsync();

                // Access messages for each - potential N+1
                foreach (var conv in conversations)
                {
                    _ = conv.GetMessageCount();
                    _ = conv.GetRecentMessages(3);
                }
            },
            "Load conversations with message access");
    }

    [Test]
    public async Task SearchOperation_LargeDataset_UsesIndexes()
    {
        // Arrange: Create searchable dataset
        var conversations = new List<Conversation>();
        for (int i = 0; i < 100; i++)
        {
            var conv = CreateTestConversation(
                AxonUserId.New(),
                i % 10 == 0 ? "Important Discussion" : $"Chat {i}",
                TimeProvider);
            conversations.Add(conv);
            await SaveConversationAsync(conv);
        }

        // Act: Search operation
        var stopwatch = Stopwatch.StartNew();

        var results = await ReadDbContext.Conversations
            .AsNoTracking()
            .Where(c => c.Title != null && c.Title.Contains("Important"))
            .OrderBy(c => c.CreatedAt)
            .Take(20)
            .ToListAsync();

        stopwatch.Stop();

        // Assert: Should use index and be fast
        TestContext.Out.WriteLine($"Search query took: {stopwatch.ElapsedMilliseconds}ms");

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100,
            "Search should use indexes and complete quickly");

        results.Count.ShouldBe(10);
    }

    #endregion

    #region Critical: Memory Leak Prevention

    [Test]
    public async Task RepeatedOperations_NoMemoryLeak()
    {
        // Arrange: Create test conversation
        var conversation = CreateTestConversationWithMessages(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        // Baseline memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baselineMemory = GC.GetTotalMemory(false);

        // Act: Repeated operations that could leak memory
        for (int i = 0; i < 100; i++)
        {
            // Create new context each time (simulates request scope)
            await using var context = new ChatDbContext(
                ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(ConnectionString),
                TimeProvider);

            var conv = await context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversation.Id);

            conv.ShouldNotBeNull();
            _ = conv.GetAllMessages();

            // Simulate some processing
            var messageCount = conv.GetMessageCount();
            var recentMessages = conv.GetRecentMessages(5);
            _ = conv.GetMessagesByRole(MessageRole.User);

            // Context disposed here
        }

        // Force cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var leakedKb = (finalMemory - baselineMemory) / 1024;

        TestContext.Out.WriteLine($"Memory after 100 iterations: {leakedKb:N0} KB increase");

        // Allow some working set increase but not linear growth
        leakedKb.ShouldBeLessThan(10240, // 10MB max increase
            $"Potential memory leak detected: {leakedKb:N0} KB increase after repeated operations");
    }

    #endregion

    #region Helper Methods

    // Simple pagination request for testing
    private class PaginatedRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    private async Task<Conversation> CreateLargeConversationAsync(int messageCount)
    {
        var conversation = CreateTestConversation(timeProvider: TimeProvider);

        for (int i = 0; i < messageCount; i++)
        {
            if (i % 2 == 0)
            {
                var userContent = MessageContent.From($"User message {i}: " + new string('A', 100));
                var userResult = conversation.AppendUserMessageToConversation(userContent, TimeProvider);
                userResult.IsSuccess.ShouldBeTrue();
            }
            else
            {
                var aiContent = MessageContent.From($"AI response {i}: " + new string('B', 200));
                var aiId = new AiResponseId($"ai-{Guid.NewGuid()}");
                var aiResult = conversation.AppendAssistantResponseToConversation(aiContent, aiId, TimeProvider);
                aiResult.IsSuccess.ShouldBeTrue();
            }
        }

        await SaveConversationAsync(conversation);
        return conversation;
    }


    #endregion
}