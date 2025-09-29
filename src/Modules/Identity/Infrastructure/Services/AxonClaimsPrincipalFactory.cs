using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Custom claims principal factory that adds Axon-specific claims during authentication.
/// Replaces the need for IClaimsTransformation by adding claims at authentication time.
/// </summary>
public sealed class AxonClaimsPrincipalFactory : UserClaimsPrincipalFactory<AxonUserAuth, IdentityRole<Guid>>
{
    private readonly IAxonPrincipalReadRepository _principalRepo;
    private readonly ILogger<AxonClaimsPrincipalFactory> _logger;

    public AxonClaimsPrincipalFactory(
        UserManager<AxonUserAuth> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> options,
        IAxonPrincipalReadRepository principalRepo,
        ILogger<AxonClaimsPrincipalFactory> logger)
        : base(userManager, roleManager, options)
    {
        _principalRepo = principalRepo ?? throw new ArgumentNullException(nameof(principalRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a ClaimsPrincipal with enhanced Axon-specific claims.
    /// This method is called during authentication, reducing the need for per-request transformations.
    /// </summary>
    public override async Task<ClaimsPrincipal> CreateAsync(AxonUserAuth user)
    {
        var principal = await base.CreateAsync(user);
        var identity = principal.Identity as ClaimsIdentity;

        if (identity == null)
            return principal;

        try
        {
            // Add Axon-specific claims
            await AddAxonClaimsAsync(identity, user);

            _logger.LogDebug("Created enhanced principal for user {UserId} with {ClaimCount} claims",
                user.Id, identity.Claims.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding Axon claims for user {UserId}", user.Id);
            // Return principal with base claims on error
        }

        return principal;
    }

    /// <summary>
    /// Generates additional claims specific to the user's role.
    /// Called automatically by CreateAsync.
    /// </summary>
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AxonUserAuth user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Add core Axon claims
        identity.AddClaim(new Claim("axon_user_id", user.AxonPrincipalId.Value.ToString()));
        identity.AddClaim(new Claim("provider_type", user.ProviderType));
        identity.AddClaim(new Claim("original_issuer", user.OriginalIssuer));
        identity.AddClaim(new Claim("original_subject", user.OriginalSubject));

        // Add authentication metadata
        if (user.FirstAuthenticatedAt.HasValue)
        {
            identity.AddClaim(new Claim("first_authenticated_at",
                user.FirstAuthenticatedAt.Value.ToString("O")));
        }

        identity.AddClaim(new Claim("last_authenticated_at",
            user.LastAuthenticatedAt.ToString("O")));

        // Add wallet/chain preferences
        if (!string.IsNullOrEmpty(user.PrimaryChainId))
        {
            identity.AddClaim(new Claim("primary_chain", user.PrimaryChainId));

            // Parse chain components
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

        if (!string.IsNullOrEmpty(user.PrimaryWalletAddress))
        {
            identity.AddClaim(new Claim("primary_wallet", user.PrimaryWalletAddress));
        }

        // Add Dynamic.xyz context if applicable
        if (user.IsDynamicUser())
        {
            identity.AddClaim(new Claim("is_dynamic_user", "true"));

            if (!string.IsNullOrEmpty(user.DynamicEnvironmentId))
            {
                identity.AddClaim(new Claim("dynamic_env", user.DynamicEnvironmentId));
            }

            if (!string.IsNullOrEmpty(user.DynamicUserId))
            {
                identity.AddClaim(new Claim("dynamic_user_id", user.DynamicUserId));
            }
        }

        // Add session context
        var sessionId = Guid.NewGuid().ToString("N");
        identity.AddClaim(new Claim("session_id", sessionId));
        identity.AddClaim(new Claim("session_started", DateTimeOffset.UtcNow.ToString("O")));

        return identity;
    }

    private async Task AddAxonClaimsAsync(ClaimsIdentity identity, AxonUserAuth user)
    {
        // Get the principal aggregate for additional context
        var principal = await _principalRepo.GetByIdAsync(
            user.AxonPrincipalId,
            CancellationToken.None);

        if (principal == null)
        {
            _logger.LogWarning("Principal not found for user {UserId} with principal ID {PrincipalId}",
                user.Id, user.AxonPrincipalId);
            return;
        }

        // Add principal type and metadata
        identity.AddClaim(new Claim("principal_type", principal.Type.ToString()));
        identity.AddClaim(new Claim("principal_created_at", principal.CreatedAt.ToString("O")));

        // Add identity credentials count
        var credentialCount = principal.Credentials.Count;
        identity.AddClaim(new Claim("identity_credential_count", credentialCount.ToString()));

        // Add chain defaults for multi-chain support
        foreach (var chainDefault in principal.PrincipalChainDefaults)
        {
            identity.AddClaim(new Claim(
                $"chain_default:{chainDefault.ChainId}",
                chainDefault.WalletId.Value.ToString()));
        }

        // Add wallet ownership count
        var walletOwnershipCount = principal.WalletOwnerships.Count;
        if (walletOwnershipCount > 0)
        {
            identity.AddClaim(new Claim("wallet_ownership_count", walletOwnershipCount.ToString()));

            // Add verified ownership count for security context
            var verifiedOwnerships = principal.WalletOwnerships
                .Where(wo => wo.Status == Domain.Enums.OwnershipStatus.Verified)
                .Count();

            identity.AddClaim(new Claim("verified_wallet_count", verifiedOwnerships.ToString()));
        }
    }
}