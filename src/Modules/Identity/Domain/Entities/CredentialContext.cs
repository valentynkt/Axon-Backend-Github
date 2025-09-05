using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using CredentialContextEntity = BuildingBlocks.Core.Domain.Entities.Base.AuditableEntity<BuildingBlocks.Primitives.Ids.IdentityCredentialId>;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Credential context owned entity containing structured metadata for identity credentials.
/// Owned by IdentityCredential - can only be modified through the credential entity.
/// Replaces the JSON-based CredentialMetadata with strongly-typed properties.
/// </summary>
public sealed class CredentialContext : CredentialContextEntity
{
    /// <summary>
    /// Hash of the user's email address (SHA-256 hex)
    /// </summary>
    public EmailHash? EmailHash { get; private set; }
    
    /// <summary>
    /// Method used to verify this credential
    /// </summary>
    public ProofType VerificationMethod { get; private set; }
    
    /// <summary>
    /// Session public key for cryptographic operations
    /// </summary>
    public string? SessionPublicKey { get; private set; }
    
    /// <summary>
    /// Device identifier for session tracking
    /// </summary>
    public string? DeviceId { get; private set; }
    
    /// <summary>
    /// User agent string from the authentication session
    /// </summary>
    public string? UserAgent { get; private set; }
    
    /// <summary>
    /// Hash of the IP address for security tracking (SHA-256 hex)
    /// </summary>
    public string? IpHash { get; private set; }

    // EF Core parameterless constructor
    private CredentialContext() : base() 
    {
        VerificationMethod = ProofType.From("unknown");
    }

    private CredentialContext(
        IdentityCredentialId credentialId,
        ProofType verificationMethod,
        EmailHash? emailHash = null,
        string? sessionPublicKey = null,
        string? deviceId = null,
        string? userAgent = null,
        string? ipHash = null) : base(credentialId)
    {
        VerificationMethod = verificationMethod;
        EmailHash = emailHash;
        SessionPublicKey = sessionPublicKey?.Trim();
        DeviceId = deviceId?.Trim();
        UserAgent = userAgent?.Trim();
        IpHash = ipHash?.Trim();
    }

    /// <summary>
    /// Creates a new CredentialContext for the specified credential.
    /// </summary>
    internal static Result<CredentialContext, Error> Create(
        IdentityCredentialId credentialId,
        ProofType verificationMethod,
        EmailHash? emailHash = null,
        string? sessionPublicKey = null,
        string? deviceId = null,
        string? userAgent = null,
        string? ipHash = null)
    {
        // Validate session public key
        if (!string.IsNullOrWhiteSpace(sessionPublicKey))
        {
            var trimmed = sessionPublicKey.Trim();
            if (trimmed.Length > 1000)
            {
                return Result.Failure<CredentialContext, Error>(
                    Error.Validation("Session public key cannot exceed 1000 characters.", "CREDENTIAL.CONTEXT.SESSION_KEY.TOO_LONG"));
            }
            sessionPublicKey = trimmed;
        }

        // Validate device ID
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var trimmed = deviceId.Trim();
            if (trimmed.Length > 255)
            {
                return Result.Failure<CredentialContext, Error>(
                    Error.Validation("Device ID cannot exceed 255 characters.", "CREDENTIAL.CONTEXT.DEVICE_ID.TOO_LONG"));
            }
            deviceId = trimmed;
        }

        // Validate user agent
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            var trimmed = userAgent.Trim();
            if (trimmed.Length > 1000)
            {
                return Result.Failure<CredentialContext, Error>(
                    Error.Validation("User agent cannot exceed 1000 characters.", "CREDENTIAL.CONTEXT.USER_AGENT.TOO_LONG"));
            }
            userAgent = trimmed;
        }

        // Validate IP hash
        if (!string.IsNullOrWhiteSpace(ipHash))
        {
            var trimmed = ipHash.Trim();
            if (trimmed.Length != 64)
            {
                return Result.Failure<CredentialContext, Error>(
                    Error.Validation("IP hash must be a 64-character hexadecimal string.", "CREDENTIAL.CONTEXT.IP_HASH.INVALID"));
            }
            ipHash = trimmed;
        }

        var context = new CredentialContext(
            credentialId, verificationMethod, emailHash, sessionPublicKey, deviceId, userAgent, ipHash);
        
        return Result.Success<CredentialContext, Error>(context);
    }

    /// <summary>
    /// Updates the credential context information.
    /// </summary>
    internal Result<Unit, Error> Update(
        ProofType? verificationMethod = null,
        EmailHash? emailHash = null,
        string? sessionPublicKey = null,
        string? deviceId = null,
        string? userAgent = null,
        string? ipHash = null)
    {
        // Validate session public key
        if (!string.IsNullOrWhiteSpace(sessionPublicKey))
        {
            var trimmed = sessionPublicKey.Trim();
            if (trimmed.Length > 1000)
            {
                return Result.Failure<Unit, Error>(
                    Error.Validation("Session public key cannot exceed 1000 characters.", "CREDENTIAL.CONTEXT.SESSION_KEY.TOO_LONG"));
            }
            SessionPublicKey = trimmed;
        }

        // Validate device ID
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var trimmed = deviceId.Trim();
            if (trimmed.Length > 255)
            {
                return Result.Failure<Unit, Error>(
                    Error.Validation("Device ID cannot exceed 255 characters.", "CREDENTIAL.CONTEXT.DEVICE_ID.TOO_LONG"));
            }
            DeviceId = trimmed;
        }

        // Validate user agent
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            var trimmed = userAgent.Trim();
            if (trimmed.Length > 1000)
            {
                return Result.Failure<Unit, Error>(
                    Error.Validation("User agent cannot exceed 1000 characters.", "CREDENTIAL.CONTEXT.USER_AGENT.TOO_LONG"));
            }
            UserAgent = trimmed;
        }

        // Validate IP hash
        if (!string.IsNullOrWhiteSpace(ipHash))
        {
            var trimmed = ipHash.Trim();
            if (trimmed.Length != 64)
            {
                return Result.Failure<Unit, Error>(
                    Error.Validation("IP hash must be a 64-character hexadecimal string.", "CREDENTIAL.CONTEXT.IP_HASH.INVALID"));
            }
            IpHash = trimmed;
        }

        if (verificationMethod.HasValue)
            VerificationMethod = verificationMethod.Value;
        
        if (emailHash.HasValue)
            EmailHash = emailHash.Value;

        MarkUpdated();
        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Sets the verification method for this credential.
    /// </summary>
    internal void SetVerificationMethod(ProofType verificationMethod)
    {
        VerificationMethod = verificationMethod;
        MarkUpdated();
    }
}