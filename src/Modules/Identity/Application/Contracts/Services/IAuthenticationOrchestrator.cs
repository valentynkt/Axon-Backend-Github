namespace Axon.Modules.Identity.Application.Contracts.Services;

using System.Security.Claims;
using Common;
using Providers;
using Domain.Entities;
using Domain.ValueObjects;
using CSharpFunctionalExtensions;

/// <summary>
/// Orchestrates authentication flows across different providers.
/// This is the main entry point for all authentication operations.
/// </summary>
public interface IAuthenticationOrchestrator
{
    // ========== Challenge & Validation Methods ==========

    /// <summary>
    /// Generates an authentication challenge for wallet signing
    /// </summary>
    Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the HMAC signature of a message
    /// </summary>
    Result<bool, Error> ValidateMac(
        string message,
        string mac,
        string keyVersion);

    /// <summary>
    /// Validates a challenge message structure and TTL
    /// </summary>
    Result<bool, Error> ValidateChallenge(
        string message,
        string expectedChainId,
        string expectedAddress,
        string expectedAudience);

    /// <summary>
    /// Checks and marks a nonce as used for replay protection
    /// </summary>
    Task<UnitResult<Error>> CheckAndMarkNonceUsedAsync(
        string signedMessage,
        string mkv,
        CancellationToken cancellationToken = default);

    // ========== Authentication Methods ==========

    /// <summary>
    /// Process wallet-based authentication with signature verification
    /// </summary>
    Task<Result<AuthenticationResponse, Error>> AuthenticateWithWalletAsync(
        WalletAuthenticationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process Dynamic.xyz JWT token exchange
    /// </summary>
    Task<Result<AuthenticationResponse, Error>> ExchangeDynamicTokenAsync(
        string dynamicToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process pre-validated claims from JWT middleware
    /// </summary>
    Task<Result<AuthenticationResponse, Error>> ProcessValidatedClaimsAsync(
        ClaimsPrincipal principal,
        string providerType,
        CancellationToken cancellationToken = default);

    // ========== Token & Session Methods ==========

    /// <summary>
    /// Get current authenticated user from claims principal
    /// </summary>
    Task<Result<AxonUserAuth, Error>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh an existing authentication token
    /// </summary>
    Task<Result<RefreshTokenResponse, Error>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate a user's current session (logout)
    /// </summary>
    Task<UnitResult<Error>> InvalidateSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}