using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Provides standardized error messages for Story 5.5 requirements
/// </summary>
public static class StandardizedErrorMessageService
{
    /// <summary>
    /// Creates error for wallet already verified on a specific network
    /// </summary>
    public static Error WalletAlreadyVerified(NetworkEnvironment networkEnvironment, string chainId)
    {
        return Error.Conflict(
            $"Wallet already verified on {networkEnvironment.Value}/{chainId}",
            "WALLET.ALREADY_VERIFIED");
    }

    /// <summary>
    /// Creates error for unknown Dynamic environment ID
    /// </summary>
    public static Error UnknownDynamicEnvironmentId(string environmentId)
    {
        return Error.Validation(
            $"Unknown Dynamic environmentId: {environmentId}",
            "DYNAMIC.UNKNOWN_ENVIRONMENT");
    }

    /// <summary>
    /// Creates error for wallet proof TTL exceeding maximum
    /// </summary>
    public static Error WalletProofTtlExceeded(int ttlSeconds, int maxTtlSeconds)
    {
        return Error.Validation(
            $"Wallet proof TTL exceeds {maxTtlSeconds / 60}-minute maximum: {ttlSeconds}s",
            "WALLET_PROOF.TTL_EXCEEDED");
    }

    /// <summary>
    /// Creates error for replay detection
    /// </summary>
    public static Error ReplayDetected(string networkEnvironment)
    {
        return Error.Conflict(
            $"Replay detected for signature on {networkEnvironment}",
            "REPLAY.DETECTED");
    }

    /// <summary>
    /// Creates error for invalid audience
    /// </summary>
    public static Error InvalidAudience(string audience)
    {
        return Error.Unauthorized(
            $"Invalid audience '{audience}' for this API key",
            "AUDIENCE.INVALID");
    }

    /// <summary>
    /// Creates error for missing key identifier (kid)
    /// </summary>
    public static Error MissingKeyIdentifier()
    {
        return Error.Unauthorized(
            "Token missing key identifier (kid) for rotation tracking",
            "JWT.MISSING_KID");
    }

    /// <summary>
    /// Creates error for invalid or expired key identifier
    /// </summary>
    public static Error InvalidKeyIdentifier(string kid)
    {
        return Error.Unauthorized(
            $"Unknown or expired key identifier: {kid}",
            "JWT.INVALID_KID");
    }

    /// <summary>
    /// Creates error for KMS key retrieval failure
    /// </summary>
    public static Error KeyRetrievalFailed(string details)
    {
        return Error.Internal(
            $"Failed to retrieve signing key: {details}",
            "KMS.KEY_RETRIEVAL_FAILED");
    }

    /// <summary>
    /// Creates error for JWKS fetch failure
    /// </summary>
    public static Error JwksFetchFailed(string details)
    {
        return Error.External(
            $"Failed to fetch JWKS keys: {details}",
            "JWKS.FETCH_FAILED");
    }

    /// <summary>
    /// Creates error for wallet ownership conflict
    /// </summary>
    public static Error WalletOwnershipConflict(string address, string existingOwner)
    {
        return Error.Conflict(
            $"Wallet {address} is already owned by another principal: {existingOwner}",
            "WALLET.OWNERSHIP_CONFLICT");
    }

    /// <summary>
    /// Creates error for canonical message validation failure
    /// </summary>
    public static Error CanonicalMessageInvalid(string reason)
    {
        return Error.Validation(
            $"Canonical message validation failed: {reason}",
            "CANONICAL_MESSAGE.INVALID");
    }

    /// <summary>
    /// Creates error for clock skew violation
    /// </summary>
    public static Error ClockSkewViolation(string details)
    {
        return Error.Validation(
            $"Clock skew tolerance exceeded: {details}",
            "TIMING.CLOCK_SKEW");
    }
}