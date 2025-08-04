namespace Axon.Shared.Domain;

/// <summary>
/// Interface for domain event store to support event sourcing patterns
/// </summary>
public interface IDomainEventStore
{
    /// <summary>
    /// Saves domain events for an aggregate
    /// </summary>
    /// <param name="aggregateId">The aggregate identifier</param>
    /// <param name="events">The domain events to save</param>
    /// <param name="expectedVersion">The expected version of the aggregate for optimistic concurrency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveEventsAsync<TId>(
        TId aggregateId, 
        IEnumerable<IDomainEvent> events, 
        int expectedVersion, 
        CancellationToken cancellationToken = default)
        where TId : notnull;

    /// <summary>
    /// Gets all domain events for an aggregate
    /// </summary>
    /// <param name="aggregateId">The aggregate identifier</param>
    /// <param name="fromVersion">Start from this version (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The domain events in order</returns>
    Task<IEnumerable<IDomainEvent>> GetEventsAsync<TId>(
        TId aggregateId, 
        int fromVersion = 0, 
        CancellationToken cancellationToken = default)
        where TId : notnull;

    /// <summary>
    /// Gets the current version of an aggregate
    /// </summary>
    /// <param name="aggregateId">The aggregate identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current version or -1 if not found</returns>
    Task<int> GetVersionAsync<TId>(
        TId aggregateId, 
        CancellationToken cancellationToken = default)
        where TId : notnull;
}