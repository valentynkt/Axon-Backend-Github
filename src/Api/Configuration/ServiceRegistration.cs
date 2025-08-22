// using Axon.Api.Common.ErrorHandling;  // Not needed for POC
using Axon.Api.Mappers;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Infrastructure.Configuration;
// using Axon.Modules.Chat.Infrastructure.Extensions;  // Not needed for POC
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Web.Builders;
using BuildingBlocks.Web.Mappers;
using BuildingBlocks.Web.OpenApi;
using FastEndpoints;
using FastEndpoints.Swagger;
using FluentValidation;
using MediatR;
using System.Reflection;

namespace Axon.Api.Configuration;

/// <summary>
/// Service registration for API layer
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Register application services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Add controllers (MVC)
        services.AddControllers();
        
        // Add FastEndpoints
        services.AddFastEndpoints();
        
        // Add API documentation using BuildingBlocks OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddAspnetOpenApi();
        
        // Add MediatR
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(Modules.Chat.Application.Commands.StartConversation.StartConversationCommand).Assembly);
        });
        
        // Register pipeline behaviors
        services.AddPipelineBehaviors(configuration, environment);
        
        // Add FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(Modules.Chat.Application.Commands.StartConversation.StartConversationValidator).Assembly);
        
        // Add BuildingBlocks Web services
        services.AddScoped<IEndpointResponseBuilder, EndpointResponseBuilder>();
        
        // Add Chat-specific services
        services.AddScoped<IChatCommandDispatcher, ChatCommandDispatcher>();
        services.AddScoped<IRequestMapper<Axon.Api.Contracts.Chat.ChatTurnRequestDto, Axon.Api.Contracts.Chat.ProcessMessageResponse>, ChatTurnRequestMapper>();
        services.AddScoped<IResponseMapper<Axon.Api.Contracts.Chat.ProcessMessageResponse, Axon.Api.Contracts.Chat.ProcessMessageResponse>, ChatTurnResponseMapper>();
        
        // Add Chat module services - manually add what we need for POC
        services.AddChatApplicationServices(configuration);
        
        return services;
    }
}