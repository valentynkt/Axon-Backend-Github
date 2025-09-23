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
        // Add ASP.NET Core Identity with Entity Framework stores
        services.AddDbContext<AxonIdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   .UseSnakeCaseNamingConvention());

        services.AddIdentity<AxonUser, AxonRole>(options =>
        {
            // Simplified password requirements for rapid development
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;

            // User settings
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AxonIdentityDbContext>()
        .AddDefaultTokenProviders();

        // Add JWT authentication with simplified configuration
        AddSimplifiedJwtAuthentication(services, configuration);

        // Add DataProtection for challenge generation
        services.AddDataProtection()
            .PersistKeysToDbContext<AxonIdentityDbContext>();

        // Register simplified services
        services.AddScoped<TokenService>();
        services.AddScoped<ChallengeService>();
        services.AddScoped<SimpleSignatureVerifier>();
        services.AddScoped<AxonClaimsTransformation>();

        // Keep the existing application and infrastructure services for now
        // TODO: These will be cleaned up in later refactoring
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration);

        // Register rate limiting for auth endpoints
        AddRateLimiting(services);
    }

    private static void AddSimplifiedJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        // Prevent implicit claim remapping globally
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        var signingKey = configuration["Authentication:SigningKey"]
            ?? throw new InvalidOperationException("Authentication:SigningKey is required");
        var issuer = configuration["Authentication:Issuer"]
            ?? throw new InvalidOperationException("Authentication:Issuer is required");
        var audience = configuration["Authentication:Audience"] ?? "axon-api";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        })
        // Keep Dynamic JWT for backward compatibility during transition
        .AddJwtBearer("DynamicJwt", options =>
        {
            var dynamicSection = configuration.GetSection("Dynamic");
            options.Authority = dynamicSection["Authority"];
            options.Audience = dynamicSection["Audience"];

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = dynamicSection["Issuer"] ?? "https://app.dynamic.xyz",
                ValidateAudience = !string.IsNullOrEmpty(dynamicSection["Audience"]),
                ValidAudience = dynamicSection["Audience"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        });

        // Add claims transformation for enhanced user claims
        services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, AxonClaimsTransformation>();

        // Add authorization policies
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("AxonUser", policy => policy.RequireClaim("axon_user_id"));
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