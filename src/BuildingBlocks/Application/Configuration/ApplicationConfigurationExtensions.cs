using BuildingBlocks.Application.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    /// Registers all application layer services with configuration and environment.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance</param>
    /// <param name="environment">The host environment</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services, 
        IConfiguration configuration, 
        IHostEnvironment environment)
    {
        // Configure application options
        services.ConfigureApplicationOptions(configuration);
        
        // Add caching services
        services.AddCachingServices();

        // Add pipeline behaviors in correct order with configuration and environment
        services.AddPipelineBehaviors(configuration, environment);

        return services;
    }
}