using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.Abstractions.Infrastructure.Caching;
using Axon.Modules.Chat.Application.Services.AI.Configuration;
using Axon.Modules.Chat.Application.Services.AI.Processing;
using Axon.Modules.Chat.Application.Services.AI.Tools;
using Axon.Modules.Chat.Application.Services.Infrastructure.Idempotency;
using Axon.Modules.Chat.Domain.Time;
using BuildingBlocks.Application.Configuration;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Axon.Modules.Chat.Application.DependencyInjection;

/// <summary>
/// Extension methods for registering Chat Application layer services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Chat Application layer services
    /// </summary>
    public static IServiceCollection AddChatApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Register MediatR handlers from this assembly
        services.AddMediatR(cfg => 
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register BuildingBlocks pipeline behaviors
        services.AddPipelineBehaviors(configuration, environment);

        // Register Chat-specific services
        services.AddMemoryCache();
        services.AddSingleton<IIdempotencyCache, InMemoryIdempotencyCache>();
        services.AddSingleton<IClock, SystemClock>();

        // Application services (provider-agnostic)
        services.AddScoped<IMessageRequestBuilder, MessageRequestBuilder>();
        services.AddScoped<IResponseMappingService, ResponseMappingService>();
        services.AddSingleton<IToolExecutionService, ToolExecutionService>();
        services.AddScoped<IMcpConfigurationService, McpConfigurationService>();
        // IAiClient is infrastructure-provided (adapter), register there.

        return services;
    }
}