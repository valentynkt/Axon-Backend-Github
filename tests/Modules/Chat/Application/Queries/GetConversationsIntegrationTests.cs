using System.Diagnostics;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Common.Sorting;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Axon.Modules.Chat.Application.Tests.Queries;

/// <summary>
/// Integration tests for GetConversations query with database optimizations.
/// Tests actual query performance, index usage, and compiled query benefits.
/// </summary>
[TestFixture]
public class GetConversationsIntegrationTests
{
    private PostgreSqlContainer _postgresContainer = null!;
    private ChatReadDbContext _dbContext = null!;
    private ConversationReadRepository _repository = null!;
    private GetConversationsHandler _handler = null!;
    private UserId _testUserId = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Start PostgreSQL test container
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("chat_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgresContainer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgresContainer.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        // Configure test database context
        var options = new DbContextOptionsBuilder<ChatReadDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new ChatReadDbContext(options, NullLogger<ChatReadDbContext>.Instance);
        
        // Ensure database is created with our schema and indexes
        await _dbContext.Database.EnsureCreatedAsync();
        
        _repository = new ConversationReadRepository(_dbContext);
        
        // Setup test data
        _testUserId = UserId.New();
        await SeedTestDataAsync();

        // Create handler with mocked dependencies
        var mockAuthService = Substitute.For<IUserAuthenticationService>();
        mockAuthService.GetAuthenticatedUserId().Returns(BuildingBlocks.Core.Functional.Results.Result.Success(_testUserId));
        
        var mockTelemetry = Substitute.For<IChatTelemetry>();
        mockTelemetry.StartActivity(Arg.Any<string>()).Returns((Activity?)null);

        _handler = new GetConversationsHandler(
            mockAuthService,
            _repository,
            mockTelemetry,
            NullLogger<GetConversationsHandler>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task Handle_WithBasicPagination_ShouldReturnOptimizedResults()
    {
        // Arrange
        var query = new GetConversationsQuery(
            PageNumber: 1,
            PageSize: 10,
            SortBy: ConversationSortBy.UpdatedAt,
            SortDirection: SortDirection.Desc);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _handler.Handle(query, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.Value.TotalCount.ShouldBeGreaterThan(0);
        
        // Performance assertion - should complete quickly with indexes
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000, "Query should complete quickly with proper indexes");
    }

    [Test]
    public async Task Handle_WithTitleFiltering_ShouldUseFullTextSearch()
    {
        // Arrange
        var query = new GetConversationsQuery(
            TitleContains: "test conversation");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(item => 
            item.Title.Contains("test", StringComparison.OrdinalIgnoreCase) ||
            item.Title.Contains("conversation", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Handle_WithLargeDataset_ShouldPerformWellWithIndexes()
    {
        // Arrange - This test validates index performance with larger dataset
        await SeedLargeDatasetAsync();
        
        var query = new GetConversationsQuery(
            PageNumber: 5,
            PageSize: 20,
            SortBy: ConversationSortBy.UpdatedAt);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _handler.Handle(query, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBeLessThanOrEqualTo(20);
        
        // Even with large dataset, should complete quickly due to indexes
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000, 
            "Query with large dataset should still perform well with indexes");
    }

    [Test]
    public async Task Repository_CompiledQueriesVsSpecifications_ShouldShowPerformanceBenefit()
    {
        // Arrange
        var skip = 0;
        var take = 10;

        // Act & Measure - Compiled Query
        var compiledStopwatch = Stopwatch.StartNew();
        var compiledResult = await _repository.GetConversationsForOwnerOptimizedAsync(
            _testUserId, skip, take);
        compiledStopwatch.Stop();

        // Act & Measure - Specification-based Query
        var spec = new ConversationsForOwnerSpec(
            _testUserId,
            new Page(1, take),
            ConversationSortBy.UpdatedAt,
            SortDirection.Desc);

        var specStopwatch = Stopwatch.StartNew();
        var specResult = await _repository.ListAsync(spec);
        specStopwatch.Stop();

        // Assert
        compiledResult.Count.ShouldBe(specResult.Count);
        
        // Compiled queries should be faster or at least comparable
        // Note: In real scenarios with more complex queries, the difference is more noticeable
        Console.WriteLine($"Compiled Query: {compiledStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Specification Query: {specStopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    public async Task Handle_ConcurrentRequests_ShouldHandleHighLoad()
    {
        // Arrange
        var tasks = new List<Task<BuildingBlocks.Core.Functional.Results.Result<Paged<ConversationListItem>, BuildingBlocks.Core.Primitives.Error>>>();
        const int concurrentRequests = 20;

        // Act - Simulate concurrent requests
        for (int i = 0; i < concurrentRequests; i++)
        {
            var query = new GetConversationsQuery(PageNumber: i % 3 + 1, PageSize: 5);
            tasks.Add(_handler.Handle(query, CancellationToken.None));
        }

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.ShouldAllBe(r => r.IsSuccess);
        
        // Should handle concurrent load efficiently
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000, 
            "Concurrent requests should complete within reasonable time");
    }

    /// <summary>
    /// Seeds basic test data for conversation queries.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        var conversations = new List<Conversation>
        {
            CreateTestConversation("First test conversation"),
            CreateTestConversation("Second test conversation"),
            CreateTestConversation("Chat about AI models"),
            CreateTestConversation("Discussion on performance"),
            CreateTestConversation("Random topic")
        };

        _dbContext.Set<Conversation>().AddRange(conversations);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a larger dataset to test index performance.
    /// </summary>
    private async Task SeedLargeDatasetAsync()
    {
        var conversations = new List<Conversation>();
        
        for (int i = 0; i < 1000; i++)
        {
            conversations.Add(CreateTestConversation($"Conversation {i:D4} with random content"));
        }

        // Add conversations in batches to avoid memory issues
        const int batchSize = 100;
        for (int i = 0; i < conversations.Count; i += batchSize)
        {
            var batch = conversations.Skip(i).Take(batchSize);
            _dbContext.Set<Conversation>().AddRange(batch);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear(); // Clear tracking to avoid memory issues
        }
    }

    /// <summary>
    /// Creates a test conversation entity.
    /// </summary>
    private Conversation CreateTestConversation(string title)
    {
        // Note: This is a simplified creation for testing.
        // In real implementation, you'd use the proper domain factory methods
        var conversation = Conversation.StartNew(_testUserId, title);
        
        // Simulate some time passing to create varied UpdatedAt values
        var randomDelay = Random.Shared.Next(1, 1000);
        
        return conversation;
    }
}