using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Infrastructure.Authentication.Options;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Identity.Infrastructure.Authentication.Handlers;

/// <summary>
/// Authentication handler for Dynamic.xyz JWT validation
/// Validates tokens via Dynamic.xyz API for protected endpoints
/// </summary>
public sealed class DynamicJwtAuthenticationHandler : AuthenticationHandler<DynamicJwtAuthenticationOptions>
{
    private readonly IDynamicAuthService _dynamicAuthService;
    
    public DynamicJwtAuthenticationHandler(
        IOptionsMonitor<DynamicJwtAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDynamicAuthService dynamicAuthService)
        : base(options, logger, encoder)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Simple and clean: if no header, no authentication
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            Logger.LogDebug("No Authorization header found");
            return AuthenticateResult.NoResult();
        }

        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogDebug("Invalid Authorization header format");
            return AuthenticateResult.NoResult();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            Logger.LogWarning("Empty token in Authorization header");
            return AuthenticateResult.Fail("Invalid token");
        }

        try
        {
            // Validate token with Dynamic.xyz
            var validationResult = await _dynamicAuthService.ValidateTokenAsync(token, Context.RequestAborted);
            
            if (validationResult.IsFailure)
            {
                Logger.LogWarning("Token validation failed: {ErrorCode}", validationResult.Error.Code);
                
                var failureMessage = validationResult.Error.Type switch
                {
                    ErrorType.Unauthorized when validationResult.Error.Code == "AUTH.TOKEN_EXPIRED" 
                        => "Token has expired",
                    ErrorType.Unauthorized when validationResult.Error.Code == "AUTH.INVALID_SIGNATURE" 
                        => "Token signature is invalid",
                    ErrorType.Unauthorized => "Token is invalid or expired",
                    ErrorType.Unavailable => "Authentication service unavailable",
                    ErrorType.Timeout => "Authentication timeout",
                    _ => "Authentication failed"
                };
                
                return AuthenticateResult.Fail(failureMessage);
            }

            // Get raw claims from validated token to preserve all original JWT claims
            var rawClaimsResult = await _dynamicAuthService.GetRawClaimsAsync(token, Context.RequestAborted);
            if (rawClaimsResult.IsFailure)
            {
                Logger.LogError("Failed to get raw claims after successful validation: {ErrorCode}", rawClaimsResult.Error.Code);
                return AuthenticateResult.Fail("Failed to retrieve token claims");
            }

            // Build claims identity preserving ALL original claims
            var userData = validationResult.Value;
            var rawClaimsPrincipal = rawClaimsResult.Value;
            var identity = new ClaimsIdentity(rawClaimsPrincipal.Claims, Scheme.Name);

            // Add supplementary claims for easier access (without replacing originals)
            AddSupplementaryClaims(identity, userData);

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogInformation("Successfully authenticated user {AxonUserId}", userData.AxonUserId);
            return AuthenticateResult.Success(ticket);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Authentication cancelled");
            return AuthenticateResult.Fail("Authentication cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during authentication");
            return AuthenticateResult.Fail("An error occurred during authentication");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = $"Bearer realm=\"{Options.Realm}\", error=\"invalid_token\"";
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Adds supplementary claims for easier access without replacing original JWT claims
    /// Only adds claims that don't already exist or need transformation for compatibility
    /// </summary>
    private static void AddSupplementaryClaims(ClaimsIdentity identity, DynamicUserData userData)
    {
        // Add ASP.NET Core standard claims for compatibility (only if not already present)
        if (!identity.HasClaim(ClaimTypes.NameIdentifier, userData.AxonUserId))
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userData.AxonUserId));

        if (!string.IsNullOrWhiteSpace(userData.Email) && !identity.HasClaim(ClaimTypes.Email, userData.Email))
            identity.AddClaim(new Claim(ClaimTypes.Email, userData.Email));

        // Add structured wallet claims for easier querying
        foreach (var wallet in userData.Wallets)
        {
            // Add wallet address claims for different access patterns
            if (!identity.HasClaim("wallet", wallet.Address))
                identity.AddClaim(new Claim("wallet", wallet.Address));

            if (!identity.HasClaim($"wallet:{wallet.Chain}", wallet.Address))
                identity.AddClaim(new Claim($"wallet:{wallet.Chain}", wallet.Address));

            // Add provider information for each chain
            if (!string.IsNullOrEmpty(wallet.Provider))
                identity.AddClaim(new Claim($"wallet:provider:{wallet.Chain}", wallet.Provider));

            // Add wallet name if available
            if (!string.IsNullOrEmpty(wallet.WalletName))
                identity.AddClaim(new Claim($"wallet:name:{wallet.Chain}", wallet.WalletName));

            // Add wallet ID for precise identification
            if (!string.IsNullOrEmpty(wallet.Id))
                identity.AddClaim(new Claim($"wallet:id:{wallet.Chain}", wallet.Id));
        }

        // Add additional metadata claims that may not be in the raw JWT
        if (!identity.HasClaim(c => c.Type == "is_new_user"))
            identity.AddClaim(new Claim("is_new_user", userData.IsNewUser.ToString().ToLowerInvariant()));

        // Add environment_id for multi-tenant support (if not already present)
        if (!string.IsNullOrWhiteSpace(userData.EnvironmentId) && !identity.HasClaim(c => c.Type == "environment_id"))
            identity.AddClaim(new Claim("environment_id", userData.EnvironmentId));

        // Add session public key if available and not already present
        if (!string.IsNullOrWhiteSpace(userData.SessionPublicKey) && !identity.HasClaim(c => c.Type == "session_public_key"))
            identity.AddClaim(new Claim("session_public_key", userData.SessionPublicKey));

        // Add visit timestamps if available and not already present
        if (userData.FirstVisitUtc.HasValue && !identity.HasClaim(c => c.Type == "first_visit"))
            identity.AddClaim(new Claim("first_visit", userData.FirstVisitUtc.Value.ToString("O")));

        if (userData.LastVisitUtc.HasValue && !identity.HasClaim(c => c.Type == "last_visit"))
            identity.AddClaim(new Claim("last_visit", userData.LastVisitUtc.Value.ToString("O")));

        // Add verified credentials hashes as JSON if available and not already present
        if (userData.VerifiedCredentialsHashes?.Count > 0 && !identity.HasClaim(c => c.Type == "verifiedCredentialsHashes"))
        {
            var hashesJson = System.Text.Json.JsonSerializer.Serialize(userData.VerifiedCredentialsHashes);
            identity.AddClaim(new Claim("verifiedCredentialsHashes", hashesJson));
        }
    }
}