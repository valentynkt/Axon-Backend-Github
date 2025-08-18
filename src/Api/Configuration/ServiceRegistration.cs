// using Axon.Api.Common.ErrorHandling;  // Not needed for POC
using Axon.Modules.Chat.Infrastructure.Configuration;
// using Axon.Modules.Chat.Infrastructure.Extensions;  // Not needed for POC
using BuildingBlocks.Application.Configuration;
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
        IConfiguration configuration)
    {
        // Add controllers (MVC)
        services.AddControllers();
        
        // Add FastEndpoints
        services.AddFastEndpoints();
        
        // Add API documentation (Swagger for both MVC and FastEndpoints)
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.SwaggerDocument(o =>
        {
            o.DocumentSettings = s =>
            {
                s.DocumentName = "Axon-Backend-API";
                s.Title = "Axon Backend API";
                s.Version = "v1.0";
                s.Description = "Modular monolith API using Clean Architecture + DDD + CQRS";
            };
        });
        
        // Add MediatR
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageCommand).Assembly);
        });
        
        // Register pipeline behaviors - commented out for POC
        // services.AddPipelineBehaviors(configuration, environment);
        
        // Add FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageValidator).Assembly);
        
        
        // Add Chat module services - manually add what we need for POC
        services.AddChatApplicationServices(configuration);
        
        return services;
    }
}