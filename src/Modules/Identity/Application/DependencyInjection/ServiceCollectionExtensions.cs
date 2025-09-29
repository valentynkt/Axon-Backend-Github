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
        // MediatR and pipeline behaviors are now registered centrally in API layer
        // to prevent handler overwriting issues

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register Identity-specific services
        services.AddSingleton(TimeProvider.System);

        // Register auto-revocation service for handling exclusive ownership constraints
        services.AddScoped<IAutoRevocationService, AutoRevocationService>();

        // Register canonical Auth command/query handlers (MediatR will auto-discover them)
        // ExchangeCredentialHandler and GetMyPrincipalHandler are auto-registered by MediatR

        return services;
    }
}