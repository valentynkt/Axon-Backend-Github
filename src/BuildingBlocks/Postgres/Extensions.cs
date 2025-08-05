using BuildingBlocks.EFCore;
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
        where TContext : PostgresDbContext
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
        where TContext : PostgresDbContext
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
        services.AddScoped<IPostgresDbContext>(provider => provider.GetRequiredService<TContext>());
        
        // Register repositories
        services.AddPostgresRepositories();
        
        // Register Unit of Work
        services.AddPostgresUnitOfWork<TContext>();

        return services;
    }

    /// <summary>
    /// Add PostgreSQL repositories
    /// </summary>
    public static IServiceCollection AddPostgresRepositories(this IServiceCollection services)
    {
        // Register generic repositories (Postgres-style)
        services.AddScoped(typeof(IRepository<,>), typeof(PostgresRepository<,>));
        services.AddScoped(typeof(IRepository<>), typeof(PostgresRepository<>));
        
        // Register read/write specific repositories
        services.AddScoped(typeof(IReadRepository<,>), typeof(PostgresRepository<,>));
        services.AddScoped(typeof(IWriteRepository<,>), typeof(PostgresRepository<,>));
        
        // REMOVED: Incorrect IEfRepository registrations
        // PostgresRepository does NOT implement IEfRepository interfaces
        // IEfRepository requires IAggregate<TId>, but PostgresRepository works with IEntity<TId>
        
        return services;
    }

    /// <summary>
    /// Add PostgreSQL Unit of Work services
    /// </summary>
    public static IServiceCollection AddPostgresUnitOfWork<TContext>(this IServiceCollection services)
        where TContext : class, IPostgresDbContext
    {
        // Register Postgres-style Unit of Work
        services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();
        services.AddScoped<IUnitOfWork<TContext>, PostgresUnitOfWork<TContext>>();
        services.AddScoped<IPostgresUnitOfWork<TContext>, PostgresUnitOfWork<TContext>>();
        
        // REMOVED: Incorrect IEfUnitOfWork registrations
        // PostgresUnitOfWork does NOT implement IEfUnitOfWork interfaces
        // Method signatures are incompatible: CommitAsync() vs CommitTransactionAsync()
        
        return services;
    }

    /// <summary>
    /// Configure PostgreSQL for development environment
    /// </summary>
    public static IServiceCollection ConfigurePostgresDevelopment(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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
        options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        
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
        where TContextImplementation : PostgresDbContext, TContextService
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
            var postgresOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PostgresOptions>>().Value;
            
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
        services.AddScoped(typeof(TContextService), typeof(TContextImplementation));
        services.AddScoped(typeof(TContextImplementation));

        // Register pure PostgreSQL interfaces
        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContextImplementation>());

        // Register both repository approaches
        services.AddPostgresRepositories();
        
        // Register Unit of Work services with proper constraint handling
        services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();
        
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
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    /// Enable detailed errors (development only)
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

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
    public bool EnableAutomaticMigrations { get; set; } = false;
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