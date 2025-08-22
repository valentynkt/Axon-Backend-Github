// using Axon.Api.Common.ErrorHandling;  // Not needed for POC
using Axon.Api.Modules;
using Axon.BuildingBlocks.Web.Configuration;
using BuildingBlocks.Web.OpenApi;
using FastEndpoints;
using Mapster;
using MapsterMapper;
using System.Reflection;

namespace Axon.Api.Configuration;

/// <summary>
/// Service registration for API layer
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Register application services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="environment">Host environment</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Add core framework services
        services.AddControllers();
        services.AddFastEndpoints();
        
        // Add API documentation
        services.AddEndpointsApiExplorer();
        services.AddAspnetOpenApi();
        
        // Note: MediatR, pipeline behaviors, and validators are registered by individual modules
        // This ensures proper assembly scanning and avoids duplication
        
        // Configure Mapster
        MapsterConfig.Configure();
        services.AddMapster();
        
        // Register API modules
        RegisterApiModules(services, configuration, environment);
        
        return services;
    }

    private static void RegisterApiModules(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Discover and register all modules implementing IApiModule
        var modules = DiscoverApiModules();
        
        foreach (var module in modules)
        {
            module.ConfigureServices(services, configuration, environment);
        }
    }

    private static IEnumerable<IApiModule> DiscoverApiModules()
    {
        var modules = new List<IApiModule>();
        
        // For now, manually register modules
        // In the future, this could use reflection to auto-discover
        modules.Add(new ChatApiModule());
        
        return modules;
    }
}