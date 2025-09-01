namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a principal is soft deleted.
/// </summary>
public sealed record PrincipalSoftDeletedEvent(
    AxonId AxonId,
    DateTimeOffset DeletedAt
) : DomainEvent;