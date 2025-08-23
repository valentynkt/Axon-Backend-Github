using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DependencyInjection;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Infrastructure.Configuration;
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Core.Abstractions.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace Axon.Api.Configuration;

/// <summary>
/// Service to validate all critical DI registrations across all architectural layers
/// </summary>
public static class DIValidationService
{
    /// <summary>
    /// Validates that all critical services can be resolved from the DI container across all layers
    /// </summary>
    /// <param name="serviceProvider">The configured service provider</param>
    /// <returns>List of validation errors, if any</returns>
    public static List<string> ValidateServiceRegistrations(IServiceProvider serviceProvider)
    {
        var errors = new List<string>();

        try
        {
            // Validate BuildingBlocks layer (foundation services)
            var buildingBlocksErrors = BuildingBlocksDIValidation.ValidateBuildingBlocksServices(serviceProvider);
            errors.AddRange(buildingBlocksErrors);
            
            // Validate Chat Application layer services
            var applicationErrors = ApplicationDIValidation.ValidateApplicationServices(serviceProvider);
            errors.AddRange(applicationErrors);
            
            // Validate Chat Infrastructure layer services  
            var infrastructureErrors = InfrastructureDIValidation.ValidateInfrastructureServices(serviceProvider);
            errors.AddRange(infrastructureErrors);
            
            // API layer specific validations
            ValidateApiLayerServices(serviceProvider, errors);
            
            // Cross-layer integration validations
            ValidateCrossLayerIntegrations(serviceProvider, errors);
            
        }
        catch (Exception ex)
        {
            errors.Add($"CRITICAL: DI validation failed with exception: {ex.Message}");
        }

        return errors;
    }

    private static void ValidateApiLayerServices(IServiceProvider serviceProvider, List<string> errors)
    {
        try
        {
            // Test API-specific services that aren't covered by other layers
            TestServiceResolution<Microsoft.AspNetCore.Http.IHttpContextAccessor>(serviceProvider, errors, "IHttpContextAccessor - HTTP context access");
            
            // Health check services
            TestServiceResolution<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>(serviceProvider, errors, "HealthCheckService - Health monitoring");
            
            // API documentation services  
            TestServiceResolution<Microsoft.AspNetCore.Mvc.ApiExplorer.IApiDescriptionGroupCollectionProvider>(serviceProvider, errors, "IApiDescriptionGroupCollectionProvider - API documentation");
            
        }
        catch (Exception ex)
        {
            errors.Add($"API: Failed to validate API layer services: {ex.Message}");
        }
    }

    private static void ValidateCrossLayerIntegrations(IServiceProvider serviceProvider, List<string> errors)
    {
        try
        {
            // Validate that critical cross-layer dependencies work together
            
            // Test that IMcpServerResolver resolves to the cached decorator (critical for performance)
            var mcpResolver = serviceProvider.GetService<IMcpServerResolver>();
            if (mcpResolver != null && mcpResolver.GetType().Name != "CachedMcpConfigurationService")
            {
                errors.Add($"CROSS-LAYER: IMcpServerResolver resolves to {mcpResolver.GetType().Name} instead of CachedMcpConfigurationService. Performance optimization not active!");
            }
            
            // Validate MediatR and FluentValidation integration
            var mediator = serviceProvider.GetService<MediatR.IMediator>();
            var validatorFactory = serviceProvider.GetService<FluentValidation.IValidatorFactory>();
            if (mediator != null && validatorFactory == null)
            {
                errors.Add("CROSS-LAYER: MediatR is registered but FluentValidation IValidatorFactory is missing - validation pipeline will not work");
            }
            
        }
        catch (Exception ex)
        {
            errors.Add($"CROSS-LAYER: Failed to validate cross-layer integrations: {ex.Message}");
        }
    }

    private static void TestServiceResolution<T>(IServiceProvider serviceProvider, List<string> errors, string serviceName)
        where T : class
    {
        try
        {
            var service = serviceProvider.GetService<T>();
            if (service == null)
            {
                errors.Add($"API: {serviceName} ({typeof(T).Name}) could not be resolved from DI container");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"API: {serviceName} ({typeof(T).Name}) failed to resolve: {ex.Message}");
        }
    }
}