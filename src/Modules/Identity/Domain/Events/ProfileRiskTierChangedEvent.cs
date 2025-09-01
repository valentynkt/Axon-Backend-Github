namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a principal's risk tier is changed.
/// </summary>
public sealed record ProfileRiskTierChangedEvent(
    AxonId AxonId,
    string NewRiskTier,
    DateTimeOffset ChangedAt
) : DomainEvent;