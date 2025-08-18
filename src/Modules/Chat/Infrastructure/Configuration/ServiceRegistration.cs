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