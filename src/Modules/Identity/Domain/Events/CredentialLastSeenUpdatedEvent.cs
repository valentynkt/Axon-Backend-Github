namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when an identity credential's last seen timestamp is updated.
/// Used for tracking credential usage and freshness.
/// </summary>
public sealed record CredentialLastSeenUpdatedEvent(
    AxonId AxonId,
    IdentityCredentialId CredentialId,
    string ProviderType,
    DateTimeOffset LastSeenAt,
    DateTimeOffset PreviousLastSeenAt
) : DomainEvent;