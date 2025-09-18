using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Identity credential linking a principal to an external identity provider.
/// </summary>
public sealed class IdentityCredential : AuditableDeletableEntity<IdentityCredentialId>
{
    public AxonUserId PrincipalId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Issuer { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public DateTime LastSeenAt { get; private set; }

    // EF Core constructor
    private IdentityCredential() { }

    private IdentityCredential(IdentityCredentialId id, AxonUserId principalId, string provider, string issuer, string subject, DateTime timestamp) : base(id)
    {
        PrincipalId = principalId;
        Provider = provider;
        Issuer = issuer;
        Subject = subject;
        LastSeenAt = timestamp;
    }

    public static IdentityCredential Create(AxonUserId principalId, string provider, string issuer, string subject, DateTime? timestamp = null)
    {
        var effectiveTimestamp = timestamp ?? DateTime.UtcNow;
        return new IdentityCredential(IdentityCredentialId.New(), principalId, provider, issuer, subject, effectiveTimestamp);
    }

    public void UpdateLastSeen(DateTime timestamp)
    {
        if (timestamp > LastSeenAt)
            LastSeenAt = timestamp;
    }
}