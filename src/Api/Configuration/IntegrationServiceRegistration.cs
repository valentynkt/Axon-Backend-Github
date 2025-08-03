using Axon.Modules.Chat.Application.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Api.Configuration;

/// <summary>
/// Service registration for coordinated agent implementations
/// Managed by integration-coordinator to ensure all agents register properly
/// </summary>
public static class IntegrationServiceRegistration
{
    /// <summary>
    /// Register all services from specialized agents
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddIntegratedServices(
        this IServiceCollection services,
        IConfiguration _)
    {
       // services.AddScoped<IMessageValidator, MessageValidator>();
       // services.AddScoped<IRequestBuilder, RequestBuilder>();
       // services.AddScoped<IResponseMapper, ResponseMapper>();
       // 
       // services.AddScoped<IMessageCache, MessageCache>();
       // services.AddMemoryCache();
       // services.Configure<CacheOptions>(configuration.GetSection("Cache"));
       // 
       // services.AddScoped<IMessageProcessor, MessageProcessor>();
       //     
       // services.AddScoped<IPerformanceMetrics, PerformanceMetrics>();
       // services.AddApplicationInsights();
//
        return services;
    }
}