using Axon.BuildingBlocks.Web.Configuration;
using Axon.Modules.Identity.Application.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.Authentication;
using Axon.Modules.Identity.Infrastructure.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.HealthChecks;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Abstractions.Authentication;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Axon.Api.Modules;

/// <summary>
/// Identity module API registration
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
        // Register Identity Application layer services (includes MediatR, validators, and authorization services)
        services.AddIdentityApplication();
        
        // Register Identity Infrastructure services (databases, external services)
        services.AddIdentityInfrastructure(configuration);

        // Register Identity Infrastructure services (HTTP clients, external services)
        services.AddDynamicXyzInfrastructure(configuration);
        
        // Register authentication services
        services.AddScoped<IDynamicClaimNormalizer, DynamicClaimNormalizer>();
        
        services.AddScoped<IDynamicAuthService, DynamicAuthService>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var jwksHttpClient = httpClientFactory.CreateClient("JwksClient");
            var cache = serviceProvider.GetRequiredService<IMemoryCache>();
            var logger = serviceProvider.GetRequiredService<ILogger<DynamicAuthService>>();
            var options = serviceProvider.GetRequiredService<IOptions<DynamicXyzOptions>>();
            var normalizer = serviceProvider.GetRequiredService<IDynamicClaimNormalizer>();
            
            return new DynamicAuthService(jwksHttpClient, cache, logger, options, normalizer);
        });
        
        // Register Dynamic JWT exchange services
        services.AddSingleton<Axon.Modules.Identity.Application.Services.IDynamicToCommandsMapper, Axon.Modules.Identity.Application.Services.DynamicToCommandsMapper>();
        services.AddScoped<Axon.Modules.Identity.Application.Services.IDynamicJwtBridge, Axon.Modules.Identity.Infrastructure.Services.DynamicJwtBridge>();
        services.AddScoped<Axon.Modules.Identity.Application.Services.IWalletProcessorService, Axon.Modules.Identity.Application.Services.WalletProcessorService>();
        services.AddScoped<Axon.Modules.Identity.Application.Services.IDynamicAuthOrchestrator, Axon.Modules.Identity.Application.Services.DynamicAuthOrchestrator>();
        
        // Register current user service
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextUserService>();
        
        // Configure Dynamic.xyz authentication
        services.AddAuthentication("DynamicXyz")
            .AddScheme<DynamicXyzAuthOptions, DynamicXyzAuthHandler>("DynamicXyz", options =>
            {
                options.Realm = "Axon API";
                options.AllowAnonymous = false; // Require authentication by default
            });
        
        // Set Dynamic.xyz as the default authentication scheme
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        
        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Add health checks for Dynamic.xyz services  
        services.AddHealthChecks()
            .AddCheck<DynamicJwksHealthCheck>(
                name: "dynamic-jwks",
                tags: new[] { "dynamic", "external", "auth" });
        
        // Register validators
        RegisterValidators();
    }

    private static void RegisterValidators()
    {
        // Validators are auto-registered by FastEndpoints and AddValidatorsFromAssembly
        // But we can explicitly register if needed for special cases
    }
}