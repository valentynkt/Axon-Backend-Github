using System.Diagnostics;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Common.Sorting;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Application.Specifications.Conversations;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Axon.Modules.Chat.Application.Tests.Performance;

/// <summary>
/// Performance benchmarks for GetConversations query optimizations.
/// Validates that database indexes and compiled queries provide expected performance benefits.
/// </summary>
[TestFixture]
[Category("Performance")]
public class GetConversationsPerformanceTests
{
    private PostgreSqlContainer _postgresContainer = null!;
    private ChatReadDbContext _dbContext = null!;
    private ConversationReadRepository _repository = null!;
    private UserId _testUserId = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("chat_perf_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgresContainer.StartAsync();

        // Setup database with indexes
        var options = new DbContextOptionsBuilder<ChatReadDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new ChatReadDbContext(options, NullLogger<ChatReadDbContext>.Instance);
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new ConversationReadRepository(_dbContext);
        _testUserId = UserId.New();

        // Seed substantial test data for performance testing
        await SeedPerformanceDataAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _dbContext.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Test]
    [TestCase(1, 10, Description = "Small page size")]
    [TestCase(1, 50, Description = "Medium page size")]
    [TestCase(10, 20, Description = "Deep pagination")]
    public async Task GetConversations_WithVariousPageSizes_ShouldMeetPerformanceTargets(int pageNumber, int pageSize)
    {
        // Arrange
        var spec = new ConversationsForOwnerSpec(
            _testUserId,
            new Page(pageNumber, pageSize),
            ConversationSortBy.UpdatedAt,
            SortDirection.Desc);

        var countSpec = new ConversationsForOwnerCountSpec(_testUserId);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        
        var dataTask = _repository.ListAsync(spec);
        var countTask = _repository.CountAsync(countSpec);
        
        await Task.WhenAll(dataTask, countTask);
        stopwatch.Stop();

        var data = await dataTask;
        var count = await countTask;

        // Assert Performance Targets
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500, 
            $"Query should complete in under 500ms (Page: {pageNumber}, Size: {pageSize})");
        
        data.Count.ShouldBeLessThanOrEqualTo(pageSize);
        count.ShouldBeGreaterThan(0);

        Console.WriteLine($"Page {pageNumber}, Size {pageSize}: {stopwatch.ElapsedMilliseconds}ms, Results: {data.Count}/{count}");
    }

    [Test]
    public async Task CompiledQueries_VsSpecifications_ShouldShowPerformanceImprovement()
    {
        // Arrange
        const int iterations = 10;
        var compiledTimes = new List<long>();
        var specificationTimes = new List<long>();

        // Warm up queries
        await _repository.GetConversationsForOwnerOptimizedAsync(_testUserId, 0, 10);
        await _repository.ListAsync(new ConversationsForOwnerSpec(_testUserId, new Page(1, 10)));

        // Benchmark Compiled Queries
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await _repository.GetConversationsForOwnerOptimizedAsync(_testUserId, i * 5, 10);
            stopwatch.Stop();
            compiledTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Benchmark Specification Queries
        for (int i = 0; i < iterations; i++)
        {
            var spec = new ConversationsForOwnerSpec(_testUserId, new Page(i + 1, 10));
            var stopwatch = Stopwatch.StartNew();
            await _repository.ListAsync(spec);
            stopwatch.Stop();
            specificationTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Analyze Results
        var avgCompiledTime = compiledTimes.Average();
        var avgSpecTime = specificationTimes.Average();
        var improvementRatio = avgSpecTime / avgCompiledTime;

        Console.WriteLine($"Compiled Query Average: {avgCompiledTime:F2}ms");
        Console.WriteLine($"Specification Average: {avgSpecTime:F2}ms");
        Console.WriteLine($"Performance Improvement Ratio: {improvementRatio:F2}x");

        // Assert - Compiled queries should be at least as fast, often faster
        avgCompiledTime.ShouldBeLessThanOrEqualTo(avgSpecTime * 1.2, 
            "Compiled queries should perform at least as well as specifications");
    }

    [Test]
    public async Task TitleSearch_WithFullTextSearch_ShouldPerformWell()
    {
        // Arrange
        var searchTerms = new[] { "performance", "chat", "test", "AI model", "discussion" };
        var searchTimes = new List<long>();

        // Act & Measure
        foreach (var term in searchTerms)
        {
            var spec = new ConversationsForOwnerSpec(
                _testUserId,
                new Page(1, 20),
                titleContains: term);

            var stopwatch = Stopwatch.StartNew();
            var results = await _repository.ListAsync(spec);
            stopwatch.Stop();

            searchTimes.Add(stopwatch.ElapsedMilliseconds);
            
            Console.WriteLine($"Search '{term}': {stopwatch.ElapsedMilliseconds}ms, Results: {results.Count}");
        }

        // Assert
        var avgSearchTime = searchTimes.Average();
        avgSearchTime.ShouldBeLessThan(200, "Title search should complete quickly with full-text search");
    }

    [Test]
    public async Task ConcurrentQueries_ShouldMaintainPerformance()
    {
        // Arrange
        const int concurrentQueries = 50;
        var tasks = new List<Task<(long ElapsedMs, int ResultCount)>>();

        // Act - Execute concurrent queries
        for (int i = 0; i < concurrentQueries; i++)
        {
            var pageNumber = (i % 10) + 1;
            var task = ExecuteTimedQueryAsync(pageNumber, 10);
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        var avgTime = results.Select(r => r.ElapsedMs).Average();
        var maxTime = results.Select(r => r.ElapsedMs).Max();

        Console.WriteLine($"Concurrent queries - Average: {avgTime:F2}ms, Max: {maxTime}ms");
        
        avgTime.ShouldBeLessThan(1000, "Average response time should remain reasonable under load");
        maxTime.ShouldBeLessThan(3000, "Maximum response time should not exceed acceptable threshold");
    }

    [Test]
    public async Task DeepPagination_ShouldRemainEfficient()
    {
        // Arrange & Act - Test pagination at different depths
        var paginationTests = new[]
        {
            (Page: 1, Size: 20),
            (Page: 10, Size: 20),
            (Page: 50, Size: 20),
            (Page: 100, Size: 20)
        };

        var results = new List<(int Page, long ElapsedMs)>();

        foreach (var (page, size) in paginationTests)
        {
            var spec = new ConversationsForOwnerSpec(
                _testUserId,
                new Page(page, size),
                ConversationSortBy.UpdatedAt,
                SortDirection.Desc);

            var stopwatch = Stopwatch.StartNew();
            var data = await _repository.ListAsync(spec);
            stopwatch.Stop();

            results.Add((page, stopwatch.ElapsedMilliseconds));
            Console.WriteLine($"Page {page}: {stopwatch.ElapsedMilliseconds}ms, Results: {data.Count}");
        }

        // Assert - Performance should not degrade significantly with deep pagination
        // This validates our composite indexes are working effectively
        var firstPageTime = results.First().ElapsedMs;
        var deepestPageTime = results.Last().ElapsedMs;
        
        var degradationRatio = (double)deepestPageTime / firstPageTime;
        degradationRatio.ShouldBeLessThan(3.0, 
            "Deep pagination should not be more than 3x slower than first page due to proper indexing");
    }

    private async Task<(long ElapsedMs, int ResultCount)> ExecuteTimedQueryAsync(int pageNumber, int pageSize)
    {
        var spec = new ConversationsForOwnerSpec(
            _testUserId,
            new Page(pageNumber, pageSize));

        var stopwatch = Stopwatch.StartNew();
        var results = await _repository.ListAsync(spec);
        stopwatch.Stop();

        return (stopwatch.ElapsedMilliseconds, results.Count);
    }

    /// <summary>
    /// Seeds substantial performance test data.
    /// Creates enough data to validate index effectiveness and query performance.
    /// </summary>
    private async Task SeedPerformanceDataAsync()
    {
        const int totalConversations = 10000; // Substantial dataset for performance testing
        const int batchSize = 500;

        var random = new Random(42); // Fixed seed for reproducible tests
        var topics = new[] 
        {
            "AI model discussion", "Performance optimization", "Chat system design",
            "Database indexing", "Query performance", "Full-text search",
            "Pagination strategies", "Microservices architecture", "Clean code practices",
            "Test-driven development", "System monitoring", "Load balancing"
        };

        for (int batch = 0; batch < totalConversations / batchSize; batch++)
        {
            var conversations = new List<Conversation>();
            
            for (int i = 0; i < batchSize; i++)
            {
                var conversationIndex = batch * batchSize + i;
                var topic = topics[conversationIndex % topics.Length];
                var title = $"{topic} - Conversation {conversationIndex:D5}";
                
                var conversation = Conversation.StartNew(_testUserId, title);
                conversations.Add(conversation);
            }

            _dbContext.Set<Conversation>().AddRange(conversations);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            // Progress indication for long-running seed operation
            if (batch % 5 == 0)
            {
                Console.WriteLine($"Seeded {(batch + 1) * batchSize} conversations...");
            }
        }

        Console.WriteLine($"Performance test data seeding complete: {totalConversations} conversations");
    }
}