using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Infrastructure.Services;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Modern claims transformation for Dynamic.xyz JWT tokens using standard .NET patterns
/// Replaces complex custom authentication handlers with standard IClaimsTransformation
/// </summary>
public sealed class DynamicClaimsTransformation : IClaimsTransformation
{
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly ILogger<DynamicClaimsTransformation> _logger;

    public DynamicClaimsTransformation(
        IDynamicAuthService dynamicAuthService,
        ILogger<DynamicClaimsTransformation> logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Only transform if we have a JWT Bearer token but no Dynamic-specific claims yet
        if (!ShouldTransform(principal))
            return principal;

        try
        {
            // Extract the original JWT token from the Authorization header
            // This is available during the authentication pipeline
            var jwtToken = ExtractJwtFromContext(principal);
            if (string.IsNullOrEmpty(jwtToken))
                return principal;

            // Validate and get user data from Dynamic.xyz
            var validationResult = await _dynamicAuthService.ValidateTokenAsync(jwtToken);
            if (validationResult.IsFailure)
            {
                _logger.LogWarning("Dynamic token validation failed during claims transformation: {Error}",
                    validationResult.Error.Message);
                return principal;
            }

            var userData = validationResult.Value;

            // Create a new identity with Dynamic-specific claims
            var dynamicIdentity = new ClaimsIdentity("Dynamic");

            // Add standard Axon user claims
            dynamicIdentity.AddClaim(new Claim("axon_user_id", userData.AxonUserId));

            if (!string.IsNullOrEmpty(userData.Email))
                dynamicIdentity.AddClaim(new Claim(ClaimTypes.Email, userData.Email));

            // Add environment information
            dynamicIdentity.AddClaim(new Claim("environment_id", userData.EnvironmentId));
            dynamicIdentity.AddClaim(new Claim("provider", "dynamic"));

            // Add wallet claims for easier access
            foreach (var wallet in userData.Wallets)
            {
                dynamicIdentity.AddClaim(new Claim("wallet", wallet.Address));
                dynamicIdentity.AddClaim(new Claim($"wallet:{wallet.Chain}", wallet.Address));

                if (!string.IsNullOrEmpty(wallet.Provider))
                    dynamicIdentity.AddClaim(new Claim($"wallet:provider:{wallet.Chain}", wallet.Provider));
            }

            // Add user metadata
            if (userData.FirstVisitUtc.HasValue)
                dynamicIdentity.AddClaim(new Claim("first_visit", userData.FirstVisitUtc.Value.ToString("O")));

            if (userData.LastVisitUtc.HasValue)
                dynamicIdentity.AddClaim(new Claim("last_visit", userData.LastVisitUtc.Value.ToString("O")));

            dynamicIdentity.AddClaim(new Claim("is_new_user", userData.IsNewUser.ToString().ToLowerInvariant()));

            // Add the new identity to the principal
            principal.AddIdentity(dynamicIdentity);

            _logger.LogDebug("Successfully transformed claims for Dynamic user {AxonUserId}", userData.AxonUserId);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Dynamic claims transformation");
            return principal; // Return original principal on error
        }
    }

    private static bool ShouldTransform(ClaimsPrincipal principal)
    {
        // Only transform if:
        // 1. Principal is authenticated
        // 2. Has a JWT Bearer authentication type
        // 3. Doesn't already have Dynamic-specific claims
        return principal.Identity?.IsAuthenticated == true &&
               principal.Identity.AuthenticationType == "Bearer" &&
               !principal.HasClaim("provider", "dynamic");
    }

    private static string? ExtractJwtFromContext(ClaimsPrincipal _ = default!)
    {
        // In a real implementation, this would extract the JWT from the current HttpContext
        // For now, we'll use a placeholder - this would need to be implemented with HttpContextAccessor
        // or passed through the authentication pipeline

        // TODO: Implement proper JWT extraction from HttpContext.Request.Headers.Authorization
        // This is a simplified implementation for the refactoring demonstration
        return null; // Placeholder - would be implemented in production
    }
}