using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Axon.Api.Configuration;

/// <summary>
/// Dependency validation for integration coordination
/// Ensures all agents' services are properly registered without conflicts
/// </summary>
public static class DependencyValidation
{
    /// <summary>
    /// Validate that all required services are registered
    /// </summary>
    /// <param name="services">Service collection to validate</param>
    /// <returns>Validation results</returns>
 //public static ValidationResult ValidateServiceRegistrations(IServiceCollection services)
 //{
 //    var errors = new List<string>();
 //    var warnings = new List<string>();

 //    // Validate SRP decomposition services
///
 //    //// Validate performance optimization services
 //    //ValidateServiceExists<Axon.Modules.Chat.Application.Contracts.IMessageCache>(services, errors, "performance-optimizer");
///
 //    //// Validate clean architecture services
 //    //ValidateServiceExists<Axon.Modules.Chat.Application.Contracts.IMessageProcessor>(services, errors, "clean-architecture-enforcer");
///
 //    //// Validate monitoring services
 //    //ValidateServiceExists<Axon.Modules.Chat.Application.Contracts.IPerformanceMetrics>(services, errors, "monitoring-specialist");
///
 //    //// Check for circular dependencies
 //    //var circularDeps = DetectCircularDependencies(services);
 //    //errors.AddRange(circularDeps);

 //    return new ValidationResult(errors, warnings);
 //}

    private static void ValidateServiceExists<T>(IServiceCollection services, List<string> errors, string agentName)
    {
        var serviceType = typeof(T);
        var hasService = services.Any(s => s.ServiceType == serviceType);
        
        if (!hasService)
        {
            errors.Add($"Missing service registration for {serviceType.Name} from {agentName}");
        }
    }

    private static List<string> DetectCircularDependencies(IServiceCollection services)
    {
        var errors = new List<string>();
        
        // Simplified circular dependency detection
        // In a full implementation, this would build a dependency graph
        // and detect cycles using graph algorithms
        
        var serviceTypes = services.Select(s => s.ServiceType).Distinct().ToList();
        
        foreach (var serviceType in serviceTypes)
        {
            var service = services.FirstOrDefault(s => s.ServiceType == serviceType);
            if (service?.ImplementationType != null)
            {
                var constructors = service.ImplementationType.GetConstructors();
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    foreach (var parameter in parameters)
                    {
                        if (parameter.ParameterType == serviceType)
                        {
                            errors.Add($"Potential circular dependency detected in {serviceType.Name}");
                        }
                    }
                }
            }
        }

        return errors;
    }

    public record ValidationResult(List<string> Errors, List<string> Warnings)
    {
        public bool IsValid => Errors.Count == 0;
    }
}