using System;
using System.Linq.Expressions;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Persistence.Caching;
using BuildingBlocks.Persistence.Common;
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Persistence.Infrastructure;
using BuildingBlocks.Persistence.Read;
using BuildingBlocks.Persistence.Write;
using BuildingBlocks.Web;
using Humanizer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Persistence layer dependency injection extensions
/// Database provider agnostic configuration and repository registrations
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Add DbContext with modern CQRS architecture
    /// </summary>
    public static IServiceCollection AddDbContext<TContext>(
        this WebApplicationBuilder builder,
        Action<DatabaseOptions>? configurator = null)
        where TContext : DbContext, IDbContext
    {
        return builder.Services.AddDbContext<TContext>(builder.Configuration, configurator);
    }

    /// <summary>
    /// Add DbContext with configuration and modern architecture
    /// </summary>
    public static IServiceCollection AddDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DatabaseOptions>? configurator = null)
        where TContext : DbContext, IDbContext
    {
        // Configure database options
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(nameof(DatabaseOptions)));

        if (configurator != null)
        {
            services.Configure(configurator);
        }
        else
        {
            services.AddValidateOptions<DatabaseOptions>();
        }

        // Register DbContext with in-memory database (for now)
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            ConfigureDbContext(options, databaseOptions, typeof(TContext));
        });

        // Register context interfaces
        services.AddScoped<IDbContext>(provider => provider.GetRequiredService<TContext>());
        
        // Register repositories and infrastructure services
        services.AddPersistenceServices();

        return services;
    }

    /// <summary>
    /// Add custom DbContext configuration with Aspire support
    /// </summary>
    public static IServiceCollection AddCustomDbContext<TContext>(
        this WebApplicationBuilder builder, 
        string? connectionName = "")
        where TContext : DbContext, IDbContext
    {
        builder.Services.AddValidateOptions<DatabaseOptions>();

        builder.Services.AddDbContext<TContext>((sp, options) =>
        {
            // Use in-memory database for now
            options.UseInMemoryDatabase($"{typeof(TContext).Name}_{connectionName.Kebaberize()}");

            // Suppress warnings for pending model changes
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddPersistenceServices();
        return builder.Services;
    }

    /// <summary>
    /// Add CQRS-style DbContext with separate read and write contexts
    /// </summary>
    public static IServiceCollection AddCqrsDbContext<TWriteContext, TReadContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DatabaseOptions>? configurator = null)
        where TWriteContext : DbContext, IWriteDbContext<object>
        where TReadContext : DbContext, IReadDbContext<object>
    {
        // Configure shared database options
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(nameof(DatabaseOptions)));

        if (configurator != null)
        {
            services.Configure(configurator);
        }

        // Register Write Context
        services.AddDbContext<TWriteContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            ConfigureDbContext(options, databaseOptions, typeof(TWriteContext));
        });

        // Register Read Context (optimized for queries)
        services.AddDbContext<TReadContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            ConfigureDbContext(options, databaseOptions, typeof(TReadContext));
            
            // Read context optimizations
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // Register interfaces
        services.AddScoped<IWriteDbContext<object>>(provider => provider.GetRequiredService<TWriteContext>());
        services.AddScoped<IReadDbContext<object>>(provider => provider.GetRequiredService<TReadContext>());
        services.AddScoped<IDbContext>(provider => provider.GetRequiredService<TWriteContext>());

        services.AddPersistenceServices();
        return services;
    }

    /// <summary>
    /// Register all persistence services (repositories, unit of work, etc.)
    /// </summary>
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        // Register repositories (using generic EF implementations for now)
        services.AddScoped(typeof(IReadRepository<,>), typeof(EfReadRepository<,>));
        services.AddScoped(typeof(IWriteRepository<,>), typeof(EfWriteRepository<,>));
        
        // Register Unit of Work
        services.AddScoped(typeof(IWriteUnitOfWork<>), typeof(EfWriteUnitOfWork<>));
        
        // Register infrastructure services
        services.AddScoped<ISeedManager, SeedManager>();
        
        return services;
    }

    /// <summary>
    /// Add persistence layer with Clean Architecture patterns and enterprise features
    /// </summary>
    public static WebApplicationBuilder AddPersistenceWithCleanArchitecture<TContext>(
        this WebApplicationBuilder builder,
        string? connectionName = "DefaultConnection",
        Action<PersistenceConfigurationOptions>? configureOptions = null)
        where TContext : DbContext, IDbContext
    {
        var options = new PersistenceConfigurationOptions();
        configureOptions?.Invoke(options);

        // Core database configuration
        builder.Services.AddDbContext<TContext>(builder.Configuration, opts =>
        {
            opts.ConnectionString = options.ConnectionString;
            opts.MaxRetryCount = options.MaxRetryCount;
            opts.MaxRetryDelaySeconds = options.MaxRetryDelaySeconds;
            opts.CommandTimeout = options.CommandTimeout;
            opts.EnableSensitiveDataLogging = options.EnableSensitiveDataLogging;
            opts.EnableDetailedErrors = options.EnableDetailedErrors;
            opts.EnableServiceProviderCaching = options.EnableServiceProviderCaching;
            opts.MigrationsAssembly = options.MigrationsAssembly;
            opts.DefaultSchema = options.DefaultSchema;
            opts.EnableAutomaticMigrations = options.EnableAutomaticMigrations;
        });

        // Add repository patterns and transaction behavior
        builder.Services.ConfigureRepositoryPatterns(options);

        // Add performance monitoring
        if (options.EnablePerformanceMonitoring)
        {
            builder.Services.AddPerformanceMonitoring<TContext>();
        }

        // Add health checks
        if (options.EnableHealthChecks)
        {
            builder.Services.AddPersistenceHealthChecks<TContext>(options.HealthCheckOptions);
        }

        return builder;
    }

    /// <summary>
    /// Configure repository patterns with Clean Architecture compliance
    /// </summary>
    public static IServiceCollection ConfigureRepositoryPatterns(
        this IServiceCollection services,
        PersistenceConfigurationOptions options)
    {
        // Configure transaction behavior
        if (options.DefaultTransactionBehavior != TransactionBehavior.None)
        {
            services.ConfigureTransactionBehavior(options.DefaultTransactionBehavior);
        }

        // Configure caching if enabled
        if (options.EnableRepositoryCaching)
        {
            services.AddPersistenceRepositoryCaching(options.CacheExpiration);
        }

        // Configure repository compatibility layer if enabled
        if (options.EnableRepositoryCompatibilityLayer)
        {
            services.AddRepositoryCompatibilityLayer();
        }

        return services;
    }

    /// <summary>
    /// Add performance monitoring for database operations
    /// </summary>
    public static IServiceCollection AddPerformanceMonitoring<TContext>(this IServiceCollection services)
        where TContext : DbContext, IDbContext
    {
        services.AddScoped<IPerformanceTracker<TContext>, PersistencePerformanceTracker<TContext>>();
        return services;
    }

    /// <summary>
    /// Add comprehensive health checks for persistence layer
    /// </summary>
    public static IServiceCollection AddPersistenceHealthChecks<TContext>(
        this IServiceCollection services,
        PersistenceHealthCheckOptions? options = null)
        where TContext : DbContext, IDbContext
    {
        services.AddHealthChecks()
            .AddCheck<PersistenceHealthCheck<TContext>>(
                $"persistence-{typeof(TContext).Name.ToLowerInvariant()}",
                tags: new[] { "persistence", "database", typeof(TContext).Name.ToLowerInvariant() });

        // Register the health check service
        services.AddScoped<IPersistenceHealthCheck<TContext>, PersistenceHealthCheck<TContext>>();
        
        // Register options if provided
        if (options != null)
        {
            services.AddSingleton(options);
        }

        return services;
    }

    /// <summary>
    /// Configure transaction behavior patterns
    /// </summary>
    public static IServiceCollection ConfigureTransactionBehavior(
        this IServiceCollection services,
        TransactionBehavior behavior)
    {
        switch (behavior)
        {
            case TransactionBehavior.PerRequest:
                services.AddScoped<ITransactionBehaviorHandler, PerRequestTransactionHandler>();
                break;
            case TransactionBehavior.PerOperation:
                services.AddScoped<ITransactionBehaviorHandler, PerOperationTransactionHandler>();
                break;
            case TransactionBehavior.Explicit:
                services.AddScoped<ITransactionBehaviorHandler, ExplicitTransactionHandler>();
                break;
            case TransactionBehavior.None:
                // No transaction behavior handler registered
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown transaction behavior");
        }

        return services;
    }

    /// <summary>
    /// Add repository-level caching using the decorator-based approach
    /// </summary>
    public static IServiceCollection AddPersistenceRepositoryCaching(
        this IServiceCollection services,
        TimeSpan? cacheExpiration = null)
    {
        // Use the existing caching extensions from the Caching folder
        return services.AddRepositoryCaching(cacheExpiration);
    }

    /// <summary>
    /// Add repository compatibility layer for legacy support
    /// TODO: Implement this extension method based on specific legacy requirements
    /// </summary>
    public static IServiceCollection AddRepositoryCompatibilityLayer(this IServiceCollection services)
    {
        // This is a placeholder for future compatibility layer implementation
        // Implementation depends on specific legacy repository patterns that need support
        return services;
    }

    /// <summary>
    /// Use database migrations
    /// </summary>
    public static IApplicationBuilder UseMigration<TContext>(this IApplicationBuilder app)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(app);
        
        MigrateAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        SeedAsync(app.ApplicationServices).GetAwaiter().GetResult();
        
        return app;
    }

    private static async Task MigrateAsync<TContext>(IServiceProvider serviceProvider)
        where TContext : DbContext, IDbContext
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Count());

            await context.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully.");
        }
    }

    private static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var seedersManager = scope.ServiceProvider.GetRequiredService<ISeedManager>();

        await seedersManager.ExecuteSeedAsync();
    }

    /// <summary>
    /// Configure database for development environment
    /// </summary>
    public static IServiceCollection ConfigureDatabaseDevelopment(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration; // Parameter reserved for future use
        
        services.Configure<DatabaseOptions>(options =>
        {
            options.EnableSensitiveDataLogging = true;
            options.EnableDetailedErrors = true;
            options.MaxRetryCount = 5;
            options.MaxRetryDelaySeconds = 10;
            options.CommandTimeout = 60;
            options.EnableServiceProviderCaching = false;
        });

        return services;
    }

    /// <summary>
    /// Configure database for production environment
    /// </summary>
    public static IServiceCollection ConfigureDatabaseProduction(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration; // Parameter reserved for future use
        
        services.Configure<DatabaseOptions>(options =>
        {
            options.EnableSensitiveDataLogging = false;
            options.EnableDetailedErrors = false;
            options.MaxRetryCount = 3;
            options.MaxRetryDelaySeconds = 30;
            options.CommandTimeout = 30;
            options.EnableServiceProviderCaching = true;
        });

        return services;
    }

    /// <summary>
    /// Configure soft delete global query filters
    /// </summary>
    public static void FilterSoftDeletedProperties(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IAggregate).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "entity");
                var property = Expression.Property(parameter, nameof(IAggregate.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(property), parameter);
                entityType.SetQueryFilter(filter);
            }
        }
    }

    /// <summary>
    /// Configure snake_case table and column names
    /// </summary>
    public static void ToSnakeCaseTables(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetTableName()!.Underscore());

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().Underscore());
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(key.GetName()!.Underscore());
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(foreignKey.GetConstraintName()!.Underscore());
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(index.GetDatabaseName()!.Underscore());
            }
        }
    }

    /// <summary>
    /// Configure DbContext options
    /// </summary>
    private static void ConfigureDbContext(
        DbContextOptionsBuilder options, 
        DatabaseOptions databaseOptions, 
        Type contextType)
    {
        // Use in-memory database for now (can be replaced with other providers)
        options.UseInMemoryDatabase(contextType.Name);

        // Development settings
        if (databaseOptions.EnableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
        }

        if (databaseOptions.EnableDetailedErrors)
        {
            options.EnableDetailedErrors();
        }

        // Performance settings
        if (databaseOptions.EnableServiceProviderCaching)
        {
            options.EnableServiceProviderCaching();
        }
    }

    /// <summary>
    /// Add validation for DatabaseOptions
    /// </summary>
    private static IServiceCollection AddValidateOptions<T>(this IServiceCollection services)
        where T : class
    {
        services.AddSingleton<IValidateOptions<T>, ValidateDatabaseOptions<T>>();
        return services;
    }
}

/// <summary>
/// Database configuration options (provider agnostic)
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// Database connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Maximum retry count for failed operations
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Maximum retry delay in seconds
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Enable sensitive data logging (development only)
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// Enable detailed errors (development only)
    /// </summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>
    /// Enable service provider caching
    /// </summary>
    public bool EnableServiceProviderCaching { get; set; } = true;

    /// <summary>
    /// Migrations assembly name
    /// </summary>
    public string? MigrationsAssembly { get; set; }

    /// <summary>
    /// Database schema name
    /// </summary>
    public string DefaultSchema { get; set; } = "dbo";

    /// <summary>
    /// Enable automatic migrations (development only)
    /// </summary>
    public bool EnableAutomaticMigrations { get; set; }
}

/// <summary>
/// Enhanced persistence configuration options with enterprise features
/// </summary>
public class PersistenceConfigurationOptions : DatabaseOptions
{
    /// <summary>
    /// Enable performance monitoring for database operations
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Enable health checks for database connectivity and performance
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Enable repository-level caching with decorators
    /// </summary>
    public bool EnableRepositoryCaching { get; set; }

    /// <summary>
    /// Default transaction behavior for operations
    /// </summary>
    public TransactionBehavior DefaultTransactionBehavior { get; set; } = TransactionBehavior.PerOperation;

    /// <summary>
    /// Cache expiration time for repository caching
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Health check options
    /// </summary>
    public PersistenceHealthCheckOptions HealthCheckOptions { get; set; } = new();

    /// <summary>
    /// Enable repository compatibility layer for legacy support
    /// </summary>
    public bool EnableRepositoryCompatibilityLayer { get; set; }
}

/// <summary>
/// Validator for DatabaseOptions
/// </summary>
public class ValidateDatabaseOptions<T> : IValidateOptions<T> where T : class
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        if (options is DatabaseOptions databaseOptions)
        {
            if (databaseOptions.MaxRetryCount < 0)
            {
                return ValidateOptionsResult.Fail("MaxRetryCount must be non-negative");
            }

            if (databaseOptions.MaxRetryDelaySeconds < 0)
            {
                return ValidateOptionsResult.Fail("MaxRetryDelaySeconds must be non-negative");
            }

            if (databaseOptions.CommandTimeout < 0)
            {
                return ValidateOptionsResult.Fail("CommandTimeout must be non-negative");
            }
        }

        return ValidateOptionsResult.Success;
    }
}