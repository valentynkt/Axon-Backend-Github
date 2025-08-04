using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Infrastructure.Ai;
using Axon.Modules.Chat.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Axon.Modules.Chat.Infrastructure.Configuration;

/// <summary>
/// Service registration for Chat Infrastructure layer
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Register Chat Infrastructure services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddChatApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Configure OpenAI options
        services.Configure<OpenAiOptions>(
            configuration.GetSection(OpenAiOptions.SectionName));

        // Configure MCP servers options
        services.Configure<McpServersOptions>(
            configuration.GetSection(McpServersOptions.SectionName));

        // Register Application services (maintaining clean architecture)
        services.AddScoped<IErrorMappingService, ErrorMappingService>();
        services.AddScoped<IToolExecutionService, ToolExecutionService>();
        services.AddScoped<IJsonSerializationService, JsonSerializationService>();
        services.AddScoped<IActivityTracker, ActivityTracker>();
        services.AddScoped<IMessageRequestBuilder, MessageRequestBuilder>();
        services.AddScoped<IResponseMappingService, ResponseMappingService>();
        
        // Register extracted SRP-compliant services
        services.AddScoped<Ai.Abstractions.IHttpRequestBuilder, Axon.Modules.Chat.Infrastructure.Ai.Services.HttpRequestBuilder>();
        services.AddScoped<Ai.Abstractions.IResponseParser, Axon.Modules.Chat.Infrastructure.Ai.Services.ResponseParser>();
        services.AddScoped<Ai.Abstractions.IPayloadSerializer, Axon.Modules.Chat.Infrastructure.Ai.Services.PayloadSerializer>();
        
        // Register Infrastructure services
        services.AddHttpClient<IAiClient, OpenAiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IMcpServerResolver, McpServerResolver>();

        return services;
    }
}