using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Core.Abstractions.Caching;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Core.Idempotency;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Validates all critical BuildingBlocks layer DI registrations
/// </summary>

public static class BuildingBlocksDIValidation
{
    /// <summary>
    /// Validates that all BuildingBlocks services can be resolved from the DI container
    /// </summary>
    /// <param name="serviceProvider">The configured service provider</param>
    /// <returns>List of validation errors, if any</returns>
    public static void ValidateBuildingBlocksServices(IServiceProvider serviceProvider)
    {
        var errors = new List<string>();
        
        try
        {
            // Core infrastructure services
            TestServiceResolution<IMediator>(serviceProvider, errors, "IMediator - MediatR command/query dispatcher");

            // FluentValidation services (using IServiceProvider instead of obsolete IValidatorFactory)
            try
            {
                var validatorServices = serviceProvider.GetServices<IValidator>();
                if (!validatorServices.Any())
                {
                    errors.Add("BUILDINGBLOCKS: No FluentValidation validators registered");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"BUILDINGBLOCKS: FluentValidation services failed to resolve: {ex.Message}");
            }
            
            // Validate behavior registration order
            ValidateBehaviorOrder(serviceProvider, errors);
            
        }
        catch (Exception ex)
        {
            errors.Add($"BUILDINGBLOCKS_CRITICAL_ERROR - Exception during validation: {ex.Message}");
        }

        if (errors.Count > 0)
        {
            var message = $"BuildingBlocks DI Validation Failed:\n{string.Join("\n", errors)}";
            throw new InvalidOperationException(message);
        }
    }

    private static void ValidatePipelineBehaviors(IServiceProvider serviceProvider, List<string> errors)
    {
        // Test that all 6 pipeline behaviors can be resolved
        var behaviorTypes = new[]
        {
            typeof(ObservabilityBehavior<,>),
            typeof(RequestValidationBehavior<,>),
            typeof(QueryCachingBehavior<,>),
            typeof(QueryRetryBehavior<,>),
            typeof(IdempotencyBehavior<,>),
            typeof(UnitOfWorkBehavior<,>)
        };

        foreach (var behaviorType in behaviorTypes)
        {
            try
            {
                var pipelineBehaviorType = typeof(IPipelineBehavior<,>);
                var services = serviceProvider.GetServices(pipelineBehaviorType);
                
                // Check if this specific behavior is registered
                var isRegistered = services.Any(s => s?.GetType().GetGenericTypeDefinition() == behaviorType);
                
                if (!isRegistered)
                {
                    errors.Add($"BUILDINGBLOCKS: Pipeline behavior {behaviorType.Name} is not registered");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"BUILDINGBLOCKS: Failed to validate pipeline behavior {behaviorType.Name}: {ex.Message}");
            }
        }
    }

    private static void ValidateBehaviorOrder(IServiceProvider serviceProvider, List<string> errors)
    {
        try
        {
            // Get all registered pipeline behaviors
            var pipelineBehaviorType = typeof(IPipelineBehavior<,>);
            var services = serviceProvider.GetServices(pipelineBehaviorType);
            
            if (!services.Any())
            {
                errors.Add("BUILDINGBLOCKS: No pipeline behaviors registered - MediatR pipeline will not work properly");
                return;
            }

            // Expected order (outer to inner):
            // 1. ObservabilityBehavior - tracing/metrics/logging
            // 2. RequestValidationBehavior - FluentValidation (fail-fast) 
            // 3. QueryCachingBehavior - L1/L2 read-through cache (queries only)
            // 4. QueryRetryBehavior - Polly-native retry (queries w/ [Retryable])
            // 5. IdempotencyBehavior - success-only cache for commands
            // 6. UnitOfWorkBehavior - transactional commit on success (commands)

            var behaviorNames = services.Where(s => s != null).Select(s => s!.GetType().Name).ToList();
            
            // Check for critical behaviors that should always be present
            var criticalBehaviors = new[] { "ObservabilityBehavior", "RequestValidationBehavior", "UnitOfWorkBehavior" };
            
            foreach (var criticalBehavior in criticalBehaviors)
            {
                if (!behaviorNames.Any(name => name.Contains(criticalBehavior, StringComparison.Ordinal)))
                {
                    errors.Add($"BUILDINGBLOCKS: Critical pipeline behavior {criticalBehavior} is missing");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"BUILDINGBLOCKS: Failed to validate behavior order: {ex.Message}");
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075:GetInterfaces",
        Justification = "Type interfaces are needed for CQRS validation, GetInterfaces() is safe here")]
    private static void ValidateCommandQueryInheritance(List<string> errors)
    {
        try
        {
            // Get all loaded assemblies to search for ICommand/IQuery implementations
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && a.FullName != null)
                .Where(a => a.FullName!.Contains("Axon", StringComparison.Ordinal) || a.FullName.Contains("BuildingBlocks", StringComparison.Ordinal))
                .ToList();

            var commandInterfaceType = typeof(ICommand<>);
            var commandVoidInterfaceType = typeof(ICommand);
            var queryInterfaceType = typeof(IQuery<>);
            var requestBaseType = typeof(RequestBase);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetExportedTypes()
                        .Where(t => t is { IsClass: true, IsAbstract: false })
                        .ToList();

                    foreach (var type in types)
                    {
                        // Suppress IL2075 warning for GetInterfaces() call
                        var interfaces = type.GetInterfaces();
                        bool isCommand = interfaces.Any(i => 
                            (i.IsGenericType && i.GetGenericTypeDefinition() == commandInterfaceType) ||
                            i == commandVoidInterfaceType);
                        bool isQuery = interfaces.Any(i => 
                            i.IsGenericType && i.GetGenericTypeDefinition() == queryInterfaceType);

                        if (isCommand || isQuery)
                        {
                            if (!requestBaseType.IsAssignableFrom(type))
                            {
                                var requestType = isCommand ? "Command" : "Query";
                                errors.Add($"ARCHITECTURE: {requestType} {type.FullName} does not inherit from RequestBase. " +
                                          "All commands and queries must inherit from RequestBase to provide IAxonRequest features " +
                                          "(RequestId, RequestedAt, Metadata) required by pipeline behaviors.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log assembly loading issues but don't fail validation
                    errors.Add($"ARCHITECTURE: Could not examine assembly {assembly.FullName} for command/query validation: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"ARCHITECTURE: Failed to validate command/query inheritance: {ex.Message}");
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
                errors.Add($"BUILDINGBLOCKS: {serviceName} ({typeof(T).Name}) could not be resolved from DI container");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"BUILDINGBLOCKS: {serviceName} ({typeof(T).Name}) failed to resolve: {ex.Message}");
        }
    }
}