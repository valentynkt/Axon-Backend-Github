using Axon.BuildingBlocks.Web.Configuration;
using Axon.Modules.Identity.Application.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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
        
        // Register Identity Infrastructure services (repositories, external services)
        services.AddIdentityInfrastructure(configuration);
        
        // Register JWT Bearer authentication using Dynamic.xyz
        AddJwtAuthentication(services, configuration);
        
        // Register rate limiting for auth endpoints
        AddRateLimiting(services);
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
            options.DefaultAuthenticateScheme = "AxonJwt";
            options.DefaultChallengeScheme = "AxonJwt";
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

            // Diagnostics for Dynamic JWT
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Auth.DynamicJwt");

                    logger.LogWarning(context.Exception, "Dynamic JWT authentication failed");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Auth.DynamicJwt");

                    var subject = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
                    logger.LogDebug("Dynamic JWT validated for sub={Subject}", subject);
                    return Task.CompletedTask;
                }
            };
        })
        .AddJwtBearer("AxonJwt", options =>
        {
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;

            // Convert Base64 signing key
            var keyBytes = Convert.FromBase64String(axonSigningKey);
            var key = new SymmetricSecurityKey(keyBytes);

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
                ValidateTokenReplay = false,
                NameClaimType = "sub",
                RoleClaimType = "role"
            };

            // Diagnostics for Axon JWT
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
}