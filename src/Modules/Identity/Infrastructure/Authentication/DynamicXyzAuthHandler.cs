using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Custom authentication handler for Dynamic.xyz JWT validation
/// Validates tokens via Dynamic.xyz API instead of local JWT validation
/// </summary>
public sealed class DynamicXyzAuthHandler : AuthenticationHandler<DynamicXyzAuthOptions>
{
    private readonly IDynamicAuthService _dynamicAuthService;
    
    public DynamicXyzAuthHandler(
        IOptionsMonitor<DynamicXyzAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDynamicAuthService dynamicAuthService)
        : base(options, logger, encoder)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for Authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            Logger.LogDebug("No Authorization header found");
            return AuthenticateResult.NoResult();
        }

        // Extract token from header
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
                Logger.LogError("Token validation failed: {ErrorCode} - {ErrorMessage}", 
                    validationResult.Error.Code, validationResult.Error.Message);
                
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

            var userData = validationResult.Value;
            
            // Create claims from validated user data
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userData.UserId),
                new(ClaimTypes.Email, userData.Email),
                new("environment_id", userData.EnvironmentId),
                new("is_new_user", userData.IsNewUser.ToString().ToLower())
            };

            // Add wallet claims
            foreach (var wallet in userData.Wallets)
            {
                claims.Add(new Claim("wallet", wallet.Address));
                claims.Add(new Claim($"wallet:{wallet.Chain}", wallet.Address));
                claims.Add(new Claim($"wallet:provider:{wallet.Chain}", wallet.Provider));
            }

            // Add visit timestamps if available
            if (userData.FirstVisitUtc.HasValue)
            {
                claims.Add(new Claim("first_visit", userData.FirstVisitUtc.Value.ToString("O")));
            }
            
            if (userData.LastVisitUtc.HasValue)
            {
                claims.Add(new Claim("last_visit", userData.LastVisitUtc.Value.ToString("O")));
            }

            // Add session public key if available
            if (!string.IsNullOrWhiteSpace(userData.SessionPublicKey))
            {
                claims.Add(new Claim("session_public_key", userData.SessionPublicKey));
            }

            // Create identity and principal
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogInformation("Successfully authenticated user {UserId}", userData.UserId);
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
}

/// <summary>
/// Options for Dynamic.xyz authentication handler
/// </summary>
public sealed class DynamicXyzAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The realm to use in WWW-Authenticate challenge headers
    /// </summary>
    public string Realm { get; set; } = "Axon API";

    /// <summary>
    /// Whether to allow anonymous access when authentication fails
    /// </summary>
    public bool AllowAnonymous { get; set; } = true;
}