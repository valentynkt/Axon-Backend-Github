using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Api.Configuration;

/// <summary>
/// Authentication configuration for Axon-issued JWT validation.
/// Strategy B: client calls /auth/exchange with a Dynamic JWT (as input only),
/// backend mints an Axon JWT, and the client uses the Axon JWT for all
/// subsequent protected API calls (e.g., /auth/me).
/// </summary>
public static class AuthenticationConfiguration
{
    /// <summary>
    /// Registers the "AxonJwt" authentication scheme as the default scheme.
    /// </summary>
    /// <remarks>
    /// Required configuration keys:
    ///   Axon:Issuer     (string, required)
    ///   Axon:Audience   (string, optional; if empty, audience validation is disabled)
    ///   Axon:SigningKey (string, required; symmetric key for HS256/HS512)
    ///
    /// Endpoints:
    ///   - /api/v1/auth/exchange : AllowAnonymous (reads Dynamic token as input, no auth)
    ///   - Protected endpoints    : AuthSchemes("AxonJwt") or [Authorize(AuthenticationSchemes="AxonJwt")]
    /// </remarks>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var axonSection   = configuration.GetSection("Axon");
        var axonIssuer    = axonSection["Issuer"]     ?? throw new InvalidOperationException("Axon:Issuer configuration is required");
        var axonAudience  = axonSection["Audience"];  // optional
        var axonSigningKey = axonSection["SigningKey"] ?? throw new InvalidOperationException("Axon:SigningKey configuration is required");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(axonSigningKey));

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme          = "AxonJwt";
                options.DefaultAuthenticateScheme = "AxonJwt";
                options.DefaultChallengeScheme    = "AxonJwt";
            })
            .AddJwtBearer("AxonJwt", options =>
            {
                // Keep JWT claims as-is; avoid automatic mapping to WS-* claim types.
                options.MapInboundClaims = false;

                // Recommended for public APIs.
                options.RequireHttpsMetadata = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Issuer / audience
                    ValidateIssuer = true,
                    ValidIssuer    = axonIssuer,

                    ValidateAudience = !string.IsNullOrEmpty(axonAudience),
                    ValidAudience    = axonAudience,

                    // Signature
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = signingKey,

                    // Lifetime
                    ValidateLifetime     = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens   = true,
                    ClockSkew             = TimeSpan.FromSeconds(60),

                    // Keep "sub" as the name claim for convenience
                    NameClaimType = "sub"
                };

                // Diagnostics
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Auth.AxonJwt");

                        logger.LogWarning(context.Exception, "Axon JWT authentication failed");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Auth.AxonJwt");

                        var subject = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
                        logger.LogDebug("Axon JWT validated for sub={Subject}", subject);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
