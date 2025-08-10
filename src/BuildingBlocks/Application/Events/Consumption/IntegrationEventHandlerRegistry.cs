using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Application.Events.Consumption;

/// <summary>
/// DI-backed registry for resolving integration event handlers.
/// Resolves handlers from the service container based on event types.
/// </summary>
public sealed class IntegrationEventHandlerRegistry : IIntegrationEventHandlerRegistry
{
    private readonly IServiceProvider _serviceProvider;

    public IntegrationEventHandlerRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IEnumerable<IIntegrationEventHandler<IIntegrationEvent>> GetHandlers(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
        {
            return Enumerable.Empty<IIntegrationEventHandler<IIntegrationEvent>>();
        }

        // Build the generic handler interface type
        var handlerInterfaceType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

        // Get all registered handlers for this specific event type
        var handlers = _serviceProvider.GetServices(handlerInterfaceType);

        // Convert to the base interface for uniform handling
        return handlers.Cast<IIntegrationEventHandler<IIntegrationEvent>>();
    }
}