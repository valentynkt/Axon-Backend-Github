namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base implementation for domain events following Epic 2 specifications.
/// Provides common properties and follows immutable design principles.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        Version = 1;
    }
    
    protected DomainEvent(Guid eventId, DateTime occurredAt, int version = 1)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        Version = version;
    }
    
    public Guid EventId { get; }
    public DateTime OccurredAt { get; }
    public int Version { get; }
}