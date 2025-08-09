namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Event + metadata wrapper. Useful when crossing module boundaries
/// or handing to the application layer without committing to a transport.
/// </summary>
public sealed record DomainEventEnvelope
{
    public IDomainEvent Event { get; }
    public DomainEventMetadata Metadata { get; }

    public DomainEventEnvelope(IDomainEvent @event, DomainEventMetadata? metadata = null)
    {
        Event = @event ?? throw new ArgumentNullException(nameof(@event));
        Metadata = metadata ?? DomainEventMetadata.Empty;
    }

    public DomainEventEnvelope WithMetadata(DomainEventMetadata metadata)
        => new(Event, metadata);

    public DomainEventEnvelope WithCorrelation(string? correlationId, string? causationId = null)
        => new(Event, Metadata with { CorrelationId = correlationId, CausationId = causationId });

    public override string ToString()
        => $"{Event.Name}#{Event.EventId} @ {Event.OccurredAt:O} v{Event.Version}";
}