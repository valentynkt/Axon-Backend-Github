using System.Reflection;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Application.Configuration;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Modules.Identity.Application.DependencyInjection;

/// <summary>
/// Extension methods for registering Identity Application layer services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Identity Application layer services
    /// </summary>
    public static IServiceCollection AddIdentityApplication(
        this IServiceCollection services)
    {
        // Register MediatR handlers from this assembly
        services.AddMediatR(cfg => 
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register BuildingBlocks pipeline behaviors
        services.AddApplicationServices();

        // Register Identity-specific services
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IWalletAuthorizationService, WalletAuthorizationService>();
        services.AddScoped<IWalletOwnershipService, WalletOwnershipService>();
        services.AddScoped<IWalletResolutionService, WalletResolutionService>();
        services.AddScoped<IDynamicAuthOrchestrator, DynamicAuthOrchestrator>();
        
        // Register new Auth command/query handlers (MediatR will auto-discover them)
        // ExchangeTokenCommandHandler and GetCurrentUserQueryHandler are auto-registered by MediatR
        
        return services;
    }
}