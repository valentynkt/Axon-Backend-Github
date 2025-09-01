using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Identity credential representing proof of identity from an external provider.
/// Child entity of AxonPrincipal with unique constraint on (ProviderType, Issuer, Subject).
/// </summary>
public sealed class IdentityCredential : AuditableDeletableEntity<IdentityCredentialId>
{
    public AxonId AxonId { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public string Issuer { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string? EnvironmentId { get; private set; }
    public DateTimeOffset VerifiedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    
    /// <summary>
    /// Structured metadata for additional credential information.
    /// </summary>
    public CredentialMetadata Metadata { get; private set; }

    // EF Core parameterless constructor
    private IdentityCredential() : base() 
    {
        Metadata = CredentialMetadata.Empty;
    }

    private IdentityCredential(
        IdentityCredentialId id,
        AxonId axonId,
        ProviderType providerType,
        string issuer,
        string subject,
        string? environmentId,
        DateTimeOffset verifiedAt,
        CredentialMetadata metadata) : base(id)
    {
        AxonId = axonId;
        ProviderType = providerType;
        Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        EnvironmentId = environmentId;
        VerifiedAt = verifiedAt;
        LastSeenAt = verifiedAt;
        Metadata = metadata;
    }

    internal static Result<IdentityCredential, Error> Create(
        AxonId axonId,
        ProviderType providerType,
        string issuer,
        string subject,
        string? environmentId,
        DateTimeOffset verifiedAt,
        Dictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            return Result.Failure<IdentityCredential, Error>(
                Error.Validation("Issuer is required.", "IDENTITY.CREDENTIAL.ISSUER.REQUIRED"));

        if (string.IsNullOrWhiteSpace(subject))
            return Result.Failure<IdentityCredential, Error>(
                Error.Validation("Subject is required.", "IDENTITY.CREDENTIAL.SUBJECT.REQUIRED"));

        if (issuer.Length > 200)
            return Result.Failure<IdentityCredential, Error>(
                Error.Validation("Issuer cannot exceed 200 characters.", "IDENTITY.CREDENTIAL.ISSUER.TOO_LONG"));

        if (subject.Length > 200)
            return Result.Failure<IdentityCredential, Error>(
                Error.Validation("Subject cannot exceed 200 characters.", "IDENTITY.CREDENTIAL.SUBJECT.TOO_LONG"));

        if (environmentId?.Length > 50)
            return Result.Failure<IdentityCredential, Error>(
                Error.Validation("Environment ID cannot exceed 50 characters.", "IDENTITY.CREDENTIAL.ENVIRONMENT_ID.TOO_LONG"));

        var metadataResult = CredentialMetadata.Create(metadata);
        if (metadataResult.IsFailure)
            return Result.Failure<IdentityCredential, Error>(metadataResult.Error);

        var id = new IdentityCredentialId(Guid.CreateVersion7());
        var credential = new IdentityCredential(
            id, axonId, providerType, issuer, subject, environmentId, verifiedAt, metadataResult.Value);

        return Result.Success<IdentityCredential, Error>(credential);
    }

    internal void UpdateLastSeen(DateTimeOffset lastSeenAt)
    {
        if (lastSeenAt > LastSeenAt)
        {
            LastSeenAt = lastSeenAt;
        }
    }

    internal void UpdateVerifiedAt(DateTimeOffset verifiedAt)
    {
        if (verifiedAt > VerifiedAt)
        {
            VerifiedAt = verifiedAt;
            UpdateLastSeen(verifiedAt);
        }
    }

    internal Result<Unit, Error> UpdateMetadata(Dictionary<string, object> newMetadata)
    {
        var metadataResult = CredentialMetadata.Create(newMetadata);
        if (metadataResult.IsFailure)
            return Result.Failure<Unit, Error>(metadataResult.Error);

        Metadata = metadataResult.Value;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> AddMetadata(string key, object value)
    {
        var metadataResult = Metadata.WithMetadata(key, value);
        if (metadataResult.IsFailure)
            return Result.Failure<Unit, Error>(metadataResult.Error);

        Metadata = metadataResult.Value;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Creates a unique key for this credential based on provider, issuer, and subject.
    /// Used for uniqueness constraints and lookups.
    /// </summary>
    public string GetUniqueKey() => $"{ProviderType.Value}:{Issuer}:{Subject}";

    /// <summary>
    /// Checks if this credential matches the given provider, issuer, and subject.
    /// </summary>
    public bool Matches(ProviderType providerType, string issuer, string subject)
    {
        return ProviderType.Value == providerType.Value &&
               string.Equals(Issuer, issuer, StringComparison.Ordinal) &&
               string.Equals(Subject, subject, StringComparison.Ordinal);
    }

    public bool BelongsTo(AxonId principalId) => AxonId == principalId;
}