using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Infrastructure.Configuration;
using Axon.Modules.Chat.Infrastructure.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.Interceptors;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using Axon.Modules.Chat.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Extensions;

/// <summary>
/// Service collection extensions for Chat Infrastructure layer following SPARC patterns
/// Registers all infrastructure services with proper dependency injection configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Chat Infrastructure services with SPARC Data Access Architecture
    /// </summary>
    public static IServiceCollection AddChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use SPARC-compliant data access registration
        services.AddChatDataAccess(configuration);

        // Register Chat Infrastructure services (AI client, MCP servers, etc.)
        services.AddChatApplicationServices(configuration);

        return services;
    }


    /// <summary>
    /// Ensures database is created and migrations are applied
    /// Should be called during application startup
    /// </summary>
    public static async Task EnsureChatDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ChatDbContext>>();

        try
        {
            // Ensure database exists
            await context.Database.EnsureCreatedAsync();
            
            // Apply any pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure Chat database");
            throw;
        }
    }
}