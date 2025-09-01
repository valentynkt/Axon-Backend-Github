namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a soft-deleted principal is restored to active status.
/// </summary>
public sealed record PrincipalRestoredEvent(
    AxonId AxonId,
    DateTimeOffset RestoredAt,
    string? Reason = null
) : DomainEvent;