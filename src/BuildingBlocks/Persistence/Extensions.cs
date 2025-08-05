using System;
using System.Linq.Expressions;
using BuildingBlocks.Core.Model;
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
/// Provides unified PostgreSQL configuration and repository registrations
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Add PostgreSQL DbContext with modern CQRS architecture
    /// </summary>
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this WebApplicationBuilder builder,
        Action<PostgresOptions>? configurator = null)
        where TContext : DbContext, IDbContext
    {
        return builder.Services.AddPostgresDbContext<TContext>(builder.Configuration, configurator);
    }

    /// <summary>
    /// Add PostgreSQL DbContext with configuration and modern architecture
    /// </summary>
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PostgresOptions>? configurator = null)
        where TContext : DbContext, IDbContext
    {
        // Configure PostgreSQL options with Aspire support
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
            ConfigurePostgresDbContext(options, postgresOptions, typeof(TContext));
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
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.Services.AddValidateOptions<PostgresOptions>();

        builder.Services.AddDbContext<TContext>((sp, options) =>
        {
            var aspireConnectionString = builder.Configuration.GetConnectionString(connectionName.Kebaberize());
            var connectionString = aspireConnectionString ?? sp.GetRequiredService<PostgresOptions>().ConnectionString;

            ArgumentException.ThrowIfNullOrEmpty(connectionString);

            options.UseNpgsql(connectionString, dbOptions =>
            {
                dbOptions.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
            })
            .UseSnakeCaseNamingConvention();

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
        Action<PostgresOptions>? configurator = null)
        where TWriteContext : DbContext, IWriteDbContext<object>
        where TReadContext : DbContext, IReadDbContext<object>
    {
        // Configure shared PostgreSQL options
        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(nameof(PostgresOptions)))
            .PostConfigure(options =>
            {
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

        // Register Write Context
        services.AddDbContext<TWriteContext>((serviceProvider, options) =>
        {
            var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            ConfigurePostgresDbContext(options, postgresOptions, typeof(TWriteContext));
        });

        // Register Read Context (optimized for queries)
        services.AddDbContext<TReadContext>((serviceProvider, options) =>
        {
            var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            ConfigurePostgresDbContext(options, postgresOptions, typeof(TReadContext));
            
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
        // Register repositories
        services.AddScoped(typeof(IReadRepository<,>), typeof(PostgresReadRepository<,>));
        services.AddScoped(typeof(IWriteRepository<,>), typeof(PostgresWriteRepository<,>));
        
        // Register Unit of Work
        services.AddScoped(typeof(IWriteUnitOfWork<>), typeof(PostgresWriteUnitOfWork<>));
        
        // Register infrastructure services
        services.AddScoped<ISeedManager, SeedManager>();
        
        return services;
    }

    /// <summary>
    /// Use database migrations
    /// </summary>
    public static IApplicationBuilder UseMigration<TContext>(this IApplicationBuilder app)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(app);
        
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        context.Database.Migrate();
        return app;
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
    /// Configure PostgreSQL DbContext options
    /// </summary>
    private static void ConfigurePostgresDbContext(
        DbContextOptionsBuilder options, 
        PostgresOptions postgresOptions, 
        Type contextType)
    {
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
            else
            {
                npgsqlOptions.MigrationsAssembly(contextType.Assembly.GetName().Name);
            }

            if (postgresOptions.CommandTimeout > 0)
            {
                npgsqlOptions.CommandTimeout(postgresOptions.CommandTimeout);
            }
        });

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

        // Use snake_case naming convention
        options.UseSnakeCaseNamingConvention();
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