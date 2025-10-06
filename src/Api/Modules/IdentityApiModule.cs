using Axon.BuildingBlocks.Web.Configuration;
using Axon.Modules.Identity.Application.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.DependencyInjection;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;

namespace Axon.Api.Modules;

/// <summary>
/// Identity module API registration with JWT authentication and rate limiting
/// </summary>
public sealed class IdentityApiModule : IApiModule
{
    public string ModuleName => "Identity";
    public string Version => "v1";

    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Register Identity Application layer services (handlers, validators, orchestration)
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration, environment);

        // Register JWT Bearer authentication using Dynamic.xyz
        AddJwtAuthentication(services, configuration);

        // Register JWT event handlers (includes replay protection via orchestrator)
        services.AddScoped<JwtEventHandlers>();

        // Register rate limiting for auth endpoints
        AddRateLimiting(services);

        // Configure authorization policies for different authentication scenarios
        ConfigureAuthorizationPolicies(services);
    }

    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        // Prevent implicit claim remapping globally
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        // Get configuration sections
        var dynamicSection = configuration.GetSection("Dynamic");
        var axonSection = configuration.GetSection("Axon");
        var authSection = configuration.GetSection("Authentication");

        // Dynamic JWT configuration
        var dynamicAuthority = dynamicSection["Authority"];
        var dynamicAudience = dynamicSection["Audience"];
        var dynamicJwksUri = dynamicSection["JwksUri"];
        var dynamicIssuer = dynamicSection["Issuer"];

        // Axon JWT configuration
        var axonIssuer = axonSection["Issuer"] ?? throw new InvalidOperationException("Axon:Issuer configuration is required");
        var axonAudience = axonSection["Audience"] ?? "axon-api";
        var axonSigningKey = axonSection["SigningKey"] ?? throw new InvalidOperationException("Axon:SigningKey configuration is required");

        // Clock skew configuration
        var clockSkewSeconds = authSection.GetValue("ClockSkewSeconds", 60);

        services.AddAuthentication(options =>
        {
            // Use policy selector for multiple schemes
            options.DefaultScheme = "DynamicOrAxon";
            options.DefaultAuthenticateScheme = "DynamicOrAxon";
            options.DefaultChallengeScheme = "AxonJwt";
        })
        .AddPolicyScheme("DynamicOrAxon", "Dynamic or Axon JWT", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                // Exchange endpoint is excluded from middleware, so this won't be called for that route
                var authorization = context.Request.Headers.Authorization.FirstOrDefault();
                if (string.IsNullOrEmpty(authorization))
                    return "AxonJwt";

                // Parse the token to check issuer (without validation)
                if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authorization["Bearer ".Length..];
                    try
                    {
                        var handler = new JwtSecurityTokenHandler();
                        if (handler.CanReadToken(token))
                        {
                            var jwtToken = handler.ReadJwtToken(token);
                            // Dynamic tokens have issuer starting with app.dynamicauth.com
                            if (jwtToken.Issuer?.StartsWith("app.dynamicauth.com", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                // Log when Dynamic JWT is handled by middleware (should be rare now)
                                var logger = context.RequestServices.GetRequiredService<ILogger<IdentityApiModule>>();
                                logger.LogDebug("Dynamic JWT processed by middleware for path: {Path}", context.Request.Path);
                                return "DynamicJwt";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log token parsing failures
                        var logger = context.RequestServices.GetRequiredService<ILogger<IdentityApiModule>>();
                        logger.LogDebug(ex, "Failed to parse JWT for scheme selection, defaulting to AxonJwt");
                    }
                }

                return "AxonJwt";
            };
        })
        .AddJwtBearer("DynamicJwt", options =>
        {
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;

            // Configure Dynamic JWT validation
            if (!string.IsNullOrEmpty(dynamicAuthority))
            {
                // Use OIDC discovery if Authority is provided
                options.Authority = dynamicAuthority;
                options.Audience = dynamicAudience;
            }
            else if (!string.IsNullOrEmpty(dynamicJwksUri))
            {
                // Alternative: Use JWKS URI directly
                options.MetadataAddress = dynamicJwksUri;
            }

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = dynamicIssuer ?? "https://app.dynamic.xyz",
                ValidateAudience = !string.IsNullOrEmpty(dynamicAudience),
                ValidAudience = dynamicAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(clockSkewSeconds),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };

            // Enhanced events for Dynamic JWT with claims transformation
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.OnAuthenticationFailedAsync(context, "DynamicJwt");
                },
                OnTokenValidated = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.ValidateDynamicTokenAsync(context);
                },
                OnChallenge = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.OnChallengeAsync(context, "DynamicJwt");
                },
                OnForbidden = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.OnForbiddenAsync(context, "DynamicJwt");
                }
            };
        })
        .AddJwtBearer("AxonJwt", options =>
        {
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;

            // Convert Base64 signing key
            var keyBytes = Convert.FromBase64String(axonSigningKey);
            var key = new SymmetricSecurityKey(keyBytes)
            {
                KeyId = "axon_key_001" // Set KeyId to match token generation
            };

            // Get the TokenReplayCache from DI for replay protection
            var serviceProvider = services.BuildServiceProvider();
            var tokenReplayCache = serviceProvider.GetRequiredService<Microsoft.IdentityModel.Tokens.ITokenReplayCache>();

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = axonIssuer,
                ValidateIssuer = true,
                ValidateAudience = !string.IsNullOrEmpty(axonAudience),
                ValidAudience = axonAudience,
                IssuerSigningKey = key,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(clockSkewSeconds),
                ValidTypes = new[] { "JWT", "at+jwt" },
                ValidateTokenReplay = true, // Enable built-in token replay validation
                TokenReplayCache = tokenReplayCache, // Use unified replay cache
                NameClaimType = "sub",
                RoleClaimType = "role"
            };

            // Enhanced events for Axon JWT with replay protection and security stamp validation
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.OnAuthenticationFailedAsync(context, "AxonJwt");
                },
                OnTokenValidated = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.ValidateAxonTokenAsync(context);
                },
                OnChallenge = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.OnChallengeAsync(context, "AxonJwt");
                },
                OnForbidden = context =>
                {
                    var handlers = context.HttpContext.RequestServices.GetRequiredService<JwtEventHandlers>();
                    return handlers.OnForbiddenAsync(context, "AxonJwt");
                }
            };
        });

        // Add authorization policies
        services.AddAuthorizationBuilder()
            .AddPolicy("Authenticated", policy =>
            {
                policy.RequireAuthenticatedUser();
            });
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global rate limit
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // Specific policy for auth exchange endpoint (stricter limits)
            options.AddPolicy("AuthExchange", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10, // 10 requests per minute per IP
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // Specific policy for auth challenge endpoint (stricter limits)
            options.AddPolicy("AuthChallenge", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 15, // 15 requests per minute per IP (slightly higher than exchange)
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", token);
            };
        });
    }

    private static void ConfigureAuthorizationPolicies(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Policy for endpoints that accept either Dynamic or Axon tokens
            options.AddPolicy("DynamicOrAxon", policy =>
                policy.AddAuthenticationSchemes("DynamicJwt", "AxonJwt")
                      .RequireAuthenticatedUser());

            // Policy for Dynamic-only endpoints
            options.AddPolicy("DynamicOnly", policy =>
                policy.AddAuthenticationSchemes("DynamicJwt")
                      .RequireAuthenticatedUser());

            // Policy for Axon-only endpoints (internal services)
            options.AddPolicy("AxonOnly", policy =>
                policy.AddAuthenticationSchemes("AxonJwt")
                      .RequireAuthenticatedUser()
                      .RequireClaim("provider_type"));

            // Policy for verified wallet users
            options.AddPolicy("VerifiedWallet", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("primary_wallet")
                      .RequireClaim("wallet_count"));

            // Policy for multi-chain users
            options.AddPolicy("MultiChain", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(context =>
                      {
                          var walletCountClaim = context.User.FindFirst("wallet_count");
                          if (walletCountClaim != null && int.TryParse(walletCountClaim.Value, out var count))
                              return count > 1;
                          return false;
                      }));

            // Default policy - accepts any authenticated user
            options.DefaultPolicy = options.GetPolicy("DynamicOrAxon") ??
                new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

            // FallbackPolicy - set to null to allow AllowAnonymous to work correctly
            // NOTE: In ASP.NET Core, authentication middleware ALWAYS runs regardless of AllowAnonymous.
            // However, FallbackPolicy triggers authorization checks which can cause issues for endpoints
            // like Exchange that manually handle authentication. By setting FallbackPolicy to null,
            // we allow AllowAnonymous() to properly bypass authorization while authentication still runs.
            // Endpoints that need authentication should explicitly use Policies() or [Authorize].
            options.FallbackPolicy = null;
        });
    }
}