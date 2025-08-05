using System;
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL dependency injection extensions
/// Maintains same patterns as MongoDB extensions but for PostgreSQL
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Add PostgreSQL DbContext with all repository and unit of work services
    /// </summary>
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this WebApplicationBuilder builder,
        Action<PostgresOptions>? configurator = null)
        where TContext : DbContext, IDbContext
    {
        return builder.Services.AddPostgresDbContext<TContext>(builder.Configuration, configurator);
    }

    /// <summary>
    /// Add PostgreSQL DbContext with configuration
    /// </summary>
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PostgresOptions>? configurator = null)
        where TContext : DbContext, IDbContext
    {
        // Configure PostgreSQL options
        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(nameof(PostgresOptions)))
            .PostConfigure(options =>
            {
                // Support Aspire connection strings
                var aspireConnectionString = configuration.GetConnectionString("postgres");
                if (!string.IsNullOrEmpty(aspireConnectionString))
                {
                    options.ConnectionString = aspireConnectionString;
                }
            });

        if (configurator != null)
        {
            services.Configure(configurator);
        }
        else
        {
            services.AddValidateOptions<PostgresOptions>();
        }

        // Register DbContext with PostgreSQL
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            
            options.UseNpgsql(postgresOptions.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: postgresOptions.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(postgresOptions.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
                
                if (!string.IsNullOrEmpty(postgresOptions.MigrationsAssembly))
                {
                    npgsqlOptions.MigrationsAssembly(postgresOptions.MigrationsAssembly);
                }
            });

            ConfigureDbContextOptions(options, postgresOptions);
        });

        // Register context interfaces
        services.AddScoped<IDbContext>(provider => provider.GetRequiredService<TContext>());
        
        // Register repositories
        services.AddPostgresRepositories();
        
        // Register Unit of Work
        services.AddPostgresUnitOfWork<TContext>();

        return services;
    }

    /// <summary>
    /// Add PostgreSQL repositories - DEPRECATED
    /// Use BuildingBlocks.Persistence registrations instead
    /// </summary>
    [Obsolete("Use BuildingBlocks.Persistence repository registrations instead. This method will be removed in a future version.")]
    public static IServiceCollection AddPostgresRepositories(this IServiceCollection services)
    {
        // REMOVED: PostgreSQL repository classes have been deleted
        // Use BuildingBlocks.Persistence.Read.PostgresReadRepository and 
        // BuildingBlocks.Persistence.Write.PostgresWriteRepository instead
        throw new NotSupportedException("PostgreSQL repository registrations removed. Use BuildingBlocks.Persistence instead.");
    }

    /// <summary>
    /// Add PostgreSQL Unit of Work services - DEPRECATED
    /// Use BuildingBlocks.Persistence Unit of Work registrations instead
    /// </summary>
    [Obsolete("Use BuildingBlocks.Persistence.Write.PostgresWriteUnitOfWork registrations instead. This method will be removed in a future version.")]
    public static IServiceCollection AddPostgresUnitOfWork<TContext>(this IServiceCollection services)
        where TContext : class, IDbContext
    {
        // REMOVED: PostgreSQL Unit of Work classes have been deleted
        // Use BuildingBlocks.Persistence.Write.PostgresWriteUnitOfWork instead
        throw new NotSupportedException("PostgreSQL Unit of Work registrations removed. Use BuildingBlocks.Persistence instead.");
    }

    /// <summary>
    /// Configure PostgreSQL for development environment
    /// </summary>
    public static IServiceCollection ConfigurePostgresDevelopment(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration; // Parameter reserved for future use
        
        services.Configure<PostgresOptions>(options =>
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
    /// Configure PostgreSQL for production environment
    /// </summary>
    public static IServiceCollection ConfigurePostgresProduction(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration; // Parameter reserved for future use
        
        services.Configure<PostgresOptions>(options =>
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
    /// Configure DbContext options based on PostgresOptions
    /// </summary>
    private static void ConfigureDbContextOptions(DbContextOptionsBuilder options, PostgresOptions postgresOptions)
    {
        // Development settings
        if (postgresOptions.EnableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
        }

        if (postgresOptions.EnableDetailedErrors)
        {
            options.EnableDetailedErrors();
        }

        // Performance settings
        if (postgresOptions.EnableServiceProviderCaching)
        {
            options.EnableServiceProviderCaching();
        }

        // Query splitting for better performance with includes
        // options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery); // TODO: Configure in DbContext
        
        // Configure command timeout
        if (postgresOptions.CommandTimeout > 0)
        {
            options.UseNpgsql(npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(postgresOptions.CommandTimeout);
            });
        }
    }

    /// <summary>
    /// Add validation for PostgresOptions
    /// </summary>
    private static IServiceCollection AddValidateOptions<T>(this IServiceCollection services)
        where T : class
    {
        services.AddSingleton<IValidateOptions<T>, ValidatePostgresOptions<T>>();
        return services;
    }

    /// <summary>
    /// Add pure PostgreSQL DbContext with service interface (EFCore-style)
    /// Provides both service and implementation registration
    /// </summary>
    public static IServiceCollection AddPostgresDbContext<TContextService, TContextImplementation>(
        this IServiceCollection services, 
        IConfiguration configuration, 
        Action<PostgresOptions>? configurator = null)
        where TContextService : class, IDbContext
        where TContextImplementation : DbContext, IDbContext, TContextService
    {
        // Configure PostgresOptions with Aspire-aware defaults
        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(nameof(PostgresOptions)))
            .PostConfigure(options =>
            {
                var aspireConnectionString = configuration.GetConnectionString("postgres");
                options.ConnectionString = aspireConnectionString ?? options.ConnectionString;
            });

        if (configurator is { })
        {
            services.Configure(nameof(PostgresOptions), configurator);
        }
        else
        {
            services.AddValidateOptions<PostgresOptions>();
        }

        // Register Entity Framework DbContext
        services.AddDbContext<TContextImplementation>((serviceProvider, options) =>
        {
            var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            
            options.UseNpgsql(postgresOptions.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: postgresOptions.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(postgresOptions.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
            });

            ConfigureDbContextOptions(options, postgresOptions);
        });

        // Register interfaces for dependency injection
        services.AddScoped<TContextService, TContextImplementation>();
        services.AddScoped<TContextImplementation>();

        // Register pure PostgreSQL interfaces
        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContextImplementation>());

        // Register both repository approaches
        services.AddPostgresRepositories();
        
        // Register Unit of Work services with proper constraint handling
        // PostgresUnitOfWork removed - use BuildingBlocks.Persistence instead
        
        // REMOVED: Incorrect IEfUnitOfWork registrations
        // PostgresUnitOfWork does NOT implement IEfUnitOfWork interfaces

        return services;
    }
}

/// <summary>
/// PostgreSQL configuration options
/// </summary>
public class PostgresOptions
{
    /// <summary>
    /// PostgreSQL connection string
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
    public string DefaultSchema { get; set; } = "public";

    /// <summary>
    /// Enable automatic migrations (development only)
    /// </summary>
    public bool EnableAutomaticMigrations { get; set; }
}

/// <summary>
/// Validator for PostgresOptions
/// </summary>
public class ValidatePostgresOptions<T> : IValidateOptions<T> where T : class
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        if (options is PostgresOptions postgresOptions)
        {
            if (string.IsNullOrEmpty(postgresOptions.ConnectionString))
            {
                return ValidateOptionsResult.Fail("PostgreSQL connection string is required");
            }

            if (postgresOptions.MaxRetryCount < 0)
            {
                return ValidateOptionsResult.Fail("MaxRetryCount must be non-negative");
            }

            if (postgresOptions.MaxRetryDelaySeconds < 0)
            {
                return ValidateOptionsResult.Fail("MaxRetryDelaySeconds must be non-negative");
            }

            if (postgresOptions.CommandTimeout < 0)
            {
                return ValidateOptionsResult.Fail("CommandTimeout must be non-negative");
            }
        }

        return ValidateOptionsResult.Success;
    }
}