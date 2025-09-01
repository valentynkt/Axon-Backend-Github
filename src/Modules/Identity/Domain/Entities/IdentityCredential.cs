using System.Text.Json;
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
    /// Additional metadata stored as JSON (e.g., session_public_key, email_hash).
    /// </summary>
    public string MetadataJson { get; private set; } = "{}";

    private Dictionary<string, object>? _metadataCache;
    private bool _metadataCacheInitialized;

    public IReadOnlyDictionary<string, object> Metadata
    {
        get
        {
            if (!_metadataCacheInitialized)
            {
                try
                {
                    _metadataCache = JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson) ?? new();
                    _metadataCacheInitialized = true;
                }
                catch (JsonException)
                {
                    // If deserialization fails, use empty dictionary and reset JSON
                    _metadataCache = new Dictionary<string, object>();
                    MetadataJson = "{}";
                    _metadataCacheInitialized = true;
                }
            }
            return _metadataCache ?? new Dictionary<string, object>();
        }
    }

    // EF Core parameterless constructor
    private IdentityCredential() : base() { }

    private IdentityCredential(
        IdentityCredentialId id,
        AxonId axonId,
        ProviderType providerType,
        string issuer,
        string subject,
        string? environmentId,
        DateTimeOffset verifiedAt,
        Dictionary<string, object>? metadata = null) : base(id)
    {
        AxonId = axonId;
        ProviderType = providerType;
        Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        EnvironmentId = environmentId;
        VerifiedAt = verifiedAt;
        LastSeenAt = verifiedAt;

        if (metadata is not null)
        {
            try
            {
                _metadataCache = metadata;
                MetadataJson = JsonSerializer.Serialize(metadata);
                _metadataCacheInitialized = true;
            }
            catch (JsonException)
            {
                // If serialization fails, use empty metadata
                _metadataCache = new Dictionary<string, object>();
                MetadataJson = "{}";
                _metadataCacheInitialized = true;
            }
        }
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

        if (metadata is not null && metadata.Count > 20)
            return Result.Failure<IdentityCredential, Error>(
                Error.Validation("Metadata cannot contain more than 20 entries.", "IDENTITY.CREDENTIAL.METADATA.TOO_MANY_ENTRIES"));

        var id = new IdentityCredentialId(Guid.CreateVersion7());
        var credential = new IdentityCredential(
            id, axonId, providerType, issuer, subject, environmentId, verifiedAt, metadata);

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
        try
        {
            _metadataCache = newMetadata;
            MetadataJson = JsonSerializer.Serialize(newMetadata);
            _metadataCacheInitialized = true;
            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (JsonException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.Internal($"Failed to serialize credential metadata: {ex.Message}", "IDENTITY.CREDENTIAL.METADATA.SERIALIZATION.FAILED"));
        }
    }

    internal Result<Unit, Error> AddMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure<Unit, Error>(
                Error.Validation("Metadata key cannot be empty.", "IDENTITY.CREDENTIAL.METADATA.KEY.EMPTY"));

        var metadata = Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        metadata[key] = value;
        return UpdateMetadata(metadata);
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