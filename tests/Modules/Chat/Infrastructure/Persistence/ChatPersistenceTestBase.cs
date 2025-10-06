using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Common.Models;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Testing;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MassTransit;
using NUnit.Framework;
using Shouldly;
using Npgsql;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

/// <summary>
/// Base class for all Chat persistence tests providing shared infrastructure and utilities.
/// Uses PostgreSQL with Testcontainers for realistic database testing.
/// Ensures proper test isolation and provides common assertion helpers.
/// </summary>
public abstract class ChatPersistenceTestBase : PostgreSqlTestBase
{
    protected ChatDbContext DbContext { get; set; } = null!;
    protected ChatDbContext ReadDbContext { get; set; } = null!;
    protected IConversationRepository ConversationRepository { get; set; } = null!;
    protected IConversationReadRepository ConversationReadRepository { get; set; } = null!;
    protected IMessageReadRepository MessageReadRepository { get; set; } = null!;
    protected ITestDataVerificationRepository VerificationRepository { get; set; } = null!;
    protected EfUnitOfWork<ChatDbContext, ChatModule> UnitOfWork { get; set; } = null!;
    protected FakeTimeProvider TimeProvider { get; set; } = null!;

    [SetUp]
    public async Task SetUpBase()
    {
        TimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // Use centralized service provider to eliminate duplication
        var serviceProvider = ChatTestServiceProvider.GetOrCreate();

        // Setup write DbContext using centralized configuration
        var writeOptions = ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(
            ConnectionString, serviceProvider);

        // Setup read DbContext using centralized configuration
        var readOptions = ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(
            ConnectionString, serviceProvider);

        DbContext = new ChatDbContext(writeOptions, TimeProvider);
        ReadDbContext = new ChatDbContext(readOptions, TimeProvider);
        UnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(DbContext);
        ConversationRepository = new ConversationRepository(DbContext, UnitOfWork);
        ConversationReadRepository = new ConversationReadRepository(ReadDbContext);
        MessageReadRepository = new MessageReadRepository(ReadDbContext);
        VerificationRepository = new TestDataVerificationRepository(ReadDbContext, DbContext);

        // Create schema using EF model configuration for tests
        await DbContext.Database.EnsureCreatedAsync();

        // Also ensure schema exists for read context (they share the same database)
        await ReadDbContext.Database.EnsureCreatedAsync();

        // Allow child classes to perform additional setup
        await SetUpDerived();
    }

    [TearDown]
    public async Task TearDownBase()
    {
        try
        {
            // Allow child classes to perform cleanup
            await TearDownDerived();
        }
        finally
        {
            // Clean up database state for next test
            await CleanupDatabaseAsync(DbContext);

            // Ensure resources are cleaned up even if child cleanup fails
            UnitOfWork?.Dispose();
            ConversationRepository?.Dispose();
            await DbContext.DisposeAsync();
            await ReadDbContext.DisposeAsync();
        }
    }

    /// <summary>
    /// Override this method to perform additional setup in derived test classes.
    /// </summary>
    protected virtual Task SetUpDerived() => Task.CompletedTask;

    /// <summary>
    /// Override this method to perform additional cleanup in derived test classes.
    /// </summary>
    protected virtual Task TearDownDerived() => Task.CompletedTask;

    /// <summary>
    /// Cleans up Chat database state between tests.
    /// </summary>
    protected override async Task CleanupDatabaseAsync(DbContext context)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE chat.message CASCADE;
                TRUNCATE TABLE chat.conversation CASCADE;
            ");
        }
        catch
        {
            // If truncate fails, try dropping and recreating with EF model
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
    }

    #region Test Data Creation Helpers

    /// <summary>
    /// Creates a test conversation with the specified owner.
    /// </summary>
    protected static Conversation CreateTestConversation(
        AxonUserId? ownerId = null,
        string? title = null,
        FakeTimeProvider? timeProvider = null)
    {
        var owner = ownerId ?? AxonUserId.New();
        var provider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);

        var result = Conversation.StartNewConversation(owner, title, provider);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    /// <summary>
    /// Creates a test conversation with multiple messages.
    /// </summary>
    protected static Conversation CreateTestConversationWithMessages(
        AxonUserId? ownerId = null,
        int userMessageCount = 2,
        int assistantMessageCount = 2,
        FakeTimeProvider? timeProvider = null)
    {
        var conversation = CreateTestConversation(ownerId, "Test Conversation", timeProvider);
        var provider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);

        for (int i = 0; i < Math.Max(userMessageCount, assistantMessageCount); i++)
        {
            if (i < userMessageCount)
            {
                var userContent = MessageContent.Create($"User message {i + 1}").Value;
                var userResult = conversation.AppendUserMessageToConversation(userContent, provider);
                userResult.IsSuccess.ShouldBeTrue();
            }

            if (i < assistantMessageCount)
            {
                var assistantContent = MessageContent.Create($"Assistant response {i + 1}").Value;
                var aiResponseId = new AiResponseId(Guid.NewGuid().ToString());
                var assistantResult = conversation.AppendAssistantResponseToConversation(assistantContent, aiResponseId, provider);
                assistantResult.IsSuccess.ShouldBeTrue();
            }
        }

        return conversation;
    }

    /// <summary>
    /// Creates multiple test conversations for different owners.
    /// </summary>
    protected static (Conversation first, Conversation second, Conversation third) CreateMultipleTestConversations(
        FakeTimeProvider? timeProvider = null)
    {
        var provider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);

        return (
            CreateTestConversation(AxonUserId.New(), "First Conversation", provider),
            CreateTestConversation(AxonUserId.New(), "Second Conversation", provider),
            CreateTestConversation(AxonUserId.New(), "Third Conversation", provider)
        );
    }

    /// <summary>
    /// Creates a completed conversation with messages.
    /// </summary>
    protected static Conversation CreateCompletedConversation(
        AxonUserId? ownerId = null,
        FakeTimeProvider? timeProvider = null)
    {
        var conversation = CreateTestConversationWithMessages(ownerId, 2, 2, timeProvider);
        var provider = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow);

        var completeResult = conversation.Complete(provider);
        completeResult.IsSuccess.ShouldBeTrue();

        return conversation;
    }

    #endregion

    #region Persistence Helpers

    /// <summary>
    /// Saves a conversation to the database.
    /// </summary>
    protected async Task<Conversation> SaveConversationAsync(Conversation conversation)
    {
        await ConversationRepository.AddAsync(conversation);
        await UnitOfWork.SaveChangesAsync();
        return conversation;
    }

    /// <summary>
    /// Creates and saves a complete test scenario with conversation and messages.
    /// </summary>
    protected async Task<Conversation> CreateCompleteTestScenario(
        AxonUserId? ownerId = null,
        int userMessageCount = 2,
        int assistantMessageCount = 2,
        string? title = null)
    {
        var conversation = CreateTestConversationWithMessages(ownerId, userMessageCount, assistantMessageCount, TimeProvider);

        if (!string.IsNullOrEmpty(title))
        {
            var titleResult = conversation.UpdateTitle(title, TimeProvider);
            titleResult.IsSuccess.ShouldBeTrue();
        }

        await SaveConversationAsync(conversation);
        return conversation;
    }

    /// <summary>
    /// Creates multiple conversations owned by the same user.
    /// </summary>
    protected async Task<(Conversation active, Conversation completed, Conversation withMessages)> CreateMultipleConversationsForUser(
        AxonUserId ownerId)
    {
        var active = CreateTestConversation(ownerId, "Active Conversation", TimeProvider);
        var completed = CreateCompletedConversation(ownerId, TimeProvider);
        var withMessages = CreateTestConversationWithMessages(ownerId, 3, 2, TimeProvider);

        await SaveConversationAsync(active);
        await SaveConversationAsync(completed);
        await SaveConversationAsync(withMessages);

        return (active, completed, withMessages);
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// Verifies that entities are properly persisted to the database.
    /// </summary>
    protected async Task AssertEntityPersistedAsync<T>() where T : class
    {
        DbContext.ChangeTracker.Clear();
        var dbSet = DbContext.Set<T>();
        var count = await dbSet.CountAsync();
        count.ShouldBeGreaterThan(0, $"Entity of type {typeof(T).Name} should be persisted");
    }

    /// <summary>
    /// Verifies that a conversation and all its navigation properties are properly loaded.
    /// </summary>
    protected async Task AssertConversationCompletelyLoadedAsync(ConversationId conversationId)
    {
        DbContext.ChangeTracker.Clear();
        var conversation = await ConversationRepository.GetByIdAsync(conversationId);

        conversation.ShouldNotBeNull();
        conversation.GetAllMessages().ShouldNotBeNull();
        conversation.OwnerId.ShouldNotBe(default);
        conversation.Status.ShouldNotBe(default);
    }

    /// <summary>
    /// Asserts that a concurrency exception should be thrown.
    /// PostgreSQL properly enforces concurrency control with optimistic concurrency.
    /// Note: The application wraps DbUpdateConcurrencyException in a custom ConcurrencyException.
    /// </summary>
    protected static void AssertConcurrencyConflict(Func<Task> action)
    {
        Should.Throw<global::BuildingBlocks.Core.Diagnostics.Exceptions.ConcurrencyException>(action);
    }

    /// <summary>
    /// Asserts that a PostgreSQL unique constraint violation occurs.
    /// Validates the specific constraint name that was violated.
    /// </summary>
    protected static async Task AssertUniqueConstraintViolation(Func<Task> action, string? expectedConstraintName = null)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(action);

        var postgresException = exception.InnerException as PostgresException;
        postgresException.ShouldNotBeNull("Expected PostgreSQL constraint violation");

        // PostgreSQL unique violation error code
        postgresException.SqlState.ShouldBe("23505", "Expected unique constraint violation");

        // Validate specific constraint name if provided
        if (!string.IsNullOrEmpty(expectedConstraintName))
        {
            postgresException.ConstraintName.ShouldBe(expectedConstraintName,
                $"Expected constraint '{expectedConstraintName}' to be violated");
        }
    }

    #endregion

    #region Database State Helpers

    /// <summary>
    /// Clears the change tracker to ensure fresh reads from database.
    /// </summary>
    protected void ClearChangeTracker()
    {
        DbContext.ChangeTracker.Clear();
        ReadDbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// Forces a fresh database query by clearing the change tracker first.
    /// </summary>
    protected async Task<T?> QueryFreshAsync<T>(Func<Task<T?>> query) where T : class
    {
        ClearChangeTracker();
        return await query();
    }

    /// <summary>
    /// Gets the current change tracker state for debugging.
    /// </summary>
    protected string GetChangeTrackerState()
    {
        var entries = DbContext.ChangeTracker.Entries().ToList();
        return $"Tracked entities: {entries.Count}, " +
               $"Added: {entries.Count(e => e.State == EntityState.Added)}, " +
               $"Modified: {entries.Count(e => e.State == EntityState.Modified)}, " +
               $"Deleted: {entries.Count(e => e.State == EntityState.Deleted)}";
    }

    /// <summary>
    /// Advances the time provider and allows testing time-dependent behaviors.
    /// </summary>
    protected void AdvanceTime(TimeSpan timeSpan)
    {
        TimeProvider.Advance(timeSpan);
    }

    /// <summary>
    /// Sets a specific time for testing time-dependent scenarios.
    /// If the requested time is in the past, creates a new FakeTimeProvider.
    /// </summary>
    protected void SetTime(DateTimeOffset dateTime)
    {
        // If trying to go back in time, create a new FakeTimeProvider
        if (dateTime < TimeProvider.GetUtcNow())
        {
            TimeProvider = new FakeTimeProvider(dateTime);
            RecreateDbContext();
        }
        else
        {
            TimeProvider.SetUtcNow(dateTime);
        }
    }

    /// <summary>
    /// Recreates the DbContext and related components with the current TimeProvider.
    /// Call this when the TimeProvider instance changes.
    /// </summary>
    protected void RecreateDbContext()
    {
        // Dispose existing contexts and repositories
        UnitOfWork?.Dispose();
        ConversationRepository?.Dispose();
        DbContext?.Dispose();
        ReadDbContext?.Dispose();

        // Use centralized service provider for consistency
        var serviceProvider = ChatTestServiceProvider.GetOrCreate();

        // Recreate contexts using centralized configuration
        var writeOptions = ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(
            ConnectionString, serviceProvider);

        var readOptions = ChatTestServiceProvider.CreateDbContextOptions<ChatDbContext>(
            ConnectionString, serviceProvider);

        DbContext = new ChatDbContext(writeOptions, TimeProvider);
        ReadDbContext = new ChatDbContext(readOptions, TimeProvider);
        UnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(DbContext);
        ConversationRepository = new ConversationRepository(DbContext, UnitOfWork);
        ConversationReadRepository = new ConversationReadRepository(ReadDbContext);
        MessageReadRepository = new MessageReadRepository(ReadDbContext);
    }

    #endregion
}