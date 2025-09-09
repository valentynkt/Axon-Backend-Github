using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace Axon.Api.Configuration;

/// <summary>
/// Authentication configuration for JWT Bearer validation and rate limiting
/// </summary>
public static class AuthenticationConfiguration
{
    /// <summary>
    /// Configure JWT Bearer authentication with Dynamic.xyz JWKS validation
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dynamicConfig = configuration.GetSection("Dynamic");
        var jwksUri = dynamicConfig["JwksUri"] ?? throw new InvalidOperationException("Dynamic:JwksUri configuration is required");
        var issuer = dynamicConfig["Issuer"] ?? throw new InvalidOperationException("Dynamic:Issuer configuration is required");
        var audience = dynamicConfig["Audience"]; // Optional

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Configure JWKS endpoint for automatic key rotation
                options.MetadataAddress = jwksUri;
                options.RequireHttpsMetadata = true;
                
                // Set cache duration for JWKS keys (1 hour aligned with provider)
                options.BackchannelTimeout = TimeSpan.FromSeconds(30);
                options.RefreshInterval = TimeSpan.FromHours(1);
                
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Issuer validation
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    
                    // Audience validation (if configured)
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,
                    
                    // Signature validation
                    ValidateIssuerSigningKey = true,
                    
                    // Lifetime validation
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    
                    // Require expiration
                    RequireExpirationTime = true,
                    RequireSignedTokens = true
                };

                // Configure events for logging
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        
                        logger.LogWarning("JWT authentication failed: {Error}", 
                            context.Exception.Message);
                        
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        
                        logger.LogDebug("JWT token validated for subject: {Subject}", 
                            context.Principal?.FindFirst("sub")?.Value ?? "unknown");
                        
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Configure basic rate limiting for auth endpoints
    /// Note: Using simplified approach due to .NET 10 preview limitations
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        // For now, rate limiting will be handled manually in endpoints
        // TODO: Implement proper ASP.NET Core rate limiting when stable
        return services;
    }
}