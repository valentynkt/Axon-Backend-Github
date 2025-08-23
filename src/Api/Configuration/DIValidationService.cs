using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Services;
using BuildingBlocks.Core.Abstractions.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace Axon.Api.Configuration;

/// <summary>
/// Service to validate all critical DI registrations are correct
/// </summary>
public static class DIValidationService
{
    /// <summary>
    /// Validates that all critical services can be resolved from the DI container
    /// </summary>
    /// <param name="serviceProvider">The configured service provider</param>
    /// <returns>List of validation errors, if any</returns>
    public static List<string> ValidateServiceRegistrations(IServiceProvider serviceProvider)
    {
        var errors = new List<string>();

        try
        {
            // Test critical Chat module services
            TestServiceResolution<IChatCommandDispatcher>(serviceProvider, errors, "IChatCommandDispatcher");
            TestServiceResolution<IMcpServerResolver>(serviceProvider, errors, "IMcpServerResolver - Should resolve to CachedMcpConfigurationService");
            TestServiceResolution<ICurrentUserService>(serviceProvider, errors, "ICurrentUserService");
            
            // Test infrastructure services
            TestServiceResolution<IMemoryCache>(serviceProvider, errors, "IMemoryCache - Required for caching decorator");
            
            // Test that IMcpServerResolver resolves to the cached decorator
            var mcpResolver = serviceProvider.GetService<IMcpServerResolver>();
            if (mcpResolver != null && mcpResolver.GetType().Name != "CachedMcpConfigurationService")
            {
                errors.Add($"CRITICAL: IMcpServerResolver resolves to {mcpResolver.GetType().Name} instead of CachedMcpConfigurationService. Performance optimization not active!");
            }
            
        }
        catch (Exception ex)
        {
            errors.Add($"CRITICAL: DI validation failed with exception: {ex.Message}");
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
                errors.Add($"CRITICAL: {serviceName} ({typeof(T).Name}) could not be resolved from DI container");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"CRITICAL: {serviceName} ({typeof(T).Name}) failed to resolve: {ex.Message}");
        }
    }
}