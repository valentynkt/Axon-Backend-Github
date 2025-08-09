using BuildingBlocks.Application.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Extension methods for configuring application layer services and options.
/// Centralizes configuration registration following the Open/Closed Principle.
/// </summary>
public static class ApplicationConfigurationExtensions
{
    /// <summary>
    /// Configures all application layer options from configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection ConfigureApplicationOptions(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure logging options
        services.Configure<LoggingOptions>(
            configuration.GetSection("Application:Logging"));

        return services;
    }

    /// <summary>
    /// Registers all application layer services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add caching services
        services.AddCachingServices();

        // Add pipeline behaviors in correct order
        services.AddPipelineBehaviors();

        return services;
    }

    /// <summary>
    /// Adds complete application layer configuration including services, options, and behaviors.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplicationLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure application options
        services.ConfigureApplicationOptions(configuration);

        // Register application services
        services.AddApplicationServices();

        return services;
    }
}