using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Infrastructure.Ai;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Services;
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
        services.AddScoped<IToolExecutionService, Application.Services.AI.Tools.ToolExecutionService>();
        services.AddScoped<IJsonSerializationService, JsonSerializationService>();
        services.AddScoped<Application.Abstractions.IActivityTracker, Application.Services.ActivityTracker>();
        services.AddScoped<Application.Abstractions.AI.IMessageRequestBuilder, Application.Services.AI.Processing.MessageRequestBuilder>();
        services.AddScoped<Application.Abstractions.AI.IResponseMappingService, Application.Services.AI.Processing.ResponseMappingService>();
        
        // Register extracted SRP-compliant services
        services.AddScoped<Ai.Abstractions.IHttpRequestBuilder, Ai.Services.HttpRequestBuilder>();
        services.AddScoped<Ai.Abstractions.IResponseParser, Ai.Services.ResponseParser>();
        services.AddScoped<Ai.Abstractions.IPayloadSerializer, Ai.Services.PayloadSerializer>();
        services.AddScoped<Ai.Abstractions.IErrorMappingService, Ai.Services.ErrorMappingService>();
        services.AddScoped<Ai.Abstractions.IActivityTracker, Ai.Services.InfrastructureActivityTracker>();
        services.AddScoped<Ai.Abstractions.IToolExecutionExtractor, Ai.Services.ToolExecutionExtractor>();
        
        // Register simple POC implementation for Direct MCP
        services.AddHttpClient<Application.Abstractions.AI.IAiClient, OpenAiMcpClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromSeconds(60); // Longer timeout for POC testing
        });
        
        
        services.AddScoped<IMcpServerResolver, McpServerResolver>();

        return services;
    }
}