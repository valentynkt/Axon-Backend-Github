using Axon.Modules.Chat.Application.Abstractions.Caching;
using Axon.Modules.Chat.Application.Services.Idempotency;
using Microsoft.Extensions.DependencyInjection;
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
    public static IServiceCollection AddChatApplication(this IServiceCollection services)
    {
        // Register MediatR handlers from this assembly
        services.AddMediatR(cfg => 
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            
            // Register behaviors in order
            cfg.AddOpenBehavior(typeof(Behaviors.ExceptionMappingBehavior<,>));
            cfg.AddOpenBehavior(typeof(Behaviors.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Behaviors.IdempotencyBehavior<,>));
            cfg.AddOpenBehavior(typeof(Behaviors.TelemetryBehavior<,>));
        });

        // Register validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register services
        services.AddMemoryCache();
        services.AddSingleton<IIdempotencyCache, InMemoryIdempotencyCache>();

        return services;
    }
}