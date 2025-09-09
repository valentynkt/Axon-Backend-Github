namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all credential-related changes.
/// Replaces: IdentityCredentialLinked, IdentityCredentialRevoked, 
/// CredentialLastSeenUpdated, and other credential events.
/// </summary>
public sealed record CredentialChangedEvent(
    string AxonId,
    string CredentialId,
    string ChangeType, // "linked", "revoked", "last_seen_updated", "context_updated"
    string? ProviderType = null,
    string? Issuer = null,
    string? Subject = null,
    string? EnvironmentId = null,
    string? Reason = null,
    DateTime? EventOccurredAt = null
) : DomainEvent(EventOccurredAt ?? DateTime.UtcNow);