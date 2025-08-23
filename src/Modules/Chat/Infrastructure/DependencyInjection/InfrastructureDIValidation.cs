using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.MCP;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.DependencyInjection;

/// <summary>
/// Validates all critical Chat Infrastructure layer DI registrations
/// </summary>
public static class InfrastructureDIValidation
{
    /// <summary>
    /// Validates that all Chat Infrastructure services can be resolved from the DI container
    /// </summary>
    /// <param name="serviceProvider">The configured service provider</param>
    /// <returns>List of validation errors, if any</returns>
    public static List<string> ValidateInfrastructureServices(IServiceProvider serviceProvider)
    {
        var errors = new List<string>();

        try
        {
            // Database contexts validation
            TestServiceResolution<ChatDbContext>(serviceProvider, errors, "ChatDbContext - Write database context");
            TestServiceResolution<ChatReadDbContext>(serviceProvider, errors, "ChatReadDbContext - Read database context");
            TestServiceResolution<IChatWriteDbContext>(serviceProvider, errors, "IChatWriteDbContext interface");
            TestServiceResolution<IChatReadDbContext>(serviceProvider, errors, "IChatReadDbContext interface");
            
            // Repository validation
            TestServiceResolution<IConversationRepository>(serviceProvider, errors, "IConversationRepository");
            
            // Unit of Work validation
            TestServiceResolution<IWriteUnitOfWork>(serviceProvider, errors, "IWriteUnitOfWork");
            
            // Authentication services
            TestServiceResolution<ICurrentUserService>(serviceProvider, errors, "ICurrentUserService");
            
            // Telemetry services
            TestServiceResolution<IAppTelemetry>(serviceProvider, errors, "IAppTelemetry");
            
            // AI services
            TestServiceResolution<IAiClient>(serviceProvider, errors, "IAiClient");
            
            // MCP services and decorator validation
            TestServiceResolution<IMcpServerResolver>(serviceProvider, errors, "IMcpServerResolver");
            
            // Infrastructure dependencies for decorators
            TestServiceResolution<IMemoryCache>(serviceProvider, errors, "IMemoryCache - Required for caching decorators");
            TestServiceResolution<ILogger<CachedMcpConfigurationService>>(serviceProvider, errors, "ILogger<CachedMcpConfigurationService>");
            
            // Validate decorator pattern implementation
            var mcpResolver = serviceProvider.GetService<IMcpServerResolver>();
            if (mcpResolver != null && mcpResolver.GetType().Name != "CachedMcpConfigurationService")
            {
                errors.Add($"CRITICAL: IMcpServerResolver resolves to {mcpResolver.GetType().Name} instead of CachedMcpConfigurationService. Performance optimization not active!");
            }
            
            // Validate Unit of Work is properly wrapped
            var unitOfWork = serviceProvider.GetService<IWriteUnitOfWork>();
            if (unitOfWork != null && !unitOfWork.GetType().Name.Contains("EfUnitOfWork"))
            {
                errors.Add($"CRITICAL: IWriteUnitOfWork resolves to {unitOfWork.GetType().Name} instead of EfUnitOfWork wrapper. Transaction management not properly configured!");
            }
            
        }
        catch (Exception ex)
        {
            errors.Add($"CRITICAL: Infrastructure DI validation failed with exception: {ex.Message}");
        }

        return errors;
    }

    private static void TestServiceResolution<T>(IServiceProvider serviceProvider, List<string> errors, string serviceName)
        where T : class
    {
        try
        {
            var service = serviceProvider.GetService<T>();
            if (service == null)
            {
                errors.Add($"INFRASTRUCTURE: {serviceName} ({typeof(T).Name}) could not be resolved from DI container");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"INFRASTRUCTURE: {serviceName} ({typeof(T).Name}) failed to resolve: {ex.Message}");
        }
    }
}