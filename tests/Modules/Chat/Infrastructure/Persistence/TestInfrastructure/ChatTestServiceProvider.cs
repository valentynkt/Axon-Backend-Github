using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;

/// <summary>
/// Centralized service provider factory for Chat module tests.
/// Eliminates duplication of DI setup across test classes and provides
/// consistent configuration for all persistence tests.
/// </summary>
public static class ChatTestServiceProvider
{
    private static IServiceProvider? _cachedProvider;
    private static readonly object _lock = new();

    /// <summary>
    /// Creates or returns a cached service provider configured for Chat tests.
    /// Uses singleton pattern to improve test performance by reusing expensive setup.
    /// </summary>
    public static IServiceProvider GetOrCreate(bool useCache = true)
    {
        if (useCache && _cachedProvider != null)
            return _cachedProvider;

        lock (_lock)
        {
            if (useCache && _cachedProvider != null)
                return _cachedProvider;

            var provider = CreateServiceProvider();

            if (useCache)
                _cachedProvider = provider;

            return provider;
        }
    }

    /// <summary>
    /// Creates a new service provider with all required services for Chat tests.
    /// </summary>
    public static IServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        // Core EF services
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddConsole();
        });

        services.AddEntityFrameworkNpgsql();

        // Allow custom configuration
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates DbContext options with the standard Chat module configuration.
    /// </summary>
    public static DbContextOptions<TContext> CreateDbContextOptions<TContext>(
        string connectionString,
        IServiceProvider? serviceProvider = null) where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            npgsqlOptions.CommandTimeout(30);
        });

        if (serviceProvider != null)
        {
            optionsBuilder.UseInternalServiceProvider(serviceProvider);
        }

        // Enable sensitive data logging for tests
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();

        return optionsBuilder.Options;
    }

    /// <summary>
    /// Clears the cached service provider. Should be called in test cleanup
    /// if cache was used.
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            if (_cachedProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _cachedProvider = null;
        }
    }
}