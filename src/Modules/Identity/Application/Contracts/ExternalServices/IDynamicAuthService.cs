using System.Security.Claims;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.ExternalServices;

/// <summary>
/// Service interface for Dynamic.xyz JWT authentication and validation operations
/// </summary>
public interface IDynamicAuthService
{
    /// <summary>
    /// Validates a JWT token using Dynamic.xyz JWKS endpoint
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with validated user data if successful, error if invalid</returns>
    Task<Result<DynamicUserData, Error>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw ClaimsPrincipal from a validated JWT token
    /// This preserves all original JWT claims exactly as issued by Dynamic.xyz
    /// </summary>
    /// <param name="token">The JWT token to get claims from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with ClaimsPrincipal containing all original JWT claims if successful, error if invalid</returns>
    Task<Result<ClaimsPrincipal, Error>> GetRawClaimsAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validated user data from Dynamic.xyz JWT token
/// </summary>
public record DynamicUserData(
    string AxonUserId,
    string Email,
    string EnvironmentId,
    List<WalletData> Wallets,
    DateTimeOffset? FirstVisitUtc,
    DateTimeOffset? LastVisitUtc,
    bool IsNewUser,
    string? SessionPublicKey = null,
    Dictionary<string, object>? VerifiedCredentialsHashes = null);

/// <summary>
/// Wallet information for authenticated user from Dynamic.xyz
/// </summary>
public record WalletData(
    string Id,
    string Address,
    string Chain,
    string? WalletName,
    string Provider,
    DateTimeOffset? ConnectedAtUtc);