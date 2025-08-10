using System.Linq.Expressions;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using BuildingBlocks.Infrastructure.Persistence.Read;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Humanizer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Persistence;

public static class Extensions
{
    // ---------- AddDbContext (builder overload) ----------
    public static IServiceCollection AddDbContext<TContext>(
        this WebApplicationBuilder builder,
        Action<DatabaseOptions>? configurator = null)
        where TContext : DbContext, IDbContext
        => builder.Services.AddDbContext<TContext>(builder.Configuration, configurator);

    // ---------- AddDbContext (services overload) ----------
    public static IServiceCollection AddDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DatabaseOptions>? configurator = null)
        where TContext : DbContext, IDbContext
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(nameof(DatabaseOptions)));

        if (configurator is not null) services.Configure(configurator);
        else services.AddValidateOptions<DatabaseOptions>();

        services.AddDbContext<TContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            ConfigureDbContext(options, db, typeof(TContext));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContext>());

        // Minimal repo + UoW registrations (Application abstractions)
        services.AddScoped(typeof(IReadRepository<,>), typeof(EfReadRepository<,>));
        services.AddScoped(typeof(IWriteRepository<,>), typeof(EfWriteRepository<,>));
        services.AddScoped(typeof(IReadRepository<>), typeof(EfReadRepository<>));
        services.AddScoped(typeof(IWriteRepository<>), typeof(EfWriteRepository<>));
        services.AddScoped<IWriteUnitOfWork, EfWriteUnitOfWork>();

        services.AddScoped<ISeedManager, Infrastructure.SeedManager>();
        return services;
    }

    // ---------- AddCustomDbContext (simple in-memory for dev/tests) ----------
    public static IServiceCollection AddCustomDbContext<TContext>(
        this WebApplicationBuilder builder,
        string? connectionName = "")
        where TContext : DbContext, IDbContext
    {
        builder.Services.AddValidateOptions<DatabaseOptions>();

        builder.Services.AddDbContext<TContext>((_, options) =>
        {
            options.UseInMemoryDatabase($"{typeof(TContext).Name}_{connectionName.Kebaberize()}");
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        // same minimal registrations
        builder.Services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContext>());
        builder.Services.AddScoped(typeof(IReadRepository<,>), typeof(EfReadRepository<,>));
        builder.Services.AddScoped(typeof(IWriteRepository<,>), typeof(EfWriteRepository<,>));
        builder.Services.AddScoped(typeof(IReadRepository<>), typeof(EfReadRepository<>));
        builder.Services.AddScoped(typeof(IWriteRepository<>), typeof(EfWriteRepository<>));
        builder.Services.AddScoped<IWriteUnitOfWork, EfWriteUnitOfWork>();
        builder.Services.AddScoped<ISeedManager, SeedManager>();

        return builder.Services;
    }

    // ---------- Migrate + Seed at startup ----------
    public static IApplicationBuilder UseMigration<TContext>(this IApplicationBuilder app)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(app);
        MigrateAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        SeedAsync(app.ApplicationServices).GetAwaiter().GetResult();
        return app;
    }

    private static async Task MigrateAsync<TContext>(IServiceProvider sp)
        where TContext : DbContext, IDbContext
    {
        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
        var pending = await ctx.Database.GetPendingMigrationsAsync();

        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} pending migrations...", pending.Count());
            await ctx.Database.MigrateAsync();
            logger.LogInformation("Migrations applied.");
        }
    }

    private static async Task SeedAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<ISeedManager>();
        await manager.ExecuteSeedAsync();
    }

    // ---------- Helpers ----------
    public static void FilterSoftDeletedProperties(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BuildingBlocks.Core.Domain.Entities.Abstractions.ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var p = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(p, nameof(BuildingBlocks.Core.Domain.Entities.Abstractions.ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(prop), p);
                entityType.SetQueryFilter(filter);
            }
        }
    }

    public static void ToSnakeCaseTables(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetTableName()!.Underscore());

            foreach (var property in entity.GetProperties())
                property.SetColumnName(property.GetColumnName().Underscore());

            foreach (var key in entity.GetKeys())
                key.SetName(key.GetName()!.Underscore());

            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(fk.GetConstraintName()!.Underscore());

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(index.GetDatabaseName()!.Underscore());
        }
    }

    private static void ConfigureDbContext(
        DbContextOptionsBuilder options,
        DatabaseOptions db,
        Type ctxType)
    {
        if (db.UseInMemory)
        {
            options.UseInMemoryDatabase(ctxType.Name);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(db.ConnectionString))
                throw new InvalidOperationException($"[{ctxType.Name}] ConnectionString is required when UseInMemory = false.");

            options.UseNpgsql(
                db.ConnectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(db.MigrationsAssembly ?? ctxType.Assembly.GetName().Name);
                    npgsql.CommandTimeout(db.CommandTimeout);
                    npgsql.EnableRetryOnFailure(db.MaxRetryCount, TimeSpan.FromSeconds(db.MaxRetryDelaySeconds), null);
                });
        }

        if (db.EnableSensitiveDataLogging) options.EnableSensitiveDataLogging();
        if (db.EnableDetailedErrors) options.EnableDetailedErrors();
        if (db.EnableServiceProviderCaching) options.EnableServiceProviderCaching();
    }

    private static IServiceCollection AddValidateOptions<T>(this IServiceCollection services)
        where T : class
    {
        services.AddSingleton<IValidateOptions<T>, ValidateDatabaseOptions<T>>();
        return services;
    }
}

// ---------- Options + validation ----------
public class DatabaseOptions
{
    public bool   UseInMemory { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public int    MaxRetryCount { get; set; } = 3;
    public int    MaxRetryDelaySeconds { get; set; } = 30;
    public int    CommandTimeout { get; set; } = 30;
    public bool   EnableSensitiveDataLogging { get; set; }
    public bool   EnableDetailedErrors { get; set; }
    public bool   EnableServiceProviderCaching { get; set; } = true;
    public string? MigrationsAssembly { get; set; }
    public string  DefaultSchema { get; set; } = "dbo";
    public bool    EnableAutomaticMigrations { get; set; }
}

public class ValidateDatabaseOptions<T> : IValidateOptions<T> where T : class
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        if (options is DatabaseOptions o)
        {
            if (!o.UseInMemory && string.IsNullOrWhiteSpace(o.ConnectionString))
                return ValidateOptionsResult.Fail("ConnectionString must be set when UseInMemory is false.");
            if (o.MaxRetryCount < 0) return ValidateOptionsResult.Fail("MaxRetryCount must be non-negative.");
            if (o.MaxRetryDelaySeconds < 0) return ValidateOptionsResult.Fail("MaxRetryDelaySeconds must be non-negative.");
            if (o.CommandTimeout < 0) return ValidateOptionsResult.Fail("CommandTimeout must be non-negative.");
        }
        return ValidateOptionsResult.Success;
    }
}
