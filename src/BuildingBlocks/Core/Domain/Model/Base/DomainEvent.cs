using BuildingBlocks.Core.Domain.Core.Model.Abstractions;

namespace BuildingBlocks.Core.Domain.Core.Model.Base;

/// <summary>
/// Minimal base for domain events with OccurredOn timestamp.
/// Derived events add meaningful, immutable data about what happened.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;

    protected DomainEvent() { }

    protected DomainEvent(DateTimeOffset occurredOn)
    {
        OccurredOn = occurredOn;
    }
}