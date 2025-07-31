using Axon.Api.Common.ErrorHandling;
using Axon.Modules.Chat.Infrastructure.Configuration;
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
            config.RegisterServicesFromAssembly(typeof(Axon.Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageCommand).Assembly);
        });
        
        // Add FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(Axon.Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageValidator).Assembly);
        
        // Add error handling services
        services.AddScoped<IErrorMapper, ErrorMapper>();
        
        // Add Chat module services
        services.AddChatInfrastructure(configuration);
        
        return services;
    }
}