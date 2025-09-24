using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Tests.Common;

/// <summary>
/// Test-specific WebApplicationFactory that configures SQLite databases for testing.
/// Replaces PostgreSQL with SQLite to avoid external dependencies and ensure test isolation.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;

    private Action<IServiceCollection>? _additionalServices;
    private string _environment = "Test";

    public TestWebApplicationFactory()
    {
        // Create unique database name for each test instance to ensure isolation
        _databaseName = $"TestDb_{Guid.NewGuid():N}";
    }

    public TestWebApplicationFactory WithServices(Action<IServiceCollection> configureServices)
    {
        _additionalServices = configureServices;
        return this;
    }

    public TestWebApplicationFactory WithEnvironment(string environment)
    {
        _environment = environment;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configBuilder =>
        {
            // Override configuration to disable automatic migrations
            var testConfig = new Dictionary<string, string>
            {
                {"DatabaseOptions:EnableAutomaticMigrations", "false"}
            };

            configBuilder.AddInMemoryCollection(testConfig!);
        });

        builder.ConfigureServices((context, services) =>
        {
            // Replace database configuration by removing all DbContext related services
            // and re-registering them with SQLite

            // Find and remove all existing database context registrations
            var contextsToRemove = new[]
            {
                typeof(ChatDbContext),
                typeof(ChatReadDbContext),
                typeof(IdentityWriteDbContext),
                typeof(IdentityReadDbContext),
                typeof(DbContextOptions<ChatDbContext>),
                typeof(DbContextOptions<ChatReadDbContext>),
                typeof(DbContextOptions<IdentityWriteDbContext>),
                typeof(DbContextOptions<IdentityReadDbContext>)
            };

            foreach (var contextType in contextsToRemove)
            {
                var descriptors = services.Where(d => d.ServiceType == contextType).ToList();
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }
            }

            // Also remove any Entity Framework service provider registrations
            var efServices = services.Where(d =>
                d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true)
                .ToList();
            foreach (var service in efServices)
            {
                services.Remove(service);
            }

            // Re-register with SQLite
            services.AddDbContext<ChatDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_databaseName}_Chat.db");
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            services.AddDbContext<ChatReadDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_databaseName}_Chat.db");
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            services.AddDbContext<IdentityWriteDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_databaseName}_Identity.db");
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            services.AddDbContext<IdentityReadDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_databaseName}_Identity.db");
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            // Apply additional service configuration if provided
            _additionalServices?.Invoke(services);
        });

        builder.UseEnvironment(_environment);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Create databases after the host is built and service provider is available
        using (var scope = host.Services.CreateScope())
        {
            try
            {
                // Create Chat database
                var chatContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                chatContext.Database.EnsureCreated();

                // Create Identity database
                var identityContext = scope.ServiceProvider.GetRequiredService<IdentityWriteDbContext>();
                identityContext.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetService<ILogger<TestWebApplicationFactory>>();
                logger?.LogError(ex, "Failed to create test databases");
                throw;
            }
        }

        return host;
    }

    private static void RemoveDbContextServices(IServiceCollection services)
    {
        // Remove DbContextOptions for all contexts
        RemoveService<DbContextOptions<ChatDbContext>>(services);
        RemoveService<DbContextOptions<ChatReadDbContext>>(services);
        RemoveService<DbContextOptions<IdentityWriteDbContext>>(services);
        RemoveService<DbContextOptions<IdentityReadDbContext>>(services);

        // Remove the actual DbContext services
        RemoveService<ChatDbContext>(services);
        RemoveService<ChatReadDbContext>(services);
        RemoveService<IdentityWriteDbContext>(services);
        RemoveService<IdentityReadDbContext>(services);
    }

    private static void RemoveService<TService>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test database files
            try
            {
                var chatDbFile = $"{_databaseName}_Chat.db";
                var identityDbFile = $"{_databaseName}_Identity.db";

                if (File.Exists(chatDbFile))
                    File.Delete(chatDbFile);

                if (File.Exists(identityDbFile))
                    File.Delete(identityDbFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        base.Dispose(disposing);
    }
}