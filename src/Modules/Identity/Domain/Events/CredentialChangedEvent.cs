using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all credential-related changes.
/// Replaces: IdentityCredentialLinked, IdentityCredentialRevoked, 
/// CredentialLastSeenUpdated, and other credential events.
/// </summary>
public sealed record CredentialChangedEvent : DomainEvent
{
    public AxonId PrincipalId { get; init; }
    public IdentityCredentialId CredentialId { get; init; }
    public string ChangeType { get; init; } // "added", "removed", "last_seen_updated"
    public string? Provider { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }

    public CredentialChangedEvent(
        AxonId principalId,
        IdentityCredentialId credentialId,
        string changeType,
        string? provider = null,
        Dictionary<string, string>? metadata = null,
        DateTime? eventOccurredAt = null) : base(eventOccurredAt ?? DateTime.UtcNow)
    {
        PrincipalId = principalId;
        CredentialId = credentialId;
        ChangeType = changeType;
        Provider = provider;
        Metadata = metadata;
    }
}