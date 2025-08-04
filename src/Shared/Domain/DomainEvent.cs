namespace Axon.Shared.Domain;

/// <summary>
/// Base class for domain events that captures common event metadata with event sourcing patterns
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <summary>
    /// Unique identifier for this domain event
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// UTC timestamp when this event occurred
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    /// Version of the event schema for evolution support
    /// </summary>
    public int EventVersion { get; protected init; } = 1;

    /// <summary>
    /// Correlation identifier for tracing related events
    /// </summary>
    public string? CorrelationId { get; protected init; }

    /// <summary>
    /// Causation identifier linking this event to its trigger
    /// </summary>
    public string? CausationId { get; protected init; }

    /// <summary>
    /// Optional metadata associated with this event
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; protected init; }

    protected DomainEvent(
        Guid? eventId = null, 
        DateTime? occurredAt = null,
        string? correlationId = null,
        string? causationId = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        EventId = eventId ?? Guid.NewGuid();
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        CorrelationId = correlationId;
        CausationId = causationId;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the event type name for serialization and routing
    /// </summary>
    public string EventType => GetType().Name;

    /// <summary>
    /// Gets a string representation suitable for logging
    /// </summary>
    public override string ToString() => 
        $"{EventType} [Id={EventId}, OccurredAt={OccurredAt:yyyy-MM-dd HH:mm:ss} UTC, Version={EventVersion}]";
}