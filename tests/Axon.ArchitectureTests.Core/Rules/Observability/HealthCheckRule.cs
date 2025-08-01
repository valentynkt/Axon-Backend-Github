using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Observability;

/// <summary>
/// Rule to validate health check implementation for system monitoring and availability validation.
/// </summary>
public sealed class HealthCheckRule : ArchitectureRuleBase
{
    public override string RuleId => "OBS004";
    public override string Name => "Health Check Implementation";
    public override string Description => "Validates that health checks are properly implemented for system monitoring and availability";
    public override string Category => "Observability";
    public override RuleSeverity Severity => RuleSeverity.Warning;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var type in context.Types.Where(t => !t.IsAbstract && !t.IsInterface))
            {
                // Check for health check implementations
                ValidateHealthCheckImplementation(type, violations);
                
                // Check for dependency health checks
                ValidateDependencyHealthChecks(type, violations);
                
                // Check for health check registration
                ValidateHealthCheckRegistration(type, violations);
                
                // Check for readiness and liveness probes
                ValidateReadinessAndLivenessProbes(type, violations);
                
                // Check for health check endpoints
                ValidateHealthCheckEndpoints(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private void ValidateHealthCheckImplementation(Type type, List<RuleViolation> violations)
    {
        if (IsHealthCheckType(type))
        {
            var hasProperImplementation = HasProperHealthCheckImplementation(type);
            
            if (!hasProperImplementation)
            {
                violations.Add(CreateViolation(
                    type,
                    $"Health check '{type.Name}' should implement proper health check interface and patterns",
                    "Implement IHealthCheck interface or proper health check base class"));
            }
            
            // Check for proper error handling in health checks
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var checkMethods = methods.Where(m => 
                m.Name.Contains("Check", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Health", StringComparison.OrdinalIgnoreCase)).ToList();
            
            foreach (var method in checkMethods)
            {
                if (!HasProperErrorHandling(method))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Health check method '{method.Name}' should have proper error handling to avoid health check failures",
                        "Add try-catch blocks and return appropriate health status (Healthy/Degraded/Unhealthy)"));
                }
            }
        }
    }

    private void ValidateDependencyHealthChecks(Type type, List<RuleViolation> violations)
    {
        if (IsExternalDependencyType(type))
        {
            var hasHealthCheck = HasAssociatedHealthCheck(type);
            
            if (!hasHealthCheck)
            {
                violations.Add(CreateViolation(
                    type,
                    $"External dependency '{type.Name}' should have an associated health check implementation",
                    "Create a health check to monitor the availability and health of this external dependency"));
            }
        }
    }

    private void ValidateHealthCheckRegistration(Type type, List<RuleViolation> violations)
    {
        if (IsConfigurationType(type) || IsStartupType(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var configMethods = methods.Where(m => 
                m.Name.Contains("Configure", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("AddServices", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("RegisterServices", StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (configMethods.Any())
            {
                var hasHealthCheckRegistration = HasHealthCheckRegistration(type);
                
                if (!hasHealthCheckRegistration)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Configuration type '{type.Name}' should register health checks for monitoring",
                        "Add health check registration using services.AddHealthChecks() and configure appropriate checks"));
                }
            }
        }
    }

    private void ValidateReadinessAndLivenessProbes(Type type, List<RuleViolation> violations)
    {
        if (IsHealthCheckConfigurationType(type))
        {
            var hasReadinessProbe = HasReadinessProbe(type);
            var hasLivenessProbe = HasLivenessProbe(type);
            
            if (!hasReadinessProbe)
            {
                violations.Add(CreateViolation(
                    type,
                    $"Health check configuration '{type.Name}' should implement readiness probes for proper orchestration",
                    "Add readiness health checks to verify the application is ready to receive traffic"));
            }
            
            if (!hasLivenessProbe)
            {
                violations.Add(CreateViolation(
                    type,
                    $"Health check configuration '{type.Name}' should implement liveness probes for proper monitoring",
                    "Add liveness health checks to verify the application is running and responsive"));
            }
        }
    }

    private void ValidateHealthCheckEndpoints(Type type, List<RuleViolation> violations)
    {
        if (IsHealthCheckController(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var healthEndpoints = methods.Where(IsHealthEndpoint).ToList();
            
            if (!healthEndpoints.Any())
            {
                violations.Add(CreateViolation(
                    type,
                    $"Health check controller '{type.Name}' should expose health check endpoints",
                    "Add GET endpoints for /health, /health/ready, and /health/live"));
            }
            else
            {
                // Check for proper response formatting
                foreach (var endpoint in healthEndpoints)
                {
                    if (!HasProperHealthResponseFormat(endpoint))
                    {
                        violations.Add(CreateViolation(
                            type,
                            $"Health endpoint '{endpoint.Name}' should return proper health check response format",
                            "Return structured health status with overall status and individual check results"));
                    }
                }
            }
        }
    }

    private static bool IsHealthCheckType(Type type)
    {
        return type.Name.Contains("HealthCheck", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Health", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("HealthCheck")) ||
               type.GetInterfaces().Any(i => i.Name.Contains("IHealthCheck"));
    }

    private static bool IsExternalDependencyType(Type type)
    {
        return type.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Client", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Service", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Gateway", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Queue", StringComparison.OrdinalIgnoreCase) ||
               (type.Namespace?.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool IsConfigurationType(Type type)
    {
        return type.Name.Contains("Configuration", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Config", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Settings", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStartupType(Type type)
    {
        return type.Name.Contains("Startup", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Program", StringComparison.OrdinalIgnoreCase) ||
               type.GetMethods().Any(m => m.Name.Contains("ConfigureServices", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHealthCheckConfigurationType(Type type)
    {
        return IsConfigurationType(type) || IsStartupType(type) ||
               type.Name.Contains("HealthCheck", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHealthCheckController(Type type)
    {
        return (type.Name.Contains("Health", StringComparison.OrdinalIgnoreCase) &&
                type.Name.Contains("Controller", StringComparison.OrdinalIgnoreCase)) ||
               type.GetMethods().Any(m => IsHealthEndpoint(m));
    }

    private static bool IsHealthEndpoint(MethodInfo method)
    {
        return (method.Name.Contains("Health", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Ready", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Live", StringComparison.OrdinalIgnoreCase)) &&
               method.GetCustomAttributes().Any(attr => 
                   attr.GetType().Name.Contains("HttpGet") ||
                   attr.GetType().Name.Contains("Route"));
    }

    private static bool HasProperHealthCheckImplementation(Type type)
    {
        // Check if implements proper health check interface
        var interfaces = type.GetInterfaces();
        var hasHealthCheckInterface = interfaces.Any(i => i.Name.Contains("HealthCheck"));
        
        // Check if has proper check method
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasCheckMethod = methods.Any(m => 
            m.Name.Contains("Check", StringComparison.OrdinalIgnoreCase) &&
            m.ReturnType.Name.Contains("Task"));
        
        return hasHealthCheckInterface || hasCheckMethod;
    }

    private static bool HasProperErrorHandling(MethodInfo method)
    {
        // Simple heuristic - check if method might have error handling
        // In a real implementation, you might use more sophisticated code analysis
        return method.GetParameters().Length > 0 || 
               method.ReturnType.Name.Contains("Task") ||
               method.ReturnType.Name.Contains("Result");
    }

    private static bool HasAssociatedHealthCheck(Type type)
    {
        // This is a heuristic - in practice, you might maintain a registry
        // or use naming conventions to determine if a health check exists
        var typeName = type.Name;
        var assemblyTypes = type.Assembly.GetTypes();
        
        return assemblyTypes.Any(t => 
            t.Name.Contains($"{typeName}HealthCheck", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains($"{typeName.Replace("Repository", "").Replace("Service", "").Replace("Client", "")}HealthCheck", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasHealthCheckRegistration(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        return methods.Any(m => 
            m.Name.Contains("AddHealthChecks", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("HealthCheck", StringComparison.OrdinalIgnoreCase) ||
            m.GetParameters().Any(p => p.ParameterType.Name.Contains("HealthCheck")));
    }

    private static bool HasReadinessProbe(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        return methods.Any(m => 
            m.Name.Contains("Ready", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Readiness", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasLivenessProbe(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        return methods.Any(m => 
            m.Name.Contains("Live", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Liveness", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Alive", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasProperHealthResponseFormat(MethodInfo method)
    {
        // Check if return type suggests proper health response
        var returnType = method.ReturnType;
        
        return returnType.Name.Contains("HealthCheckResult") ||
               returnType.Name.Contains("HealthReport") ||
               returnType.Name.Contains("HealthStatus") ||
               (returnType.Name.Contains("ActionResult") && 
                method.GetParameters().Any(p => p.ParameterType.Name.Contains("HealthCheck")));
    }
}