using System.Reflection;
using BuildingBlocks.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
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
    /// Registers all infrastructure services including messaging with EF Outbox.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext that will host outbox tables</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="configureMessaging">Optional messaging configuration</param>
    /// <param name="consumerAssemblies">Assemblies to scan for MassTransit consumers</param>
    public static IServiceCollection AddInfrastructure<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MassTransitOptions>? configureMessaging = null,
        params Assembly[] consumerAssemblies)
        where TDbContext : DbContext
    {
        // Register messaging infrastructure with EF Outbox
        services.AddInfrastructureMessaging<TDbContext>(configuration, configureMessaging, consumerAssemblies);

        // Future: Add other infrastructure services here
        // - Caching
        // - File storage
        // - Email services
        // - SMS services
        // - etc.

        return services;
    }
}