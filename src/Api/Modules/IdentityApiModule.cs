using Axon.BuildingBlocks.Web.Configuration;
using Axon.Modules.Identity.Application.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
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
        // Get JWT configuration from appsettings
        var jwtSection = configuration.GetSection("Axon");
        var issuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Axon:Issuer configuration is required");
        var audience = jwtSection["Audience"] ?? "axon-api";
        var signingKey = jwtSection["SigningKey"] ?? throw new InvalidOperationException("Axon:SigningKey configuration is required");

        // Add standard JWT Bearer authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Keep JWT claims as-is; avoid automatic mapping to WS-* claim types
                options.MapInboundClaims = false;

                // Recommended for public APIs
                options.RequireHttpsMetadata = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Issuer / audience validation - STRICT
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,

                    // Signature validation - MANDATORY
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    RequireSignedTokens = true,

                    // Lifetime validation - SHORT EXPIRY
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30), // Reduced from 60 seconds for tighter security

                    // Token type validation - SECURITY BEST PRACTICE
                    ValidTypes = new[] { "JWT", "at+jwt" }, // Only allow specific token types
                    ValidateTokenReplay = false, // Handled by our replay protection service

                    // Use "sub" as the name claim
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };

                // Diagnostics
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Auth.JwtBearer");

                        logger.LogWarning(context.Exception, "JWT authentication failed");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Auth.JwtBearer");

                        var subject = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
                        logger.LogDebug("JWT validated for sub={Subject}", subject);
                        return Task.CompletedTask;
                    }
                };
            });

        // Add authorization using AddAuthorizationBuilder for modern ASP.NET Core
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

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", token);
            };
        });
    }
}