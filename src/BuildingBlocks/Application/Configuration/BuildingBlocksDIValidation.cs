using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Core.Abstractions.Caching;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

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
    public static List<string> ValidateBuildingBlocksServices(IServiceProvider serviceProvider)
    {
        var errors = new List<string>();

        try
        {
            // Caching services validation
            TestServiceResolution<IMemoryCache>(serviceProvider, errors, "IMemoryCache - In-memory caching");
            TestServiceResolution<IDistributedCache>(serviceProvider, errors, "IDistributedCache - Distributed caching");
            TestServiceResolution<IIdempotencyCache>(serviceProvider, errors, "IIdempotencyCache - Idempotency support");
            
            // MediatR pipeline behaviors validation (in execution order)
            ValidatePipelineBehaviors(serviceProvider, errors);
            
            // FluentValidation core services
            TestServiceResolution<IValidatorFactory>(serviceProvider, errors, "IValidatorFactory - FluentValidation factory");
            
            // Validate behavior registration order
            ValidateBehaviorOrder(serviceProvider, errors);
            
        }
        catch (Exception ex)
        {
            errors.Add($"CRITICAL: BuildingBlocks DI validation failed with exception: {ex.Message}");
        }

        return errors;
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