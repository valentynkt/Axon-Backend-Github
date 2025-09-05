using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Identity credential representing proof of identity from an external provider.
/// Child entity of AxonPrincipal with unique constraint on (ProviderType, Issuer, Subject).
/// Now uses strongly-typed CredentialContext instead of JSON metadata.
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
    /// Strongly-typed context information for this credential.
    /// Replaces the JSON-based CredentialMetadata.
    /// </summary>
    public CredentialContext Context { get; private set; } = null!;

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
        CredentialContext context) : base(id)
    {
        AxonId = axonId;
        ProviderType = providerType;
        Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        EnvironmentId = environmentId;
        VerifiedAt = verifiedAt;
        LastSeenAt = verifiedAt;
        Context = context;
    }

    /// <summary>
    /// Creates a new identity credential with typed context information.
    /// </summary>
    internal static Result<IdentityCredential, Error> Create(
        AxonId axonId,
        ProviderType providerType,
        string issuer,
        string subject,
        string? environmentId,
        DateTimeOffset verifiedAt,
        ProofType? verificationMethod = null,
        EmailHash? emailHash = null,
        string? sessionPublicKey = null,
        string? deviceId = null,
        string? userAgent = null,
        string? ipHash = null)
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

        var id = new IdentityCredentialId(Guid.CreateVersion7());

        // Create the credential context
        var contextResult = CredentialContext.Create(
            id,
            verificationMethod ?? ProofType.From("unknown"),
            emailHash,
            sessionPublicKey,
            deviceId,
            userAgent,
            ipHash);

        if (contextResult.IsFailure)
            return Result.Failure<IdentityCredential, Error>(contextResult.Error);

        var credential = new IdentityCredential(
            id, axonId, providerType, issuer, subject, environmentId, verifiedAt, contextResult.Value);

        return Result.Success<IdentityCredential, Error>(credential);
    }

    /// <summary>
    /// Updates the last seen timestamp.
    /// </summary>
    internal void UpdateLastSeen(DateTimeOffset lastSeenAt)
    {
        if (lastSeenAt > LastSeenAt)
        {
            LastSeenAt = lastSeenAt;
            MarkUpdated();
        }
    }

    /// <summary>
    /// Updates the verified timestamp.
    /// </summary>
    internal void UpdateVerifiedAt(DateTimeOffset verifiedAt)
    {
        if (verifiedAt > VerifiedAt)
        {
            VerifiedAt = verifiedAt;
            UpdateLastSeen(verifiedAt);
        }
    }

    /// <summary>
    /// Updates the credential context with new typed information.
    /// Replaces the old UpdateMetadata method.
    /// </summary>
    internal Result<Unit, Error> UpdateContext(
        ProofType? verificationMethod = null,
        EmailHash? emailHash = null,
        string? sessionPublicKey = null,
        string? deviceId = null,
        string? userAgent = null,
        string? ipHash = null)
    {
        var updateResult = Context.Update(
            verificationMethod,
            emailHash,
            sessionPublicKey,
            deviceId,
            userAgent,
            ipHash);

        if (updateResult.IsFailure)
            return Result.Failure<Unit, Error>(updateResult.Error);

        MarkUpdated();
        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Sets the verification method for this credential.
    /// </summary>
    internal void SetVerificationMethod(ProofType verificationMethod)
    {
        Context.SetVerificationMethod(verificationMethod);
        MarkUpdated();
    }

    /// <summary>
    /// Updates context information from a session interaction.
    /// </summary>
    internal Result<Unit, Error> TouchSession(
        string? deviceId = null,
        string? userAgent = null,
        string? ipHash = null,
        DateTimeOffset? lastSeenAt = null)
    {
        var updateResult = Context.Update(
            deviceId: deviceId,
            userAgent: userAgent,
            ipHash: ipHash);

        if (updateResult.IsFailure)
            return Result.Failure<Unit, Error>(updateResult.Error);

        if (lastSeenAt.HasValue)
        {
            UpdateLastSeen(lastSeenAt.Value);
        }

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

    /// <summary>
    /// Checks if this credential belongs to the specified principal.
    /// </summary>
    public bool BelongsTo(AxonId principalId) => AxonId == principalId;

    /// <summary>
    /// Gets the email hash from the context if available.
    /// </summary>
    public EmailHash? GetEmailHash() => Context.EmailHash;

    /// <summary>
    /// Gets the session public key from the context if available.
    /// </summary>
    public string? GetSessionPublicKey() => Context.SessionPublicKey;

    /// <summary>
    /// Gets the verification method from the context.
    /// </summary>
    public ProofType GetVerificationMethod() => Context.VerificationMethod;
}