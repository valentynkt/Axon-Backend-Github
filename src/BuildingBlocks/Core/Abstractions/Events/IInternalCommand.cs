using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Interface for internal commands that are processed within the same bounded context.
/// Internal commands represent deferred operations or compensating actions.
/// These commands are typically used for eventual consistency scenarios.
/// </summary>
public interface IInternalCommand : IEvent
{
}