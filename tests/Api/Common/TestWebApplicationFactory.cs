using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Context;
using BuildingBlocks.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Tests.Common;

/// <summary>
/// Test-specific WebApplicationFactory that configures PostgreSQL databases for testing.
/// Uses Testcontainers for consistent PostgreSQL testing across all environments.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private PostgreSqlTestBase _testBase = null!;
    private Action<IServiceCollection>? _additionalServices;
    private string _environment = "Testing";

    public TestWebApplicationFactory()
    {
        _testBase = new TestPostgreSqlTestBase();
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

    public async Task InitializeAsync()
    {
        // Initialize the PostgreSQL container
        await _testBase.OneTimeSetUpPostgreSql();
    }

    public override async ValueTask DisposeAsync()
    {
        // Clean up the PostgreSQL container
        await _testBase.OneTimeTearDownPostgreSql();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            // Build configuration to get existing settings first
            var existingConfig = configBuilder.Build();

            // Override configuration to disable automatic migrations and use test connection
            // Add these settings with higher priority to ensure they override appsettings files
            var testConfig = new Dictionary<string, string>
            {
                {"DatabaseOptions:EnableAutomaticMigrations", "false"},
                {"ConnectionStrings:DefaultConnection", _testBase.GetConnectionString()}
            };

            // Add as the last configuration source to ensure highest priority
            configBuilder.AddInMemoryCollection(testConfig!);
        });

        builder.ConfigureServices((context, services) =>
        {
            // Replace database configuration by removing all DbContext related services
            // and re-registering them with PostgreSQL

            // Find and remove all existing database context registrations
            var contextsToRemove = new[]
            {
                typeof(ChatDbContext),
                typeof(IdentityDbContext),
                typeof(IdentityDbContext),
                typeof(AspNetIdentityContext),
                typeof(DbContextOptions<ChatDbContext>),
                typeof(DbContextOptions<IdentityDbContext>),
                typeof(DbContextOptions<IdentityDbContext>),
                typeof(DbContextOptions<AspNetIdentityContext>)
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

            // Re-register with PostgreSQL using the shared connection string
            services.AddDbContext<ChatDbContext>(options =>
            {
                options.UseNpgsql(_testBase.GetConnectionString());
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            services.AddDbContext<IdentityDbContext>(options =>
            {
                options.UseNpgsql(_testBase.GetConnectionString());
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            services.AddDbContext<IdentityDbContext>(options =>
            {
                options.UseNpgsql(_testBase.GetConnectionString());
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            // Add AspNetIdentityContext for ASP.NET Identity Framework
            services.AddDbContext<AspNetIdentityContext>(options =>
            {
                options.UseNpgsql(_testBase.GetConnectionString());
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            // Apply additional service configuration if provided
            _additionalServices?.Invoke(services);
        });

        builder.UseEnvironment(_environment);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Initialize PostgreSQL container first
        _testBase.OneTimeSetUpPostgreSql().GetAwaiter().GetResult();

        var host = base.CreateHost(builder);

        // Create databases after the host is built and service provider is available
        using (var scope = host.Services.CreateScope())
        {
            try
            {
                // Create Chat database using migrations
                var chatContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                chatContext.Database.Migrate();

                // Create Identity database using migrations
                var identityContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                identityContext.Database.Migrate();

                // Create AspNetIdentityContext database using migrations
                var mainAspNetIdentityContext = scope.ServiceProvider.GetRequiredService<AspNetIdentityContext>();
                mainAspNetIdentityContext.Database.Migrate();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test resources - no dispose needed for PostgreSqlTestBase
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Private implementation of PostgreSqlTestBase for container management
    /// </summary>
    private class TestPostgreSqlTestBase : PostgreSqlTestBase
    {
        // This class is just to access the protected PostgreSqlTestBase functionality
    }
}