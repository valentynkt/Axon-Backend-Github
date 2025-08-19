using BuildingBlocks.Core.Abstractions.Caching;
using BuildingBlocks.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Minimal caching registration: IMemoryCache + in-process IDistributedCache.
/// Swap to Redis later by replacing AddDistributedMemoryCache with AddStackExchangeRedisCache.
/// </summary>
public static class CachingServiceExtensions
{
    public static IServiceCollection AddCachingServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddDistributedMemoryCache(); // replace with Redis if/when needed
        
        // Register idempotency cache implementation
        services.AddSingleton<IIdempotencyCache, InMemoryIdempotencyCache>();
        
        return services;
    }
}