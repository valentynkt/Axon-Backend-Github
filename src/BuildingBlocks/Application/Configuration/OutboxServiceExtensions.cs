using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.Outbox;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// KISS/YAGNI Outbox registration for Application layer.
/// - Registers a thin <see cref="IOutboxService"/> façade that publishes domain events
///   (mapping -> dispatcher). 
/// - Reliability (persist/retry/transport) is handled by Infrastructure (e.g., MassTransit EF Outbox).
/// </summary>
public static class OutboxServiceExtensions
{
    /// <summary>
    /// Register the minimal outbox façade.
    /// Infrastructure should enable MassTransit EF Outbox so Publish is captured transactionally.
    /// </summary>
    public static IServiceCollection AddOutboxFacade(this IServiceCollection services)
    {
        services.TryAddScoped<IOutboxService, OutboxService>();
        return services;
    }

    /// <summary>
    /// Register the minimal outbox façade plus a transactional command pipeline behavior.
    /// Use this if you already have a CommandTransactionBehavior and want it wired automatically.
    /// </summary>
    public static IServiceCollection AddOutboxFacadeWithTransactions(this IServiceCollection services)
    {
        services.AddOutboxFacade();
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(CommandTransactionBehavior<,>));
        return services;
    }
}