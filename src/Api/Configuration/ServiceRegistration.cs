using Axon.Modules.Chat.Infrastructure.Configuration;
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
        // Add controllers
        services.AddControllers();
        
        // Add API documentation
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
        // Add MediatR
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(Axon.Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageCommand).Assembly);
        });
        
        // Add FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(Axon.Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageValidator).Assembly);
        
        // Add Chat module services
        services.AddChatInfrastructure(configuration);
        
        return services;
    }
}