using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MassTransit;
using NUnit.Framework;
using Npgsql;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbInvariants;

/// <summary>
/// Base class for Chat database invariants testing.
/// Following the pattern from Identity module, these tests validate that database constraints
/// alone prevent data integrity violations without relying on domain logic.
/// Tests are designed to initially fail (TDD approach) to expose gaps in current implementation.
/// </summary>
public abstract class ChatDbInvariantsTestBase : PostgreSqlTestBase
{
    protected ChatDbContext DbContext { get; set; } = null!;
    protected ChatDbContext ReadDbContext { get; set; } = null!;
    protected IConversationRepository ConversationWriteRepository { get; set; } = null!;
    protected IConversationReadRepository ConversationReadRepository { get; set; } = null!;
    protected IMessageReadRepository MessageReadRepository { get; set; } = null!;
    protected ITestDataVerificationRepository VerificationRepository { get; set; } = null!;
    protected EfUnitOfWork<ChatDbContext, ChatModule> UnitOfWork { get; set; } = null!;
    protected FakeTimeProvider TimeProvider { get; set; } = null!;

    [SetUp]
    public async Task SetUpBase()
    {
        TimeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEntityFrameworkNpgsql();

        // Add MassTransit for event bus support
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        var serviceProvider = services.BuildServiceProvider();

        // Setup write DbContext
        var writeOptions = CreateDbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            })
            .UseInternalServiceProvider(serviceProvider)
            .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            .EnableSensitiveDataLogging()
            .Options;

        // Setup read DbContext
        var readOptions = CreateDbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            })
            .UseInternalServiceProvider(serviceProvider)
            .Options;

        DbContext = new ChatDbContext(writeOptions, TimeProvider);
        ReadDbContext = new ChatDbContext(readOptions, TimeProvider);
        UnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(DbContext);
        ConversationWriteRepository = new ConversationWriteRepository(DbContext, UnitOfWork);
        ConversationReadRepository = new ConversationReadRepository(ReadDbContext);
        MessageReadRepository = new MessageReadRepository(ReadDbContext);
        VerificationRepository = new TestDataVerificationRepository(ReadDbContext, DbContext);

        // Create schema and apply migrations
        await DbContext.Database.MigrateAsync();

        // Ensure we're working with a fresh database for each test
        await CleanupDatabaseAsync();

        await SetUpDerived();
    }

    [TearDown]
    public async Task TearDownBase()
    {
        try
        {
            await TearDownDerived();
        }
        finally
        {
            await CleanupDatabaseAsync();

            UnitOfWork?.Dispose();
            ConversationWriteRepository?.Dispose();
            await DbContext.DisposeAsync();
            await ReadDbContext.DisposeAsync();
        }
    }

    protected virtual Task SetUpDerived() => Task.CompletedTask;
    protected virtual Task TearDownDerived() => Task.CompletedTask;

    protected async Task CleanupDatabaseAsync()
    {
        try
        {
            // Use CASCADE to handle owned entities (Messages)
            await DbContext.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE chat.""Messages"" CASCADE;
                TRUNCATE TABLE chat.""Conversations"" CASCADE;
            ");
        }
        catch
        {
            // If truncate fails, recreate the schema
            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.Database.MigrateAsync();
        }
    }

    #region Assertion Helpers

    /// <summary>
    /// Asserts that a PostgreSQL constraint violation occurs with specific constraint name.
    /// Used to validate database-level invariants are properly enforced.
    /// </summary>
    protected static async Task AssertPostgreSQLConstraintViolation(
        Func<Task> action,
        string expectedConstraintName)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(action);

        var pgException = exception.InnerException as PostgresException;
        pgException.ShouldNotBeNull("Expected PostgreSQL constraint violation");

        // 23505 = unique_violation
        // 23503 = foreign_key_violation
        // 23514 = check_violation
        pgException.SqlState.ShouldBeOneOf(["23505", "23503", "23514"],
            $"Expected constraint violation but got {pgException.SqlState}");

        if (!string.IsNullOrEmpty(expectedConstraintName))
        {
            pgException.ConstraintName?.ToLower().ShouldContain(expectedConstraintName.ToLower());
        }
    }

    /// <summary>
    /// Asserts constraint violation for raw SQL operations.
    /// Used when testing invariants that can't be triggered through repository operations.
    /// </summary>
    protected static async Task AssertPostgreSQLConstraintViolationRaw(
        Func<Task> action,
        string expectedConstraintName)
    {
        try
        {
            await action();
            Assert.Fail($"Expected PostgreSQL constraint violation for '{expectedConstraintName}' but operation succeeded");
        }
        catch (PostgresException pgException)
        {
            pgException.SqlState.ShouldBeOneOf(["23505", "23503", "23514"]);

            if (!string.IsNullOrEmpty(expectedConstraintName))
            {
                pgException.ConstraintName?.ToLower().ShouldContain(expectedConstraintName.ToLower());
            }
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected PostgresException but got {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Asserts that message sequence numbers maintain integrity within a conversation.
    /// Validates that sequences are consecutive and start from 1.
    /// </summary>
    protected async Task AssertMessageSequenceIntegrity(ConversationId conversationId)
    {
        var messages = await VerificationRepository.GetMessageSequencesAsync(conversationId);

        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1,
                $"Message sequence integrity violated at position {i}");
        }
    }

    /// <summary>
    /// Verifies that aggregate version is properly updated when child entities change.
    /// This is critical for optimistic concurrency control.
    /// </summary>
    protected async Task<uint> GetAggregateVersion(ConversationId conversationId)
    {
        var version = await VerificationRepository.GetConversationVersionAsync(conversationId);
        version.ShouldNotBe(0u, $"Conversation {conversationId} not found");
        return version;
    }

    #endregion
}