using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Shared utilities and helpers for DI validation across all layers
/// </summary>
public static class DIValidationHelpers
{
    /// <summary>
    /// Generic method to test service resolution with consistent error formatting
    /// </summary>
    /// <typeparam name="T">Service type to resolve</typeparam>
    /// <param name="serviceProvider">DI service provider</param>
    /// <param name="errors">Error list to append to</param>
    /// <param name="serviceName">Descriptive name for error messages</param>
    /// <param name="layerPrefix">Layer prefix for categorization (e.g., "API", "APPLICATION")</param>
    public static void TestServiceResolution<T>(IServiceProvider serviceProvider, List<string> errors, string serviceName, string layerPrefix = "GENERIC")
        where T : class
    {
        ArgumentNullException.ThrowIfNull(errors);

        try
        {
            var service = serviceProvider.GetService<T>();
            if (service == null)
            {
                errors.Add($"{layerPrefix}: {serviceName} ({typeof(T).Name}) could not be resolved from DI container");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{layerPrefix}: {serviceName} ({typeof(T).Name}) failed to resolve: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that a service resolves to a specific implementation type (useful for decorator patterns)
    /// </summary>
    /// <typeparam name="TInterface">Interface type to resolve</typeparam>
    /// <param name="serviceProvider">DI service provider</param>
    /// <param name="errors">Error list to append to</param>
    /// <param name="expectedImplementationName">Expected concrete type name</param>
    /// <param name="serviceName">Descriptive name for error messages</param>
    /// <param name="layerPrefix">Layer prefix for categorization</param>
    public static void ValidateImplementationType<TInterface>(
        IServiceProvider serviceProvider,
        List<string> errors,
        string expectedImplementationName,
        string serviceName,
        string layerPrefix = "GENERIC")
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(errors);

        try
        {
            var service = serviceProvider.GetService<TInterface>();
            if (service == null)
            {
                errors.Add($"{layerPrefix}: {serviceName} could not be resolved to validate implementation type");
                return;
            }

            var actualTypeName = service.GetType().Name;
            if (actualTypeName != expectedImplementationName)
            {
                errors.Add($"{layerPrefix}: {serviceName} resolves to {actualTypeName} instead of expected {expectedImplementationName}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{layerPrefix}: Failed to validate implementation type for {serviceName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that service has the expected lifetime (Singleton, Scoped, Transient)
    /// </summary>
    /// <param name="serviceProvider">DI service provider</param>
    /// <param name="serviceType">Service type to check</param>
    /// <param name="expectedLifetime">Expected service lifetime</param>
    /// <param name="errors">Error list to append to</param>
    /// <param name="layerPrefix">Layer prefix for categorization</param>
    public static void ValidateServiceLifetime(
        IServiceProvider serviceProvider,
        Type serviceType,
        ServiceLifetime expectedLifetime,
        List<string> errors,
        string layerPrefix = "GENERIC")
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(errors);

        try
        {
            if (serviceProvider is IServiceCollection services)
            {
                var serviceDescriptor = services.FirstOrDefault(s => s.ServiceType == serviceType);
                if (serviceDescriptor != null && serviceDescriptor.Lifetime != expectedLifetime)
                {
                    errors.Add($"{layerPrefix}: Service {serviceType.Name} has lifetime {serviceDescriptor.Lifetime} but expected {expectedLifetime}");
                }
            }
            // Note: This method has limitations as IServiceProvider doesn't expose registration metadata
            // Consider using IServiceCollection directly in registration methods for full validation
        }
        catch (Exception ex)
        {
            errors.Add($"{layerPrefix}: Failed to validate service lifetime for {serviceType.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that all types implementing a generic interface are registered
    /// </summary>
    /// <param name="serviceProvider">DI service provider</param>
    /// <param name="genericInterfaceType">Generic interface type (e.g., typeof(IHandler&lt;,&gt;))</param>
    /// <param name="assembly">Assembly to scan for implementations</param>
    /// <param name="errors">Error list to append to</param>
    /// <param name="layerPrefix">Layer prefix for categorization</param>
    public static void ValidateGenericInterfaceImplementations(
        IServiceProvider serviceProvider,
        Type genericInterfaceType,
        Assembly assembly,
        List<string> errors,
        string layerPrefix = "GENERIC")
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(genericInterfaceType);
        ArgumentNullException.ThrowIfNull(errors);

        try
        {
            // Find all concrete types that implement the generic interface
            var implementationTypes = assembly.GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Where(type => type.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceType))
                .ToList();

            foreach (var implementationType in implementationTypes)
            {
                try
                {
                    var service = serviceProvider.GetService(implementationType);
                    if (service == null)
                    {
                        errors.Add($"{layerPrefix}: Implementation {implementationType.Name} for {genericInterfaceType.Name} is not registered");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{layerPrefix}: Failed to resolve {implementationType.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{layerPrefix}: Failed to validate generic interface implementations for {genericInterfaceType.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that required services are registered in proper dependency order
    /// </summary>
    /// <param name="serviceProvider">DI service provider</param>
    /// <param name="dependencyChain">List of service types in dependency order (first depends on second, etc.)</param>
    /// <param name="errors">Error list to append to</param>
    /// <param name="layerPrefix">Layer prefix for categorization</param>
    public static void ValidateDependencyChain(
        IServiceProvider serviceProvider,
        List<Type> dependencyChain,
        List<string> errors,
        string layerPrefix = "GENERIC")
    {
        ArgumentNullException.ThrowIfNull(dependencyChain);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(errors);

        for (int i = 0; i < dependencyChain.Count; i++)
        {
            var serviceType = dependencyChain[i];
            try
            {
                var service = serviceProvider.GetService(serviceType);
                if (service == null)
                {
                    errors.Add($"{layerPrefix}: Dependency chain broken at {serviceType.Name} (position {i + 1})");
                    break; // No point continuing if chain is broken
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{layerPrefix}: Dependency chain validation failed at {serviceType.Name}: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Checks for potential circular dependencies by attempting to create service instances
    /// </summary>
    /// <param name="serviceProvider">DI service provider</param>
    /// <param name="serviceTypes">Service types to check for circular dependencies</param>
    /// <param name="errors">Error list to append to</param>
    /// <param name="layerPrefix">Layer prefix for categorization</param>
    public static void DetectCircularDependencies(
        IServiceProvider serviceProvider,
        IEnumerable<Type> serviceTypes,
        List<string> errors,
        string layerPrefix = "GENERIC")
    {
        ArgumentNullException.ThrowIfNull(serviceTypes);
        ArgumentNullException.ThrowIfNull(errors);

        foreach (var serviceType in serviceTypes)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetService(serviceType);
                // If we can create it without exception, no circular dependency
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("circular dependency", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{layerPrefix}: Circular dependency detected for {serviceType.Name}: {ex.Message}");
            }
            catch (Exception)
            {
                // Other exceptions are not circular dependency issues
            }
        }
    }

    /// <summary>
    /// Validates that a collection of services are all registered (useful for pipeline behaviors, etc.)
    /// </summary>
    /// <param name="serviceProvider">DI service provider</param>
    /// <param name="collectionServiceType">The collection service type (e.g., typeof(IPipelineBehavior&lt;,&gt;))</param>
    /// <param name="expectedCount">Expected minimum number of implementations</param>
    /// <param name="errors">Error list to append to</param>
    /// <param name="serviceName">Descriptive name for the collection</param>
    /// <param name="layerPrefix">Layer prefix for categorization</param>
    public static void ValidateServiceCollection(
        IServiceProvider serviceProvider,
        Type collectionServiceType,
        int expectedCount,
        List<string> errors,
        string serviceName,
        string layerPrefix = "GENERIC")
    {
        ArgumentNullException.ThrowIfNull(errors);

        try
        {
            var services = serviceProvider.GetServices(collectionServiceType);
            var actualCount = services.Count();
            
            if (actualCount < expectedCount)
            {
                errors.Add($"{layerPrefix}: {serviceName} has {actualCount} registrations but expected at least {expectedCount}");
            }
            else if (actualCount == 0)
            {
                errors.Add($"{layerPrefix}: {serviceName} has no registrations - collection is empty");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{layerPrefix}: Failed to validate service collection {serviceName}: {ex.Message}");
        }
    }
}