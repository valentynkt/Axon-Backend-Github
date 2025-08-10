using BuildingBlocks.Application.Events.Collecting;
using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Application.Events.Mapping;
using BuildingBlocks.Application.Events.Notifications;
using BuildingBlocks.Application.Events.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// Registers the outbound integration event pipeline (Lane B) with NO custom mappers.
    /// Useful default: dispatcher + publisher, mapper returns null (no mappings).
    /// </summary>
    public static IServiceCollection AddIntegrationEventPipeline(this IServiceCollection services)
    {
        // Publisher port (Application default – Infra should override)
        services.TryAddScoped<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();

        // Mapper: empty composite (safe default, returns null for all)
        services.AddScoped<IEventMapper>(_ => new CompositeEventMapper(Array.Empty<IEventMapper>()));

        // Dispatcher
        services.AddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>();

        return services;
    }

    /// <summary>
    /// Registers the outbound integration event pipeline (Lane B) with concrete mapper types.
    /// Pass one or more types that implement IEventMapper.
    /// Example: services.AddIntegrationEventPipeline(typeof(OrderPlacedMapper), typeof(UserCreatedMapper));
    /// </summary>
    public static IServiceCollection AddIntegrationEventPipeline(this IServiceCollection services, params Type[] mapperTypes)
    {
        // Validate and register leaf mappers so we can compose them without DI self-recursion.
        mapperTypes ??= Array.Empty<Type>();
        foreach (var t in mapperTypes)
        {
            if (!typeof(IEventMapper).IsAssignableFrom(t))
                throw new ArgumentException($"Type '{t.FullName}' must implement {nameof(IEventMapper)}.", nameof(mapperTypes));

            services.AddScoped(t); // register concrete mapper type
        }

        services.TryAddScoped<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();

        // Compose a CompositeEventMapper from the provided leaf types only.
        services.AddScoped<IEventMapper>(sp =>
        {
            var leaves = mapperTypes
                .Select(t => (IEventMapper)sp.GetRequiredService(t))
                .ToArray();

            return new CompositeEventMapper(leaves);
        });

        services.AddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
        return services;
    }

    /// <summary>
    /// Convenience method: registers both Lane A and Lane B with the safe defaults
    /// (no custom mappers – you can call the overload above later to add them).
    /// </summary>
    public static IServiceCollection AddApplicationEventing(this IServiceCollection services)
    {
        services.AddInProcessDomainEventNotifications();
        services.AddIntegrationEventPipeline();
        return services;
    }
}
