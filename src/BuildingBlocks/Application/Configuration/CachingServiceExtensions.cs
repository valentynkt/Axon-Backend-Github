using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Extension methods for configuring caching services.
/// Follows the Dependency Inversion Principle by registering abstractions.
/// </summary>
public static class CachingServiceExtensions
{
    /// <summary>
    /// Registers caching services with their default implementations.
    /// Uses abstraction-based registration for better testability and flexibility.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCachingServices(this IServiceCollection services)
    {
        // Register caching abstractions with their implementations
        services.AddSingleton<IContentHasher, Sha256ContentHasher>();
        services.AddSingleton<ICacheKeyBuilder, DefaultCacheKeyBuilder>();
        services.AddSingleton<ICacheKeyGenerator, DefaultCacheKeyGenerator>();

        return services;
    }
}