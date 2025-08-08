using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using BuildingBlocks.Infrastructure.Persistence.Read;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Persistence.Caching;

/// <summary>
/// Extension methods for configuring caching repository decorators
/// Uses Scrutor library for decorator pattern implementation
/// </summary>
public static class RepositoryCachingExtensions
{
    /// <summary>
    /// Adds caching decorators to all registered repository implementations
    /// This method should be called after registering your base repositories
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="cacheExpiration">Optional cache expiration time (default: 15 minutes)</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddRepositoryCaching(
        this IServiceCollection services,
        TimeSpan? cacheExpiration = null)
    {
        // Ensure IMemoryCache is registered
        services.AddMemoryCache();

        // Decorate IReadRepository<TEntity, TId> implementations with caching (only for aggregates)
        services.Decorate(typeof(IReadRepository<,>), (inner, serviceProvider) =>
        {
            var innerType = inner.GetType();
            var interfaces = innerType.GetInterfaces();
            
            foreach (var @interface in interfaces)
            {
                if (@interface.IsGenericType && 
                    @interface.GetGenericTypeDefinition() == typeof(IReadRepository<,>))
                {
                    var genericArgs = @interface.GetGenericArguments();
                    var entityType = genericArgs[0];
                    var idType = genericArgs[1];
                    
                    // Only cache repositories for entities that implement IAggregate<TId>
                    var aggregateInterface = typeof(IAggregate<>).MakeGenericType(idType);
                    if (aggregateInterface.IsAssignableFrom(entityType))
                    {
                        var decoratorType = typeof(CachedReadRepositoryForAggregates<,>).MakeGenericType(entityType, idType);
                        var cache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                        var loggerType = typeof(Microsoft.Extensions.Logging.ILogger<>).MakeGenericType(decoratorType);
                        var logger = serviceProvider.GetRequiredService(loggerType);
                        
                        return Activator.CreateInstance(decoratorType, inner, cache, logger, cacheExpiration)!;
                    }
                }
            }
            
            return inner;
        });

        // Decorate IWriteRepository<TEntity, TId> implementations with cache invalidation
        services.Decorate(typeof(IWriteRepository<,>), (inner, serviceProvider) =>
        {
            var innerType = inner.GetType();
            var interfaces = innerType.GetInterfaces();
            
            foreach (var @interface in interfaces)
            {
                if (@interface.IsGenericType && 
                    @interface.GetGenericTypeDefinition() == typeof(IWriteRepository<,>))
                {
                    var genericArgs = @interface.GetGenericArguments();
                    var entityType = genericArgs[0];
                    var idType = genericArgs[1];
                    
                    // IWriteRepository already requires IAggregate<TId> constraint
                    var decoratorType = typeof(WriteRepositoryWithCacheInvalidation<,>).MakeGenericType(entityType, idType);
                    var cache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                    var loggerType = typeof(Microsoft.Extensions.Logging.ILogger<>).MakeGenericType(decoratorType);
                    var logger = serviceProvider.GetRequiredService(loggerType);
                    
                    return Activator.CreateInstance(decoratorType, inner, cache, logger, cacheExpiration)!;
                }
            }
            
            return inner;
        });

        return services;
    }

    /// <summary>
    /// Adds caching for a specific entity type with explicit registration
    /// Use this when you need fine-grained control over which repositories get cached
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The ID type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="cacheExpiration">Optional cache expiration time (default: 15 minutes)</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddRepositoryCachingFor<TEntity, TId>(
        this IServiceCollection services,
        TimeSpan? cacheExpiration = null)
        where TEntity : class, IAggregate<TId>
        where TId : notnull
    {
        // Ensure IMemoryCache is registered
        services.AddMemoryCache();

        // Decorate the specific read repository
        services.Decorate<IReadRepository<TEntity, TId>>((inner, serviceProvider) =>
        {
            var cache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedReadRepositoryForAggregates<TEntity, TId>>>();
            return new CachedReadRepositoryForAggregates<TEntity, TId>(inner, cache, logger, cacheExpiration);
        });

        // Decorate the specific write repository
        services.Decorate<IWriteRepository<TEntity, TId>>((inner, serviceProvider) =>
        {
            var cache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WriteRepositoryWithCacheInvalidation<TEntity, TId>>>();
            return new WriteRepositoryWithCacheInvalidation<TEntity, TId>(inner, cache, logger, cacheExpiration);
        });

        return services;
    }
}