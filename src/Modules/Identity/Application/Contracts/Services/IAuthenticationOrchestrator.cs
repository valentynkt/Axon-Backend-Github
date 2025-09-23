namespace Axon.Modules.Identity.Application.Contracts.Services;

using System.Security.Claims;
using Providers;
using Domain.Entities;
using CSharpFunctionalExtensions;

/// <summary>
/// Orchestrates authentication flows across different providers.
/// This is the main entry point for all authentication operations.
/// </summary>
public interface IAuthenticationOrchestrator
{
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

    /// <summary>
    /// Get current authenticated user from claims principal
    /// </summary>
    Task<Result<AxonUserAuth, Error>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh an existing authentication token
    /// </summary>
    Task<Result<AuthenticationResponse, Error>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate a user's current session (logout)
    /// </summary>
    Task<UnitResult<Error>> InvalidateSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}