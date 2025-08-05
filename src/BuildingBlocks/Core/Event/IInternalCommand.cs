namespace BuildingBlocks.Core.Event;

/// <summary>
/// Interface for internal commands that are processed within the same bounded context.
/// Internal commands represent deferred operations or compensating actions.
/// These commands are typically used for eventual consistency scenarios.
/// </summary>
public interface IInternalCommand : IEvent
{
}