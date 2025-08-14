using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.Events.Collecting;
using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Notifications;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Application.Outbox;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Complete outbox and eventing registration for Application layer.
/// - Registers the complete eventing pipeline: collection → dispatching → publishing
/// - Reliability (persist/retry/transport) is handled by Infrastructure (e.g., MassTransit EF Outbox).
/// </summary>
public static class OutboxServiceExtensions
{
    /// <summary>
    /// Register the complete outbox and eventing pipeline.
    /// Infrastructure should override IIntegrationEventPublisher to enable MassTransit EF Outbox.
    /// </summary>
    public static IServiceCollection AddOutboxFacade(this IServiceCollection services)
    {
        // Core outbox service
        services.TryAddScoped<IOutboxService, OutboxService>();
        
        // Domain event collection
        services.TryAddSingleton<IDomainEventCollector, EfDomainEventCollector>();
        
        // Integration event dispatching
        services.TryAddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
        
        // Post-commit domain event publishing
        services.TryAddScoped<IPostCommitDomainEventPublisher, MediatorPostCommitDomainEventPublisher>();
        
        // Integration event publishing (default NoOp - Infrastructure should override)
        services.TryAddScoped<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();
        
        // Envelope context for correlation headers
        services.TryAddSingleton<IEnvelopeContextAccessor, AsyncLocalEnvelopeContextAccessor>();
        
        return services;
    }

    /// <summary>
    /// Register the complete outbox/eventing pipeline plus transactional command behavior.
    /// This is the recommended method for full eventing support.
    /// </summary>
    public static IServiceCollection AddOutboxFacadeWithTransactions(this IServiceCollection services)
    {
        services.AddOutboxFacade();
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(CommandTransactionBehavior<,>));
        return services;
    }
}