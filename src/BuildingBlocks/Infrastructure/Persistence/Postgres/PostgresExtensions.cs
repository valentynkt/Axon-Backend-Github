using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Persistence.Postgres;

public static class PostgresExtensions
{
    // ---------- Builder overload ----------
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this WebApplicationBuilder builder,
        string? connectionName = "DefaultConnection",
        Action<PostgresOptions>? configure = null)
        where TContext : DbContext, IDbContext
        => builder.Services.AddPostgresDbContext<TContext>(builder.Configuration, connectionName, configure);

    // ---------- Services overload ----------
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? connectionName = "DefaultConnection",
        Action<PostgresOptions>? configure = null)
        where TContext : DbContext, IDbContext
    {
        // Bind PG options (and allow caller overrides)
        var section = configuration.GetSection(PostgresOptions.SectionName);
        services.AddOptions<PostgresOptions>().Bind(section);
        if (configure is not null) services.Configure(configure);
        services.AddSingleton<IValidateOptions<PostgresOptions>, ValidatePostgresOptions>();

        // Copy effective PostgresOptions into DatabaseOptions, then configure provider
        return services.AddDbContext<TContext>(
            configuration,
            configureOptions: db =>
            {
                var pg = section.Get<PostgresOptions>() ?? new PostgresOptions();
                configure?.Invoke(pg);

                // Prefer named connection string, then section, then ConnectionStrings:DefaultConnection
                db.ConnectionString = !string.IsNullOrWhiteSpace(connectionName)
                    ? (configuration.GetConnectionString(connectionName) ?? pg.GetConnectionString(connectionName))
                    : (pg.ConnectionString ?? configuration.GetConnectionString("DefaultConnection") ?? "");

                db.MaxRetryCount = pg.MaxRetryCount;
                db.MaxRetryDelaySeconds = pg.MaxRetryDelaySeconds;
                db.CommandTimeout = pg.CommandTimeout;
                db.EnableSensitiveDataLogging = pg.EnableSensitiveDataLogging;
                db.EnableDetailedErrors = pg.EnableDetailedErrors;
                db.EnableServiceProviderCaching = pg.EnableServiceProviderCaching;
                db.MigrationsAssembly = pg.MigrationsAssembly;
                db.DefaultSchema = string.IsNullOrWhiteSpace(pg.DefaultSchema) ? "public" : pg.DefaultSchema;
                db.EnableAutomaticMigrations = pg.EnableAutomaticMigrations;
            },
            configureProvider: (opts, db, ctxType) =>
            {
                opts.UseNpgsql(
                    db.ConnectionString,
                    npgsql =>
                    {
                        npgsql.CommandTimeout(db.CommandTimeout);
                        npgsql.MigrationsAssembly(db.MigrationsAssembly ?? ctxType.Assembly.GetName().Name);
                        npgsql.EnableRetryOnFailure(db.MaxRetryCount, TimeSpan.FromSeconds(db.MaxRetryDelaySeconds), null);
                    });
            });
    }

    public static string GetPostgresConnectionString(this IConfiguration configuration, string? name = "DefaultConnection")
    {
        var pg = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>();
        if (pg is not null)
        {
            var viaSection = pg.GetConnectionString(name);
            if (!string.IsNullOrWhiteSpace(viaSection)) return viaSection;
        }
        return configuration.GetConnectionString(name ?? "DefaultConnection") ?? string.Empty;
    }
}

public class ValidatePostgresOptions : IValidateOptions<PostgresOptions>
{
    public ValidateOptionsResult Validate(string? name, PostgresOptions options)
    {
        var errors = new List<string>();

        if (!options.UseInMemory && string.IsNullOrWhiteSpace(options.ConnectionString) && (options.ConnectionStrings?.Count ?? 0) == 0)
            errors.Add("Either ConnectionString or a named connection in ConnectionStrings must be provided.");

        if (options.MaxRetryCount < 0) errors.Add("MaxRetryCount must be non-negative.");
        if (options.MaxRetryDelaySeconds < 0) errors.Add("MaxRetryDelaySeconds must be non-negative.");
        if (options.CommandTimeout < 0) errors.Add("CommandTimeout must be non-negative.");

        if (options.Pooling.MinPoolSize < 0) errors.Add("MinPoolSize must be non-negative.");
        if (options.Pooling.MaxPoolSize <= 0) errors.Add("MaxPoolSize must be greater than zero.");
        if (options.Pooling.MinPoolSize > options.Pooling.MaxPoolSize) errors.Add("MinPoolSize cannot be greater than MaxPoolSize.");

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}
