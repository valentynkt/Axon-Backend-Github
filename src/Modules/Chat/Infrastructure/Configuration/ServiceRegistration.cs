using BuildingBlocks.Application;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Persistence;
using Axon.Modules.Chat.Application.Services;

using Axon.Modules.Chat.Infrastructure.Ai;
using Axon.Modules.Chat.Infrastructure.Persistence;
using Axon.Modules.Chat.Infrastructure.Services;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

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
    public static IServiceCollection AddChatInfrastructure(
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
        
        // Register DbContexts
        var connectionString = configuration.GetConnectionString("ChatDb") 
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=axon_chat;Username=postgres;Password=postgres";
        
        // Write DbContext
        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            });
        });
        
        // Read DbContext
        services.AddDbContext<ChatReadDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            });
        });
        
        // Register Repository and DbContext interfaces
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IChatReadDbContext>(provider => provider.GetRequiredService<ChatReadDbContext>());
        services.AddScoped<IChatWriteDbContext>(provider => provider.GetRequiredService<ChatDbContext>());
        
        // Register UnitOfWork using the EfUnitOfWork wrapper with correct module type
        services.AddScoped<IWriteUnitOfWork>(provider => 
        {
            var context = provider.GetRequiredService<ChatDbContext>();
            return new EfUnitOfWork<ChatDbContext, ChatModule>(context);
        });
        
        // TimeProvider is registered in Application layer
        
        // Register CurrentUserService
        services.AddScoped<ICurrentUserService, DefaultCurrentUserService>();
        
        // Register Telemetry service
        services.AddScoped<Application.Abstractions.Telemetry.IAppTelemetry, AppTelemetryService>();
        
        // Register simple POC implementation for Direct MCP
        services.AddHttpClient<Application.Abstractions.AI.IAiClient, OpenAiMcpClient>((serviceProvider, client) =>
        {
            var openAiOptions = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromSeconds(openAiOptions.TimeoutSeconds);
        });
        
        
        services.AddScoped<IMcpServerResolver, McpServerResolver>();

        return services;
    }
}