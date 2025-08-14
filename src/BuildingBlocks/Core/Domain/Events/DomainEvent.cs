namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Base record for domain events. Immutable, infra-agnostic.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; }
    public DateTime OccurredAt { get; init; }
    public int Version { get; init; }
    public string Name { get; init; }

    protected DomainEvent(int version = 1, string? name = null)
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        Version = version;
        Name = name ?? GetDefaultName(GetType());
    }

    protected DomainEvent(DateTime occurredAt, int version = 1, string? name = null)
    {
        EventId = Guid.NewGuid();
        OccurredAt = occurredAt;
        Version = version;
        Name = name ?? GetDefaultName(GetType());
    }

    protected DomainEvent(Guid eventId, DateTime occurredAt, int version = 1, string? name = null)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        Version = version;
        Name = name ?? GetDefaultName(GetType());
    }

    private static string GetDefaultName(Type t)
        => t.FullName ?? t.Name;
}