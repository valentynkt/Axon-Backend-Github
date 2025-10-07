using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Configuration;

/// <summary>
/// Infrastructure layer service registration.
/// Single entry point for all infrastructure services.
/// </summary>
public static class InfrastructureRegistration
{
    /// <summary>
    /// Registers all infrastructure services.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration instance (reserved for future use)</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        _ = configuration; // Reserved for future infrastructure configuration

        // Future: Add infrastructure services here
        // - Caching
        // - File storage
        // - Email services
        // - SMS services
        // - etc.

        return services;
    }
}