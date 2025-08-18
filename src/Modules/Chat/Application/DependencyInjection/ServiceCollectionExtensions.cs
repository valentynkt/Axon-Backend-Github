using Axon.Modules.Chat.Domain.Time;
using BuildingBlocks.Application.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Axon.Modules.Chat.Application.DependencyInjection;

/// <summary>
/// Extension methods for registering Chat Application layer services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Chat Application layer services
    /// </summary>
    public static IServiceCollection AddChatApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Register MediatR handlers from this assembly
        services.AddMediatR(cfg => 
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register BuildingBlocks pipeline behaviors
        services.AddPipelineBehaviors(configuration, environment);

        // Register Chat-specific services
        services.AddMemoryCache();
        // services.AddSingleton<IIdempotencyCache, InMemoryIdempotencyCache>();
        services.AddSingleton<IClock, SystemClock>();
        
        // IAiClient is infrastructure-provided (adapter), register there.

        return services;
    }
}