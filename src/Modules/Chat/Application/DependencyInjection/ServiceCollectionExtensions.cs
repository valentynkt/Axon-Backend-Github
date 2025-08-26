using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Application.Services.Idempotency;
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Core.Abstractions.Caching;
using BuildingBlocks.Core.Abstractions.Idempotency;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.Contracts.Dispatching;
using Axon.Modules.Chat.Application.Services.Dispatching;
using Axon.Modules.Chat.Application.Services.Orchestration;
using Polly.Utilities;

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

        // Register Chat-specific services
        services.AddSingleton(TimeProvider.System);
        
        // Register command dispatcher (moved from API layer for proper architecture)
        services.AddScoped<IChatCommandDispatcher, ChatCommandDispatcher>();
        
        // Register idempotency key providers
        services.AddTransient<IIdempotencyKeyProvider<AppendUserMessageCommand>, ChatIdempotencyKeyProvider>();
        services.AddTransient<IIdempotencyKeyProvider<StartConversationCommand>, ChatIdempotencyKeyProvider>();
        
        // Register extracted chat services
        services.AddScoped<IMcpServerResolutionService, McpServerResolutionService>();
        services.AddScoped<IAiProcessingService, AiProcessingService>();
        services.AddScoped<IMessageProcessingOrchestrator, MessageProcessingOrchestrator>();
        
        // IAiClient is infrastructure-provided (adapter), register there.

        return services;
    }
}