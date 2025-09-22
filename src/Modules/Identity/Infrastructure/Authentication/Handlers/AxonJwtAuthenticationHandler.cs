using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Axon.Modules.Identity.Infrastructure.Authentication.Options;

namespace Axon.Modules.Identity.Infrastructure.Authentication.Handlers;

/// <summary>
/// Authentication handler for Axon internal JWT validation
/// Validates Axon-issued JWTs for protected endpoints (e.g., /auth/me)
/// </summary>
public sealed class AxonJwtAuthenticationHandler : AuthenticationHandler<AxonJwtAuthenticationOptions>
{
    private static readonly string[] ValidJwtTypes = new[] { "JWT", "at+jwt" }; // access tokens
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public AxonJwtAuthenticationHandler(
        IOptionsMonitor<AxonJwtAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 0) Config sanity checks
        if (Options.SigningKeys is null || Options.SigningKeys.Count == 0)
        {
            Logger.LogError("Axon JWT handler misconfigured: no signing keys provided");
            return Task.FromResult(AuthenticateResult.Fail("Server misconfiguration"));
        }
        if (Options.ValidIssuers is null || Options.ValidIssuers.Count == 0)
        {
            Logger.LogError("Axon JWT handler misconfigured: no valid issuers provided");
            return Task.FromResult(AuthenticateResult.Fail("Server misconfiguration"));
        }

        // 1) Authorization header presence/shape
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            Logger.LogDebug("No Authorization header for Axon JWT");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogDebug("Invalid Authorization header format for Axon JWT");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            Logger.LogWarning("Empty Axon JWT token in Authorization header");
            return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
        }

        try
        {
            // 2) Quick shape check
            if (!_tokenHandler.CanReadToken(token))
            {
                Logger.LogWarning("Invalid Axon JWT token format");
                return Task.FromResult(AuthenticateResult.Fail("Invalid token format"));
            }

            // 3) Peek issuer + kid without validation
            var jwt = _tokenHandler.ReadJwtToken(token);
            var issuer = jwt.Issuer;
            var issuerOk = Options.ValidIssuers.Any(i => string.Equals(i, issuer, StringComparison.Ordinal));
            if (!issuerOk)
            {
                Logger.LogWarning("Invalid token issuer: {Issuer}", issuer ?? "null");
                return Task.FromResult(AuthenticateResult.Fail("Invalid token issuer"));
            }

            // 4) Build strict validation params
            var tvp = new TokenValidationParameters
            {
                // Issuer
                ValidateIssuer = true,
                ValidIssuers = Options.ValidIssuers,

                // Audience
                ValidateAudience = Options.ValidateAudience && (Options.ValidAudiences?.Count > 0),
                ValidAudiences = Options.ValidAudiences,

                // Signature
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = Options.SigningKeys,

                // Lifetime
                ValidateLifetime = true,
                ClockSkew = Options.ClockSkew,
                RequireExpirationTime = true,
                RequireSignedTokens = true,

                // Claims mapping
                NameClaimType = "sub",
                RoleClaimType = "role",

                // Token type (typ) hardening
                ValidTypes = ValidJwtTypes
            };

            // 5) Validate
            var principal = _tokenHandler.ValidateToken(token, tvp, out var validatedToken);

            // 6) Preserve kid (helpful for audits/rotation)
            var kid = jwt.Header.Kid;
            if (!string.IsNullOrEmpty(kid) && !principal.HasClaim("kid", kid))
            {
                var id = (ClaimsIdentity)principal.Identity!;
                id.AddClaim(new Claim("kid", kid));
            }

            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            Logger.LogInformation("Authenticated Axon JWT for sub={Sub}", principal.FindFirst("sub")?.Value ?? "unknown");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (SecurityTokenExpiredException)
        {
            Logger.LogWarning("Axon JWT token has expired");
            return Task.FromResult(AuthenticateResult.Fail("Token has expired"));
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            Logger.LogWarning("Axon JWT token signature invalid");
            return Task.FromResult(AuthenticateResult.Fail("Token signature is invalid"));
        }
        catch (SecurityTokenValidationException ex)
        {
            Logger.LogWarning("Axon JWT validation failed: {Error}", ex.Message);
            return Task.FromResult(AuthenticateResult.Fail("Token validation failed"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during Axon JWT authentication");
            return Task.FromResult(AuthenticateResult.Fail("Authentication error"));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate =
            $"Bearer realm=\"{Options.Realm}\", error=\"invalid_token\", error_description=\"Axon JWT required or invalid\"";
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}
