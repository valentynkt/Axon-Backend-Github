namespace Axon.Modules.Identity.Infrastructure.Services;
using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// Transforms claims from wallet-based authentication to include additional context
/// about the user's principal, wallets, and chain preferences.
/// </summary>
public sealed class WalletClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IAxonPrincipalReadRepository _principalRepo;
    private readonly IWalletOwnershipRepository _walletOwnershipRepo;
    private readonly ILogger<WalletClaimsTransformation> _logger;

    public WalletClaimsTransformation(
        UserManager<AxonUserAuth> userManager,
        IAxonPrincipalReadRepository principalRepo,
        IWalletOwnershipRepository walletOwnershipRepo,
        ILogger<WalletClaimsTransformation> logger)
    {
        _userManager = userManager;
        _principalRepo = principalRepo;
        _walletOwnershipRepo = walletOwnershipRepo;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only transform authenticated principals
        if (principal?.Identity?.IsAuthenticated != true)
            return principal;

        try
        {
            // Get the user ID from claims
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return principal;

            // Find the AxonUserAuth
            var user = await _userManager.FindByIdAsync(userIdClaim);
            if (user == null)
                return principal;

            // Clone the existing identity and add new claims
            var claimsIdentity = principal.Identity as ClaimsIdentity;
            if (claimsIdentity == null)
                return principal;

            var newIdentity = new ClaimsIdentity(
                claimsIdentity.Claims,
                claimsIdentity.AuthenticationType,
                claimsIdentity.NameClaimType,
                claimsIdentity.RoleClaimType);

            // Add wallet-specific claims
            await AddWalletClaims(newIdentity, user);

            // Add principal-specific claims
            await AddPrincipalClaims(newIdentity, user);

            // Add chain preferences
            AddChainClaims(newIdentity, user);

            // Add authentication metadata
            AddAuthenticationMetadata(newIdentity, user);

            var transformedPrincipal = new ClaimsPrincipal(newIdentity);

            _logger.LogDebug(
                "Transformed claims for user {UserId} with {ClaimCount} total claims",
                user.Id, newIdentity.Claims.Count());

            return transformedPrincipal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming claims for principal");
            return principal; // Return original on error
        }
    }

    private static async Task AddWalletClaims(ClaimsIdentity identity, AxonUserAuth user)
    {
        // For now, we'll skip wallet claims until we have a proper method
        // to get wallet ownerships by principal ID
        // This would require adding a method to IWalletOwnershipRepository

        // Add primary wallet if set on the user
        if (!string.IsNullOrEmpty(user.PrimaryWalletAddress))
        {
            identity.AddClaim(new Claim("primary_wallet", user.PrimaryWalletAddress));
        }
    }

    private async Task AddPrincipalClaims(ClaimsIdentity identity, AxonUserAuth user)
    {
        // Get the principal aggregate for additional context
        var principal = await _principalRepo.GetByIdAsync(
            user.AxonPrincipalId,
            CancellationToken.None);

        if (principal == null)
            return;

        // Add principal type
        identity.AddClaim(new Claim("principal_type", principal.Type.ToString()));

        // Add creation timestamp
        identity.AddClaim(new Claim("principal_created_at",
            principal.CreatedAt.ToString("O")));

        // Add identity credentials count
        var credentialCount = principal.Credentials.Count;
        identity.AddClaim(new Claim("identity_credential_count", credentialCount.ToString()));

        // Add chain defaults
        foreach (var chainDefault in principal.PrincipalChainDefaults)
        {
            identity.AddClaim(new Claim(
                $"chain_default:{chainDefault.ChainId}",
                chainDefault.WalletId.Value.ToString()));
        }
    }

    private static void AddChainClaims(ClaimsIdentity identity, AxonUserAuth user)
    {
        // Add primary chain if set
        if (!string.IsNullOrEmpty(user.PrimaryChainId))
        {
            identity.AddClaim(new Claim("primary_chain", user.PrimaryChainId));
        }

        // Add primary wallet address if set
        if (!string.IsNullOrEmpty(user.PrimaryWalletAddress))
        {
            identity.AddClaim(new Claim("primary_wallet_address", user.PrimaryWalletAddress));
        }

        // Parse chain from PrimaryChainId (e.g., "solana-mainnet" -> "solana")
        if (!string.IsNullOrEmpty(user.PrimaryChainId))
        {
            var chainParts = user.PrimaryChainId.Split('-');
            if (chainParts.Length > 0)
            {
                identity.AddClaim(new Claim("chain_type", chainParts[0]));
            }
            if (chainParts.Length > 1)
            {
                identity.AddClaim(new Claim("chain_network", chainParts[1]));
            }
        }
    }

    private static void AddAuthenticationMetadata(ClaimsIdentity identity, AxonUserAuth user)
    {
        // Add authentication timestamps
        if (user.FirstAuthenticatedAt.HasValue)
        {
            identity.AddClaim(new Claim("first_authenticated_at",
                user.FirstAuthenticatedAt.Value.ToString("O")));
        }

        identity.AddClaim(new Claim("last_authenticated_at",
            user.LastAuthenticatedAt.ToString("O")));

        // Add authentication context
        identity.AddClaim(new Claim("auth_provider", user.ProviderType));
        identity.AddClaim(new Claim("auth_issuer", user.OriginalIssuer));

        // Add Dynamic.xyz context if applicable
        if (user.IsDynamicUser())
        {
            identity.AddClaim(new Claim("is_dynamic_user", "true"));

            if (!string.IsNullOrEmpty(user.DynamicEnvironmentId))
            {
                identity.AddClaim(new Claim("dynamic_env", user.DynamicEnvironmentId));
            }
        }

        // Add session context
        var sessionId = Guid.NewGuid().ToString();
        identity.AddClaim(new Claim("session_id", sessionId));
        identity.AddClaim(new Claim("session_key", user.GetSessionKey(sessionId)));
    }
}