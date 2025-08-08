using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Persistence.Postgres;

/// <summary>
/// PostgreSQL-specific extensions that provide enterprise features
/// while maintaining backward compatibility with existing PostgresOptions usage
/// </summary>
public static class PostgresExtensions
{
    /// <summary>
    /// Configure PostgreSQL with Clean Architecture patterns (backward compatible)
    /// This method maintains compatibility with existing PostgresOptions usage patterns
    /// </summary>
    /// <summary>
    /// Configure PostgreSQL with Clean Architecture patterns (backward compatible)
    /// This method maintains compatibility with existing PostgresOptions usage patterns
    /// </summary>
    public static WebApplicationBuilder AddPostgresWithCleanArchitecture<TContext>(
        this WebApplicationBuilder builder,
        string? connectionName = "DefaultConnection",
        Action<PostgresOptions>? configureOptions = null)
        where TContext : DbContext, IDbContext
    {
        var options = new PostgresOptions();
        configureOptions?.Invoke(options);

        // Bind from configuration if available
        var postgresSection = builder.Configuration.GetSection(PostgresOptions.SectionName);
        if (postgresSection.Exists())
        {
            postgresSection.Bind(options);
            configureOptions?.Invoke(options); // Apply any overrides
        }

        // Register PostgresOptions for backward compatibility
        builder.Services.Configure<PostgresOptions>(postgresSection);
        builder.Services.AddSingleton<IValidateOptions<PostgresOptions>, ValidatePostgresOptions>();

        // Use our enhanced persistence configuration - FIX: Remove connectionName parameter
        builder.AddPersistenceWithCleanArchitecture<TContext>(persistenceOptions =>
        {
            // Copy PostgresOptions to PersistenceConfigurationOptions
            CopyPostgresOptionsToPersistenceOptions(options, persistenceOptions);
            
            // Set connection string if provided
            if (!string.IsNullOrEmpty(connectionName))
            {
                var connectionString = builder.Configuration.GetConnectionString(connectionName);
                if (!string.IsNullOrEmpty(connectionString))
                {
                    persistenceOptions.ConnectionString = connectionString;
                }
            }
        });

        return builder;
    }

    /// <summary>
    /// Add PostgreSQL DbContext with enterprise features
    /// Maintains backward compatibility with existing patterns
    /// </summary>
    /// <summary>
    /// Add PostgreSQL DbContext with enterprise features
    /// Maintains backward compatibility with existing patterns
    /// </summary>
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PostgresOptions>? configureOptions = null)
        where TContext : DbContext, IDbContext
    {
        var options = new PostgresOptions();
        var postgresSection = configuration.GetSection(PostgresOptions.SectionName);
        
        if (postgresSection.Exists())
        {
            postgresSection.Bind(options);
        }
        
        configureOptions?.Invoke(options);

        // Register PostgresOptions for backward compatibility
        services.Configure<PostgresOptions>(postgresSection);
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Use the enhanced persistence layer - FIX: Use DatabaseOptions instead of PersistenceConfigurationOptions
        return services.AddDbContext<TContext>(configuration, databaseOptions =>
        {
            CopyPostgresOptionsToDatabaseOptions(options, databaseOptions);
        });
    }

    /// <summary>
    /// Get PostgreSQL connection string by name with fallback support
    /// Maintains backward compatibility with existing connection string patterns
    /// </summary>
    public static string GetPostgresConnectionString(
        this IConfiguration configuration, 
        string? connectionName = null)
    {
        var options = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>();
        
        if (options == null)
        {
            // Fallback to standard connection strings section
            var connectionString = configuration.GetConnectionString(connectionName ?? "DefaultConnection");
            return connectionString ?? string.Empty;
        }

        return options.GetConnectionString(connectionName);
    }

    /// <summary>
    /// Configure PostgreSQL-specific database context options
    /// Optimized for PostgreSQL performance and features
    /// </summary>
    public static void ConfigurePostgresDbContext(
        DbContextOptionsBuilder options,
        PostgresOptions postgresOptions,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(postgresOptions);

        // Configure Npgsql/PostgreSQL specific options
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(postgresOptions.CommandTimeout);
            
            if (!string.IsNullOrEmpty(postgresOptions.MigrationsAssembly))
            {
                npgsqlOptions.MigrationsAssembly(postgresOptions.MigrationsAssembly);
            }

            // PostgreSQL-specific optimizations
            if (postgresOptions.Performance.EnablePreparedStatements)
            {
                npgsqlOptions.EnableRetryOnFailure(
                    postgresOptions.MaxRetryCount,
                    TimeSpan.FromSeconds(postgresOptions.MaxRetryDelaySeconds),
                    null);
            }
        });

        // Apply general database options
        if (postgresOptions.EnableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
        }

        if (postgresOptions.EnableDetailedErrors)
        {
            options.EnableDetailedErrors();
        }

        if (postgresOptions.EnableServiceProviderCaching)
        {
            options.EnableServiceProviderCaching();
        }

        // Configure query tracking for read contexts
        if (typeof(IReadDbContext<object>).IsAssignableFrom(options.Options.ContextType))
        {
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
    }

    /// <summary>
    /// Copy PostgresOptions to PersistenceConfigurationOptions for enterprise features
    /// </summary>
    private static void CopyPostgresOptionsToPersistenceOptions(
        PostgresOptions postgresOptions,
        PersistenceConfigurationOptions persistenceOptions)
    {
        // Basic database options
        persistenceOptions.ConnectionString = postgresOptions.ConnectionString;
        persistenceOptions.MaxRetryCount = postgresOptions.MaxRetryCount;
        persistenceOptions.MaxRetryDelaySeconds = postgresOptions.MaxRetryDelaySeconds;
        persistenceOptions.CommandTimeout = postgresOptions.CommandTimeout;
        persistenceOptions.EnableSensitiveDataLogging = postgresOptions.EnableSensitiveDataLogging;
        persistenceOptions.EnableDetailedErrors = postgresOptions.EnableDetailedErrors;
        persistenceOptions.EnableServiceProviderCaching = postgresOptions.EnableServiceProviderCaching;
        persistenceOptions.MigrationsAssembly = postgresOptions.MigrationsAssembly;
        persistenceOptions.DefaultSchema = postgresOptions.DefaultSchema;
        persistenceOptions.EnableAutomaticMigrations = postgresOptions.EnableAutomaticMigrations;

        // Enterprise features
        persistenceOptions.EnablePerformanceMonitoring = postgresOptions.EnablePerformanceMonitoring;
        persistenceOptions.EnableHealthChecks = postgresOptions.EnableHealthChecks;
        persistenceOptions.EnableRepositoryCaching = postgresOptions.EnableRepositoryCaching;
        persistenceOptions.DefaultTransactionBehavior = postgresOptions.DefaultTransactionBehavior;
        persistenceOptions.CacheExpiration = postgresOptions.CacheExpiration;
        persistenceOptions.HealthCheckOptions = postgresOptions.HealthCheckOptions;
        persistenceOptions.EnableRepositoryCompatibilityLayer = postgresOptions.EnableRepositoryCompatibilityLayer;
    }

    /// <summary>
    /// Copy PostgresOptions to DatabaseOptions for basic database configuration
    /// </summary>
    private static void CopyPostgresOptionsToDatabaseOptions(
        PostgresOptions postgresOptions,
        DatabaseOptions databaseOptions)
    {
        // Basic database options
        databaseOptions.ConnectionString = postgresOptions.ConnectionString;
        databaseOptions.MaxRetryCount = postgresOptions.MaxRetryCount;
        databaseOptions.MaxRetryDelaySeconds = postgresOptions.MaxRetryDelaySeconds;
        databaseOptions.CommandTimeout = postgresOptions.CommandTimeout;
        databaseOptions.EnableSensitiveDataLogging = postgresOptions.EnableSensitiveDataLogging;
        databaseOptions.EnableDetailedErrors = postgresOptions.EnableDetailedErrors;
        databaseOptions.EnableServiceProviderCaching = postgresOptions.EnableServiceProviderCaching;
        databaseOptions.MigrationsAssembly = postgresOptions.MigrationsAssembly;
        databaseOptions.DefaultSchema = postgresOptions.DefaultSchema;
        databaseOptions.EnableAutomaticMigrations = postgresOptions.EnableAutomaticMigrations;
    }
}

/// <summary>
/// Validation for PostgresOptions
/// </summary>
public class ValidatePostgresOptions : IValidateOptions<PostgresOptions>
{
    public ValidateOptionsResult Validate(string? name, PostgresOptions options)
    {
        var errors = new List<string>();

        if (options.MaxRetryCount < 0)
        {
            errors.Add("MaxRetryCount must be non-negative");
        }

        if (options.MaxRetryDelaySeconds < 0)
        {
            errors.Add("MaxRetryDelaySeconds must be non-negative");
        }

        if (options.CommandTimeout < 0)
        {
            errors.Add("CommandTimeout must be non-negative");
        }

        if (options.Pooling.MinPoolSize < 0)
        {
            errors.Add("MinPoolSize must be non-negative");
        }

        if (options.Pooling.MaxPoolSize <= 0)
        {
            errors.Add("MaxPoolSize must be greater than zero");
        }

        if (options.Pooling.MinPoolSize > options.Pooling.MaxPoolSize)
        {
            errors.Add("MinPoolSize cannot be greater than MaxPoolSize");
        }

        return errors.Count > 0 
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}