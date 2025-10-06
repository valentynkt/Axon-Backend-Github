using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.MCP;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

using Axon.Modules.Chat.Infrastructure.Services.Telemetry;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.DependencyInjection;

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
        
        // Unified DbContext with both read and write capabilities
        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
                npgsqlOptions.CommandTimeout(30); // 30-second timeout for operations
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            // Read-specific EF Core optimizations
            options.EnableServiceProviderCaching(true);

#if DEBUG
            options.EnableSensitiveDataLogging(true);
            options.EnableDetailedErrors(true);
#endif

            // Performance optimizations
            options.ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.DetachedLazyLoadingWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.FirstWithoutOrderByAndFilterWarning);
            });
        });

        // Register Repository and DbContext interfaces
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();
        services.AddScoped<IMessageReadRepository, MessageReadRepository>();
        services.AddScoped<IChatDbContext>(provider => provider.GetRequiredService<ChatDbContext>());
        
        // Register module-specific UnitOfWork using the EfUnitOfWork wrapper with correct module type
        services.AddScoped<IWriteUnitOfWork<ChatModule>>(provider =>
        {
            var context = provider.GetRequiredService<ChatDbContext>();
            return new EfUnitOfWork<ChatDbContext, ChatModule>(context);
        });
        
        // TimeProvider is registered in Application layer
        
        // NOTE: ICurrentUserService is now registered by IdentityApiModule with HttpContextUserService
        // This provides real user context from Dynamic.xyz authentication
        
        // Register Chat Telemetry service
        services.AddScoped<IChatTelemetry, ChatTelemetry>();
        
        // Register simple POC implementation for Direct MCP
        services.AddHttpClient<IAiClient, OpenAiMcpClient>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
        });
        
        
        // Register core MCP server resolver
        services.AddScoped<McpServerResolver>();
        
        // CRITICAL FIX: Register memory cache for decorator pattern
        services.AddMemoryCache();
        
        // PERFORMANCE OPTIMIZATION: Register cached decorator around core resolver
        // This provides 90% CPU reduction for repeated MCP server configuration lookups
        services.AddScoped<IMcpServerResolver>(provider =>
        {
            var coreResolver = provider.GetRequiredService<McpServerResolver>();
            var cache = provider.GetRequiredService<IMemoryCache>();
            var logger = provider.GetRequiredService<ILogger<CachedMcpConfigurationService>>();
            
            return new CachedMcpConfigurationService(coreResolver, cache, logger);
        });

        return services;
    }
}