using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using BuildingBlocks.Application.Behaviors;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Configuration extensions for Epic 04 Story 02 declarative query caching.
/// Sets up memory + distributed caching with intelligent key generation.
/// </summary>
public static class CachingConfiguration
{
    /// <summary>
    /// Adds caching pipeline behavior with declarative query support.
    /// Configures both memory (L1) and distributed (L2) caching providers.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <param name="configure">Optional cache configuration action</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection AddDeclarativeQueryCaching(
    this IServiceCollection services,
    Action<CacheOptions>? configure = null)
{
    // Configure cache options
    if (configure != null)
    {
        services.Configure<CacheOptions>(configure);
    }
    else
    {
        services.Configure<CacheOptions>(options =>
        {
            options.DefaultDuration = TimeSpan.FromMinutes(5);
            options.IncludeTraceInKey = false; // Default to global cache keys
            options.MemoryCacheSizeLimitMB = 100;
        });
    }

    // Add cache key generator
    services.AddSingleton<ICacheKeyGenerator, DefaultCacheKeyGenerator>();
    
    // Add cache infrastructure services
    services.AddSingleton<ICacheTagIndex, DistributedCacheTagIndex>();
    services.AddSingleton<ICacheInvalidator, CacheInvalidator>();
    
    // Add memory cache with size limits
    services.AddMemoryCache(options =>
    {
        using var serviceProvider = services.BuildServiceProvider();
        var cacheOptions = serviceProvider.GetService<IOptions<CacheOptions>>()?.Value ?? new();
        options.SizeLimit = cacheOptions.MemoryCacheSizeLimitMB * 1024 * 1024; // Convert MB to bytes
    });

    // Add distributed cache (Redis) - only if Redis connection is configured
    services.AddStackExchangeRedisCache(options =>
    {
        using var serviceProvider = services.BuildServiceProvider();
        var cacheOptions = serviceProvider.GetService<IOptions<CacheOptions>>()?.Value ?? new();
        
        options.Configuration = cacheOptions.RedisConnectionString;
        options.InstanceName = cacheOptions.InstanceName;
    });

    return services;
}

    /// <summary>
    /// Adds caching behavior to MediatR pipeline for queries.
    /// Must be called after AddMediatR.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection AddCachingPipelineBehavior(this IServiceCollection services)
    {
        // Register the caching pipeline behavior
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
        
        return services;
    }

    /// <summary>
    /// Configures caching with common development settings.
    /// Short cache durations, local Redis, trace context disabled.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection AddDevelopmentCaching(this IServiceCollection services)
    {
        return services.AddDeclarativeQueryCaching(options =>
        {
            options.DefaultDuration = TimeSpan.FromMinutes(2); // Short for development
            options.IncludeTraceInKey = false; // Global cache keys
            options.RedisConnectionString = "localhost:6379";
            options.InstanceName = "axon-dev";
            options.MemoryCacheSizeLimitMB = 50; // Smaller for development
        });
    }

    /// <summary>
    /// Configures caching with production settings.
    /// Longer cache durations, production Redis, trace context enabled.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <param name="redisConnectionString">Production Redis connection string</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection AddProductionCaching(
        this IServiceCollection services,
        string redisConnectionString)
    {
        return services.AddDeclarativeQueryCaching(options =>
        {
            options.DefaultDuration = TimeSpan.FromMinutes(15); // Longer for production
            options.IncludeTraceInKey = true; // Trace context isolation
            options.RedisConnectionString = redisConnectionString;
            options.InstanceName = "axon-prod";
            options.MemoryCacheSizeLimitMB = 200; // Larger for production
            options.CompressionThreshold = 512; // Aggressive compression
        });
    }
}