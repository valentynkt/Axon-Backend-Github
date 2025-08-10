using BuildingBlocks.Application.Events.Collecting;
using BuildingBlocks.Application.Events.Notifications;
using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Application.Events.Serialization;
using BuildingBlocks.Application.Events.Consumption;
using BuildingBlocks.Application.Events.Consumption.Inbox;
using BuildingBlocks.Application.Events.Consumption.Errors;
using BuildingBlocks.Application.Events.Consumption.Retry;
using BuildingBlocks.Application.Events.Consumption.DeadLetter;
using BuildingBlocks.Application.Outbox.Retry;
using BuildingBlocks.Application.Outbox.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

public static class ApplicationEventingRegistration
{
    /// <summary>
    /// Registers in-process domain event collection and post-commit publishing (Lane A).
    /// </summary>
    public static IServiceCollection AddInProcessDomainEventNotifications(this IServiceCollection services)
    {
        services.AddSingleton<IDomainEventCollector, EfDomainEventCollector>();
        services.AddScoped<IPostCommitDomainEventPublisher, MediatorPostCommitDomainEventPublisher>();
        return services;
    }

    /// <summary>
    /// Registers integration event pipeline for cross-boundary publishing (Lane B).
    /// Includes enveloping, publisher port, and pure Application dispatcher.
    /// </summary>
    public static IServiceCollection AddIntegrationEventPipeline(this IServiceCollection services)
    {
        // Enveloping infrastructure
        services.AddSingleton<IEventTypeNameResolver, DefaultEventTypeNameResolver>();
        services.AddSingleton<IEnvelopeHeaderPolicy, DefaultEnvelopeHeaderPolicy>();
        services.AddSingleton<IIntegrationEventEnvelopeFactory, DefaultIntegrationEventEnvelopeFactory>();

        // Context accessor (AsyncLocal)
        services.AddSingleton<IEnvelopeContextAccessor, AsyncLocalEnvelopeContextAccessor>();

        // Publisher port (NoOp in Application; real implementation comes from Infrastructure in Phase 2)
        services.AddSingleton<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();

        // Serializer
        services.AddSingleton<IEventSerializer, SystemTextJsonEventSerializer>();

        // Pure Application dispatcher (replaces legacy EventDispatcher)
        services.AddScoped<IEventDispatcher, IntegrationEventDispatcher>();

        // Outbox retry and monitoring policies
        services.AddSingleton<IOutboxBackoffPolicy, DefaultOutboxBackoffPolicy>();
        services.AddSingleton<IOutboxMetrics, NoOpOutboxMetrics>();

        return services;
    }

    /// <summary>
    /// Registers inbound integration event pipeline for consuming external events (Story 9 + 10).
    /// Includes inbox pattern for idempotency, handler registry, dispatcher, retry policies, and dead-letter handling.
    /// </summary>
    public static IServiceCollection AddInboundIntegrationEventPipeline(this IServiceCollection services)
    {
        return services.AddInboundIntegrationEventPipeline(_ => { });
    }

    /// <summary>
    /// Registers inbound integration event pipeline with configuration options.
    /// </summary>
    public static IServiceCollection AddInboundIntegrationEventPipeline(
        this IServiceCollection services,
        Action<InboxOptions> configureOptions)
    {
        // Configure inbox options
        services.Configure(configureOptions);

        // Inbox store for idempotency (NoOp in Application; real implementation comes from Infrastructure)
        services.AddSingleton<IInboxStore, NoOpInboxStore>();

        // Handler registry for resolving event handlers from DI
        services.AddScoped<IIntegrationEventHandlerRegistry, IntegrationEventHandlerRegistry>();

        // Error classification for retry/dead-letter decisions (Story 10)
        services.AddSingleton<IInboundErrorClassifier, DefaultInboundErrorClassifier>();

        // Retry policy for exponential backoff and jitter (Story 10)
        services.AddSingleton<IInboxRetryPolicy, DefaultInboxRetryPolicy>();

        // Dead-letter store for permanently failed messages (Story 10)
        // NoOp in Application; real implementation comes from Infrastructure
        services.AddSingleton<IInboxDeadLetterStore, NoOpInboxDeadLetterStore>();

        // Inbound dispatcher for processing integration event envelopes
        services.AddScoped<IInboundIntegrationEventDispatcher, InboundIntegrationEventDispatcher>();

        return services;
    }

    /// <summary>
    /// Registers inbound integration event pipeline with configuration binding.
    /// </summary>
    public static IServiceCollection AddInboundIntegrationEventPipeline(
        this IServiceCollection services,
        IConfiguration configuration,
        string configurationSectionName = "Inbox")
    {
        // Bind configuration to options
        services.Configure<InboxOptions>(configuration.GetSection(configurationSectionName));

        // Inbox store for idempotency (NoOp in Application; real implementation comes from Infrastructure)
        services.AddSingleton<IInboxStore, NoOpInboxStore>();

        // Handler registry for resolving event handlers from DI
        services.AddScoped<IIntegrationEventHandlerRegistry, IntegrationEventHandlerRegistry>();

        // Error classification for retry/dead-letter decisions (Story 10)
        services.AddSingleton<IInboundErrorClassifier, DefaultInboundErrorClassifier>();

        // Retry policy for exponential backoff and jitter (Story 10)
        services.AddSingleton<IInboxRetryPolicy, DefaultInboxRetryPolicy>();

        // Dead-letter store for permanently failed messages (Story 10)
        // NoOp in Application; real implementation comes from Infrastructure
        services.AddSingleton<IInboxDeadLetterStore, NoOpInboxDeadLetterStore>();

        // Inbound dispatcher for processing integration event envelopes
        services.AddScoped<IInboundIntegrationEventDispatcher, InboundIntegrationEventDispatcher>();

        return services;
    }

    /// <summary>
    /// Registers complete eventing pipeline including both inbound and outbound lanes.
    /// </summary>
    public static IServiceCollection AddApplicationEventing(this IServiceCollection services)
    {
        services.AddInProcessDomainEventNotifications();
        services.AddIntegrationEventPipeline();
        services.AddInboundIntegrationEventPipeline();
        return services;
    }
}