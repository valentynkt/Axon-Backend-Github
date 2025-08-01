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
        IConfiguration configuration)
    {
        // Services from srp-decomposition-specialist
        // services.AddScoped<IMessageValidator, MessageValidator>();
        // services.AddScoped<IRequestBuilder, RequestBuilder>();
        // services.AddScoped<IResponseMapper, ResponseMapper>();

        // Services from performance-optimizer
        // services.AddScoped<IMessageCache, MessageCache>();
        // services.AddMemoryCache();
        // services.Configure<CacheOptions>(configuration.GetSection("Cache"));

        // Services from clean-architecture-enforcer
        // services.AddScoped<IMessageProcessor, MessageProcessor>();
        // Additional interface implementations

        // Services from domain-modeler
        // Domain services and aggregates registration

        // Services from testing-strategist
        // Test infrastructure services (only in test environment)

        // Services from monitoring-specialist
        // services.AddScoped<IPerformanceMetrics, PerformanceMetrics>();
        // services.AddApplicationInsights();

        return services;
    }
}