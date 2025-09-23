using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Common;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Persistence;

public static class Extensions
{
    // ---------- AddDbContext (builder overload) ----------
    // Provider-agnostic. Provider must be supplied by the caller (e.g., PostgresExtensions).
    public static IServiceCollection AddDbContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TContext>(
        this WebApplicationBuilder builder,
        Action<DatabaseOptions>? configureOptions = null,
        Action<DbContextOptionsBuilder, DatabaseOptions, Type>? configureProvider = null)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddDbContext<TContext>(builder.Configuration, configureOptions, configureProvider);
    }

    // ---------- AddDbContext (services overload) ----------
    // Provider-agnostic. Provider must be supplied by the caller (e.g., PostgresExtensions).
    public static IServiceCollection AddDbContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DatabaseOptions>? configureOptions = null,
        Action<DbContextOptionsBuilder, DatabaseOptions, Type>? configureProvider = null)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(nameof(DatabaseOptions)));

        if (configureOptions is not null) services.Configure(configureOptions);
        else services.AddValidateOptions<DatabaseOptions>();

        services.AddDbContext<TContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            // In-memory path (for tests/dev)
            if (db.UseInMemory)
            {
                options.UseInMemoryDatabase(typeof(TContext).Name);
            }
            else
            {
                // Provider must be configured by the caller (e.g., UseNpgsql)
                if (configureProvider is null)
                    throw new InvalidOperationException(
                        $"No provider configured for {typeof(TContext).Name}. " +
                        "Call the Postgres (or other provider) extension that supplies UseXxx().");

                configureProvider(options, db, typeof(TContext));
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

            if (db.EnableSensitiveDataLogging) options.EnableSensitiveDataLogging();
            if (db.EnableDetailedErrors) options.EnableDetailedErrors();
            if (db.EnableServiceProviderCaching) options.EnableServiceProviderCaching();
        });

        // Common registrations
        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContext>());
// after: services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContext>());

        var writeIface = typeof(TContext).GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWriteDbContext<>));

        if (writeIface is not null)
        {
            var moduleType = writeIface.GetGenericArguments()[0];
            var uowIface   = typeof(IWriteUnitOfWork<>).MakeGenericType(moduleType);
            var uowImpl    = typeof(EfUnitOfWork<,>)
                .MakeGenericType(typeof(TContext), moduleType);

            services.AddScoped(uowIface, sp =>
                Activator.CreateInstance(uowImpl, sp.GetRequiredService<TContext>())!);

            // Optional: expose a single non-generic UoW for apps with only one write context
            services.TryAddScoped<IWriteUnitOfWork>(
                sp => (IWriteUnitOfWork)sp.GetRequiredService(uowIface));
        }

        services.AddScoped(typeof(IReadRepository<,>), typeof(EfReadRepository<,>));
        services.AddScoped(typeof(IWriteRepository<,>), typeof(EfWriteRepository<,>));
        services.AddScoped(typeof(IReadRepository<>), typeof(EfReadRepository<>));
        services.AddScoped(typeof(IWriteRepository<>), typeof(EfWriteRepository<>));
        services.AddScoped(typeof(ISpecificationReadRepository<>), typeof(EfSpecificationReadRepository<>));
        services.AddScoped<ISeedManager, SeedManager>();

        return services;
    }

    // ---------- Optional: explicit in-memory helper ----------
    public static IServiceCollection AddInMemoryDbContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TContext>(
        this WebApplicationBuilder builder,
        string? nameSuffix = null)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Services.AddDbContext<TContext>((_, options) =>
        {
            var dbName = $"{typeof(TContext).Name}{(string.IsNullOrWhiteSpace(nameSuffix) ? "" : "_" + nameSuffix.Kebaberize())}";
            options.UseInMemoryDatabase(dbName);
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        })
        .AddScoped<IDbContext>(sp => sp.GetRequiredService<TContext>())
        .AddScoped(typeof(IReadRepository<,>), typeof(EfReadRepository<,>))
        .AddScoped(typeof(IWriteRepository<,>), typeof(EfWriteRepository<,>))
        .AddScoped(typeof(IReadRepository<>), typeof(EfReadRepository<>))
        .AddScoped(typeof(IWriteRepository<>), typeof(EfWriteRepository<>))
        .AddScoped(typeof(ISpecificationReadRepository<>), typeof(EfSpecificationReadRepository<>))
        .AddScoped<ISeedManager, SeedManager>();
    }

    // ---------- Migrate + Seed at startup ----------
    public static IApplicationBuilder UseMigration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TContext>(this IApplicationBuilder app)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(app);
        MigrateAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        SeedAsync(app.ApplicationServices).GetAwaiter().GetResult();
        return app;
    }

    private static async Task MigrateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TContext>(IServiceProvider sp)
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
            if (typeof(Core.Domain.Entities.Abstractions.ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var p = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(p, nameof(Core.Domain.Entities.Abstractions.ISoftDeletable.IsDeleted));
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

    private static IServiceCollection AddDbContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> builder)
        where TContext : DbContext, IDbContext
    {
        services.AddDbContext<TContext>(builder);
        return services;
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
