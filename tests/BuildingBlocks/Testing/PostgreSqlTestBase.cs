using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using EFCore.NamingConventions;

namespace BuildingBlocks.Testing;

/// <summary>
/// Shared base class for tests requiring PostgreSQL with Testcontainers.
/// Provides consistent container lifecycle management and database cleanup.
/// </summary>
public abstract class PostgreSqlTestBase
{
    protected string ConnectionString { get; private set; } = null!;

    private PostgreSqlContainer _postgreSqlContainer = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpPostgreSql()
    {
        // Create PostgreSQL container for real database testing
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("axon_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        await _postgreSqlContainer.StartAsync();
        ConnectionString = _postgreSqlContainer.GetConnectionString();

        // Allow derived classes to perform additional one-time setup
        await OnOneTimeSetUpAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownPostgreSql()
    {
        try
        {
            // Allow derived classes to perform cleanup
            await OnOneTimeTearDownAsync();
        }
        finally
        {
            if (_postgreSqlContainer != null)
            {
                await _postgreSqlContainer.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Override this method for additional one-time setup in derived classes.
    /// </summary>
    protected virtual Task OnOneTimeSetUpAsync() => Task.CompletedTask;

    /// <summary>
    /// Override this method for additional one-time cleanup in derived classes.
    /// </summary>
    protected virtual Task OnOneTimeTearDownAsync() => Task.CompletedTask;

    /// <summary>
    /// Creates DbContextOptions for PostgreSQL with standard configuration.
    /// </summary>
    protected DbContextOptionsBuilder<T> CreateDbContextOptionsBuilder<T>() where T : DbContext
    {
        return new DbContextOptionsBuilder<T>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention(NamingConvention.None) // Disable automatic snake_case to match production
            .EnableSensitiveDataLogging();
    }

    /// <summary>
    /// Executes SQL to clean up database state between tests.
    /// Override in derived classes to provide specific cleanup SQL.
    /// </summary>
    protected virtual async Task CleanupDatabaseAsync(DbContext context)
    {
        // Default implementation - derived classes should override with specific cleanup
        try
        {
            await context.Database.ExecuteSqlRawAsync("SELECT 1;");
        }
        catch
        {
            // If anything fails, recreate the database
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
    }
}