using MassTransit;

namespace BuildingBlocks.Core.Event;

/// <summary>
/// Interface for integration events that cross bounded context boundaries.
/// Integration events enable communication between different bounded contexts or external systems.
/// These events are published asynchronously and handled outside the originating transaction.
/// </summary>
[ExcludeFromTopology]
public interface IIntegrationEvent : IEvent
{
}