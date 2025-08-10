namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Resolves stable type names for integration events used in envelopes.
/// Supports customization via attributes or conventions.
/// </summary>
public interface IEventTypeNameResolver
{
    /// <summary>
    /// Resolve a stable type name for the given integration event type.
    /// </summary>
    /// <param name="integrationEventType">Integration event type</param>
    /// <returns>Stable type name for transport</returns>
    string Resolve(Type integrationEventType);
}