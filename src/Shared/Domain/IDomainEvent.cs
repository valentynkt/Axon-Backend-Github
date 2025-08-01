namespace Axon.Shared.Domain;

/// <summary>
/// Marker interface for domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the date and time when the domain event occurred
    /// </summary>
    DateTime OccurredOn { get; }

    /// <summary>
    /// Gets the unique identifier of the domain event
    /// </summary>
    Guid EventId { get; }
}