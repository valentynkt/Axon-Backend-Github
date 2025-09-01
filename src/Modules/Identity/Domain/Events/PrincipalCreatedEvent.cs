namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a new principal is created.
/// </summary>
public sealed record PrincipalCreatedEvent(
    AxonId AxonId,
    string PrincipalType,
    string? PrimaryEmailHash,
    DateTimeOffset CreatedAt
) : DomainEvent;