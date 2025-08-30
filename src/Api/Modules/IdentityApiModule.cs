using Axon.BuildingBlocks.Web.Configuration;
using Axon.Modules.Identity.Infrastructure.Authentication;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Abstractions.Authentication;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
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
        // Register MediatR for Identity Application handlers
        services.AddMediatR(cfg =>
        {
            // Register from the Api assembly (current) for endpoint handlers
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            // Also register from Identity.Application assembly for domain handlers
            cfg.RegisterServicesFromAssembly(typeof(Axon.Modules.Identity.Application.Commands.ProcessWebhook.ProcessWebhookCommand).Assembly);
        });

        // Register Identity Infrastructure services (HTTP clients, external services)
        services.AddDynamicXyzInfrastructure(configuration);
        
        // Register authentication services
        services.AddScoped<IDynamicAuthService, DynamicAuthService>();
        
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
        
        // Register validators
        RegisterValidators();
    }

    private static void RegisterValidators()
    {
        // Validators are auto-registered by FastEndpoints and AddValidatorsFromAssembly
        // But we can explicitly register if needed for special cases
    }
}