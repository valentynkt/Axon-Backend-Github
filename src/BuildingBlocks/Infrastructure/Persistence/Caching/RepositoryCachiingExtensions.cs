using BuildingBlocks.Application.Abstractions.Persistence;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Infrastructure.Persistence.Read;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Caching;

/// <summary>
/// Extension methods for configuring caching repository decorators (Scrutor-based).
/// Call after registering your base repositories.
/// </summary>
public static class RepositoryCachingExtensions
{
    /// <summary>
    /// Adds caching decorators to all registered repository implementations.
    /// - IReadRepository&lt;TReadModel,TId&gt;: only decorates when TReadModel implements IAggregateRoot&lt;TId&gt;.
    /// - IWriteRepository&lt;TAggregate,TId&gt;: adds cache invalidation decorator.
    /// </summary>
    public static IServiceCollection AddRepositoryCaching(
        this IServiceCollection services,
        TimeSpan? cacheExpiration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ensure IMemoryCache is available
        services.AddMemoryCache();

        // Decorate IReadRepository<,> selectively (only aggregates)
        services.Decorate(typeof(IReadRepository<,>), (inner, sp) =>
        {
            var innerType = inner.GetType();
            foreach (var contract in innerType.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IReadRepository<,>))
                {
                    var args = contract.GetGenericArguments();
                    var entityType = args[0];
                    var idType = args[1];

                    // Only decorate read repos where the read model implements IIdentifiable<TId>
                    var identifiableIface = typeof(IIdentifiable<>).MakeGenericType(idType);
                    if (identifiableIface.IsAssignableFrom(entityType))
                    {
                        var decoratorType = typeof(CachedReadRepositoryForAggregates<,>).MakeGenericType(entityType, idType);

                        // Resolve logger strongly-typed to the decorator
                        var loggerType = typeof(ILogger<>).MakeGenericType(decoratorType);
                        var logger = sp.GetRequiredService(loggerType);
                        var cache = sp.GetRequiredService<IMemoryCache>();

                        // Use ActivatorUtilities so DI can still inject anything else if the ctor grows
                        return ActivatorUtilities.CreateInstance(
                            sp,
                            decoratorType,
                            inner,    // the decorated instance
                            cache,
                            logger,
                            cacheExpiration)!;
                    }
                }
            }

            // Not an aggregate-based read repo – return original
            return inner;
        });

        // Decorate IWriteRepository<,> with invalidation
        services.Decorate(typeof(IWriteRepository<,>), (inner, sp) =>
        {
            var innerType = inner.GetType();
            foreach (var contract in innerType.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IWriteRepository<,>))
                {
                    var args = contract.GetGenericArguments();
                    var entityType = args[0];
                    var idType = args[1];

                    var decoratorType = typeof(WriteRepositoryWithCacheInvalidation<,>).MakeGenericType(entityType, idType);
                    var loggerType = typeof(ILogger<>).MakeGenericType(decoratorType);
                    var logger = sp.GetRequiredService(loggerType);
                    var cache = sp.GetRequiredService<IMemoryCache>();

                    return ActivatorUtilities.CreateInstance(
                        sp,
                        decoratorType,
                        inner,
                        cache,
                        logger,
                        cacheExpiration)!;
                }
            }

            return inner;
        });

        return services;
    }

    /// <summary>
    /// Adds caching decorators for a specific aggregate type. Use for fine-grained control.
    /// </summary>
    public static IServiceCollection AddRepositoryCachingFor<TEntity, TId>(
        this IServiceCollection services,
        TimeSpan? cacheExpiration = null)
        where TEntity : class, IAggregateRoot<TId>
        where TId : IStrongId
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();

        services.Decorate<IReadRepository<TEntity, TId>>((inner, sp) =>
        {
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<CachedReadRepositoryForAggregates<TEntity, TId>>>();
            return new CachedReadRepositoryForAggregates<TEntity, TId>(inner, cache, logger, cacheExpiration);
        });

        services.Decorate<IWriteRepository<TEntity, TId>>((inner, sp) =>
        {
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<WriteRepositoryWithCacheInvalidation<TEntity, TId>>>();
            return new WriteRepositoryWithCacheInvalidation<TEntity, TId>(inner, cache, logger, cacheExpiration);
        });

        return services;
    }
}
