// /BuildingBlocks/Application/Configuration/ApplicationConfigurationExtensions.cs
#nullable enable
using BuildingBlocks.Application.Behaviors; // AddApplicationPipelineBehaviors
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Central place to wire Application-layer services & behaviors.
/// </summary>
public static class ApplicationConfigurationExtensions
{
    /// <summary>
    /// Bind Application-layer options (keep empty unless you add new options).
    /// </summary>
    public static IServiceCollection ConfigureApplicationOptions(
        this IServiceCollection services)
    {
        // NOTE:
        // We removed RequestLoggingBehavior, so LoggingOptions binding is no longer needed.
        // If you add ObservabilityOptions or others later, bind them here.

        return services;
    }

    /// <summary>
    /// Registers Application services and pipeline behaviors in the correct order.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        // Options (currently no-op; kept for future additions)
        services.ConfigureApplicationOptions();

        // Caching primitives (assumes you have this; otherwise add Memory/Distributed cache here)
        services.AddCachingServices();

        // MediatR pipeline (Observability → Validation → Caching → Retry → Idempotency → UoW)
        // Validators are auto-registered inside this call.
        services.AddApplicationPipelineBehaviors();

        return services;
    }
}