using Axon.BuildingBlocks.Web.Configuration;
using Axon.Modules.Identity.Application.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.Authentication.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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

    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration _)
    {
        // Configure Dynamic JWT authentication options
        services.Configure<DynamicJwtAuthenticationOptions>(options =>
        {
            options.Realm = "Axon API";
        });

        // Add JWT Bearer authentication scheme
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<DynamicJwtAuthenticationOptions, Axon.Modules.Identity.Infrastructure.Authentication.Handlers.DynamicJwtAuthenticationHandler>(
                JwtBearerDefaults.AuthenticationScheme, 
                "Dynamic JWT Authentication", 
                options => { });

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