namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when an identity credential is revoked or soft-deleted.
/// </summary>
public sealed record IdentityCredentialRevokedEvent(
    AxonId AxonId,
    IdentityCredentialId CredentialId,
    string ProviderType,
    string Issuer,
    string Subject,
    string? EnvironmentId,
    DateTimeOffset RevokedAt,
    string? Reason = null
) : DomainEvent;