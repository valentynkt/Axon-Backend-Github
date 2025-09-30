using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;
using Testcontainers.PostgreSql;
using Npgsql;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;

/// <summary>
/// Enhanced test base for DB Invariant tests using real PostgreSQL via Testcontainers.
/// Provides PostgreSQL-specific constraint testing and concurrent transaction support.
/// Implements canonical fixtures from TDD document for consistent testing.
/// </summary>
public abstract class IdentityDbInvariantsTestBase
{
    protected IdentityWriteDbContext DbContext { get; set; } = null!;
    protected AxonPrincipalWriteRepository PrincipalRepository { get; set; } = null!;
    protected WalletWriteRepository WalletRepository { get; set; } = null!;
    protected EfUnitOfWork<IdentityWriteDbContext, IdentityModule> UnitOfWork { get; set; } = null!;
    protected AutoRevocationService AutoRevocationService { get; set; } = null!;

    private PostgreSqlContainer _postgreSqlContainer = null!;
    private string _connectionString = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Create PostgreSQL container for real database constraint testing
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("axon_identity_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        await _postgreSqlContainer.StartAsync();
        _connectionString = _postgreSqlContainer.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_postgreSqlContainer != null)
        {
            await _postgreSqlContainer.DisposeAsync();
        }
    }

    [SetUp]
    public async Task SetUpBase()
    {
        var options = new DbContextOptionsBuilder<IdentityWriteDbContext>()
            .UseNpgsql(_connectionString)
            .EnableSensitiveDataLogging()
            .Options;

        DbContext = new IdentityWriteDbContext(options);
        UnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(DbContext);
        PrincipalRepository = new AxonPrincipalWriteRepository(DbContext, UnitOfWork, TimeProvider.System);
        WalletRepository = new WalletWriteRepository(DbContext, UnitOfWork);

        // Create AutoRevocationService with DbContext as IIdentityWriteDbContext
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AutoRevocationService>.Instance;
        AutoRevocationService = new AutoRevocationService(DbContext, TimeProvider.System, logger);

        // Ensure database is created and migrated
        await DbContext.Database.EnsureCreatedAsync();

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
            await CleanupDatabaseAsync();

            UnitOfWork?.Dispose();
            PrincipalRepository?.Dispose();
            WalletRepository?.Dispose();
            AutoRevocationService = null!;
            await DbContext.DisposeAsync();
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

    #region Database Cleanup

    /// <summary>
    /// Cleans up database state between tests to ensure isolation.
    /// </summary>
    private async Task CleanupDatabaseAsync()
    {
        try
        {
            await DbContext.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE identity.""PrincipalChainDefault"" CASCADE;
                TRUNCATE TABLE identity.""WalletOwnership"" CASCADE;
                TRUNCATE TABLE identity.""Credential"" CASCADE;
                TRUNCATE TABLE identity.""Wallet"" CASCADE;
                TRUNCATE TABLE identity.""Principal"" CASCADE;
            ");
        }
        catch
        {
            // If truncate fails, try dropping and recreating
            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.Database.EnsureCreatedAsync();
        }
    }

    #endregion

    #region PostgreSQL Constraint Assertion Helpers

    /// <summary>
    /// Asserts that a PostgreSQL unique constraint violation occurs.
    /// Validates the specific constraint name that was violated.
    /// </summary>
    protected static async Task AssertPostgreSQLConstraintViolation(
        Func<Task> action,
        string expectedConstraintName)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(action);

        var postgresException = exception.InnerException as PostgresException;
        postgresException.ShouldNotBeNull("Expected PostgreSQL constraint violation");

        // PostgreSQL unique violation error code
        postgresException.SqlState.ShouldBe("23505", "Expected unique constraint violation");

        // Validate specific constraint name
        postgresException.ConstraintName.ShouldBe(expectedConstraintName,
            $"Expected constraint '{expectedConstraintName}' to be violated");
    }

    /// <summary>
    /// Asserts that a PostgreSQL constraint violation occurs from raw SQL operations.
    /// Used when bypassing EF Core with ExecuteSqlRawAsync.
    /// </summary>
    protected static async Task AssertPostgreSQLConstraintViolationRaw(
        Func<Task> action,
        string expectedConstraintName)
    {
        var exception = await Should.ThrowAsync<PostgresException>(action);

        // PostgreSQL unique violation error code
        exception.SqlState.ShouldBe("23505", "Expected unique constraint violation");

        // Validate specific constraint name
        exception.ConstraintName.ShouldBe(expectedConstraintName,
            $"Expected constraint '{expectedConstraintName}' to be violated");
    }

    /// <summary>
    /// Asserts that a PostgreSQL check constraint violation occurs.
    /// </summary>
    protected static async Task AssertPostgreSQLCheckConstraintViolation(
        Func<Task> action,
        string expectedConstraintName)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(action);

        var postgresException = exception.InnerException as PostgresException;
        postgresException.ShouldNotBeNull("Expected PostgreSQL check constraint violation");

        // PostgreSQL check violation error code
        postgresException.SqlState.ShouldBe("23514", "Expected check constraint violation");

        // Validate specific constraint name
        postgresException.ConstraintName.ShouldBe(expectedConstraintName,
            $"Expected constraint '{expectedConstraintName}' to be violated");
    }

    /// <summary>
    /// Asserts that a partial unique index violation occurs.
    /// Partial indexes in PostgreSQL have specific behavior patterns.
    /// </summary>
    protected static async Task AssertPartialUniqueIndexViolation(
        Func<Task> action,
        string expectedIndexName)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(action);

        var postgresException = exception.InnerException as PostgresException;
        postgresException.ShouldNotBeNull("Expected PostgreSQL partial index violation");

        postgresException.SqlState.ShouldBe("23505", "Expected unique constraint violation");

        // For partial indexes, the constraint name might be the index name
        var constraintName = postgresException.ConstraintName ?? postgresException.Detail ?? "";
        constraintName.Contains(expectedIndexName, StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            $"Expected partial index '{expectedIndexName}' to be violated, but got constraint: '{constraintName}'");
    }

    /// <summary>
    /// Asserts that a PostgreSQL partial unique index violation occurs from raw SQL operations.
    /// Used when bypassing EF Core with ExecuteSqlRawAsync.
    /// </summary>
    protected static async Task AssertPartialUniqueIndexViolationRaw(
        Func<Task> action,
        string expectedIndexName)
    {
        var exception = await Should.ThrowAsync<PostgresException>(action);

        exception.SqlState.ShouldBe("23505", "Expected unique constraint violation");

        // For partial indexes, the constraint name might be the index name
        var constraintName = exception.ConstraintName ?? exception.Detail ?? "";
        constraintName.Contains(expectedIndexName, StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            $"Expected partial index '{expectedIndexName}' to be violated, but got constraint: '{constraintName}'");
    }

    #endregion

    #region Concurrent Transaction Helpers

    /// <summary>
    /// Creates a separate DbContext for concurrent transaction testing.
    /// </summary>
    protected IdentityWriteDbContext CreateConcurrentDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityWriteDbContext>()
            .UseNpgsql(_connectionString)
            .EnableSensitiveDataLogging()
            .Options;

        return new IdentityWriteDbContext(options);
    }

    /// <summary>
    /// Executes two operations concurrently in separate transactions.
    /// Useful for testing race conditions and exclusivity constraints.
    /// </summary>
    protected async Task<(Exception? Exception1, Exception? Exception2)> ExecuteConcurrentOperations(
        Func<IdentityWriteDbContext, Task> operation1,
        Func<IdentityWriteDbContext, Task> operation2)
    {
        using var context1 = CreateConcurrentDbContext();
        using var context2 = CreateConcurrentDbContext();

        Exception? exception1 = null;
        Exception? exception2 = null;

        var task1 = Task.Run(async () =>
        {
            try
            {
                await operation1(context1);
            }
            catch (Exception ex)
            {
                exception1 = ex;
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                await operation2(context2);
            }
            catch (Exception ex)
            {
                exception2 = ex;
            }
        });

        await Task.WhenAll(task1, task2);
        return (exception1, exception2);
    }

    #endregion

    #region Database State Helpers

    /// <summary>
    /// Clears the change tracker to ensure fresh reads from database.
    /// </summary>
    protected void ClearChangeTracker()
    {
        DbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// Forces a fresh database query by clearing the change tracker first.
    /// </summary>
    protected async Task<T?> QueryFreshAsync<T>(Func<Task<T?>> query) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);

        ClearChangeTracker();
        return await query();
    }

    /// <summary>
    /// Gets the current database connection state for debugging.
    /// </summary>
    protected string GetDatabaseState()
    {
        return $"Connection: {DbContext.Database.GetConnectionString()}, " +
               $"Transaction: {(DbContext.Database.CurrentTransaction != null ? "Active" : "None")}";
    }

    #endregion
}