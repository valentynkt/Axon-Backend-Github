using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Performance;

/// <summary>
/// Validates advanced performance patterns for scalable applications.
/// </summary>
public sealed class AdvancedPerformanceRule : ArchitectureRuleBase
{
    public override string RuleId => "PERF-ADV-001";
    public override string Name => "Advanced Performance Patterns";
    public override string Description => "Validates advanced performance optimization patterns including lazy loading, connection pooling, and memory efficiency";
    public override string Category => "Performance";
    public override RuleSeverity Severity => RuleSeverity.Warning;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {   
        var violations = new List<RuleViolation>();
        var types = context.Types.ToList();

        // Check for lazy loading patterns
        await ValidateLazyLoadingPatterns(types, violations);
        
        // Check for connection pooling
        await ValidateConnectionPooling(types, violations);
        
        // Check for memory efficiency patterns
        await ValidateMemoryEfficiency(types, violations);
        
        // Check for batch processing patterns
        await ValidateBatchProcessing(types, violations);
        
        return violations;
    }

    private static async Task ValidateLazyLoadingPatterns(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var properties = type.GetProperties();
            
            foreach (var property in properties)
            {
                // Check for collections that should be lazy loaded
                if (property.PropertyType.IsGenericType && 
                    (property.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                     property.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) ||
                     property.PropertyType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    var hasLazyLoading = property.GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("Lazy"));

                    var isNavigationProperty = property.GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("Navigation") ||
                                a.GetType().Name.Contains("Foreign") ||
                                a.GetType().Name.Contains("Related"));

                    if (isNavigationProperty && !hasLazyLoading)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Navigation property '{property.Name}' in '{type.Name}' should implement lazy loading",
                            Severity = RuleSeverity.Warning
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateConnectionPooling(List<Type> types, List<RuleViolation> violations)
    {
        var repositoryTypes = types.Where(t => 
            t.Name.EndsWith("Repository") ||
            t.Name.EndsWith("DataAccess") ||
            t.Name.EndsWith("Context"))
            .ToList();

        foreach (var repoType in repositoryTypes)
        {
            var methods = repoType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
                // Check for database connection creation in methods
                var parameters = method.GetParameters();
                var hasConnectionParameter = parameters.Any(p => 
                    p.ParameterType.Name.Contains("Connection") ||
                    p.ParameterType.Name.Contains("Database"));

                // Check if method creates new connections instead of using DI
                if (method.ReturnType.Name.Contains("Task") && !hasConnectionParameter)
                {
                    var methodName = method.Name.ToLower();
                    if (methodName.Contains("get") || methodName.Contains("find") || 
                        methodName.Contains("save") || methodName.Contains("update"))
                    {
                        // This is a simplified check - ideally would analyze method body
                        var hasPoolingAttribute = method.GetCustomAttributes()
                            .Any(a => a.GetType().Name.Contains("Pool") ||
                                    a.GetType().Name.Contains("Scoped"));

                        if (!hasPoolingAttribute)
                        {
                            violations.Add(new RuleViolation
                            {
                                TypeName = repoType.FullName!,
                                AssemblyName = repoType.Assembly.FullName!,
                                Message = $"Data access method '{method.Name}' in '{repoType.Name}' should use connection pooling",
                                Severity = RuleSeverity.Error
                            });
                        }
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateMemoryEfficiency(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
                // Check for potential memory leaks in event handlers
                if (method.Name.ToLower().Contains("event") || 
                    method.ReturnType.Name.Contains("EventHandler"))
                {
                    var hasDisposalPattern = type.GetInterfaces()
                        .Any(i => i.Name == "IDisposable");

                    if (!hasDisposalPattern)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Type '{type.Name}' with event handlers should implement IDisposable",
                            Severity = RuleSeverity.Error
                        });
                    }
                }

                // Check for large object allocations
                var returnType = method.ReturnType;
                if (returnType.IsArray || 
                    (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    var hasCapacityOptimization = method.GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("Capacity") ||
                                a.GetType().Name.Contains("Size"));

                    if (!hasCapacityOptimization && method.Name.ToLower().Contains("get"))
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Method '{method.Name}' in '{type.Name}' returns collections without capacity optimization",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateBatchProcessing(List<Type> types, List<RuleViolation> violations)
    {
        var serviceTypes = types.Where(t => 
            t.Name.EndsWith("Service") ||
            t.Name.EndsWith("Handler") ||
            t.Name.EndsWith("Processor"))
            .ToList();

        foreach (var serviceType in serviceTypes)
        {
            var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
                // Check for methods that process collections individually
                var parameters = method.GetParameters();
                var hasCollectionParameter = parameters.Any(p => 
                    p.ParameterType.IsGenericType && 
                    (p.ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                     p.ParameterType.GetGenericTypeDefinition() == typeof(IList<>) ||
                     p.ParameterType.GetGenericTypeDefinition() == typeof(List<>)));

                if (hasCollectionParameter)
                {
                    var hasBatchProcessing = method.GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("Batch") ||
                                a.GetType().Name.Contains("Bulk"));

                    var methodName = method.Name.ToLower();
                    if ((methodName.Contains("process") || methodName.Contains("handle") || 
                         methodName.Contains("save") || methodName.Contains("update")) && 
                        !hasBatchProcessing)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = serviceType.FullName!,
                            AssemblyName = serviceType.Assembly.FullName!,
                            Message = $"Method '{method.Name}' in '{serviceType.Name}' processes collections but lacks batch optimization",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }
}

/// <summary>
/// SLA Compliance Rule - Validates that critical operations meet performance SLAs
/// </summary>
public class SlaComplianceRule : ArchitectureRuleBase
{
    public override string RuleId => "PERF006";
    public override string Name => "SLA Compliance Rule";
    public override string Category => "Performance - SLA Compliance";
    public override string Description => "Critical operations must complete within defined SLA timeouts";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(IArchitectureContext context, CancellationToken cancellationToken)
    {
        var criticalHandlers = context.Types
            .Where(t => t.Name.EndsWith("Handler") && IsCriticalHandler(t))
            .ToList();

        var violations = new List<RuleViolation>();
        
        foreach (var handler in criticalHandlers)
        {
            var handleMethod = handler.GetMethods()
                .FirstOrDefault(m => m.Name == "Handle");

            if (handleMethod != null && !HasTimeoutAttribute(handleMethod))
            {
                violations.Add(new RuleViolation
                {
                    TypeName = handler.FullName ?? handler.Name,
                    AssemblyName = handler.Assembly.GetName().Name ?? "Unknown",
                    Message = $"Critical handler {handler.Name} lacks SLA timeout specification",
                    Severity = RuleSeverity.Critical
                });
            }
        }
        
        await Task.CompletedTask;
        return violations;
    }

    private static bool IsCriticalHandler(Type type)
    {
        // ProcessMessage and other critical business operations
        return type.Name.Contains("ProcessMessage") ||
               type.Name.Contains("Authentication") ||
               type.Name.Contains("Authorization");
    }

    private static bool HasTimeoutAttribute(MethodInfo method)
    {
        // Check for timeout or SLA-related attributes
        return method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Timeout") || 
                        attr.GetType().Name.Contains("Sla"));
    }
}

/// <summary>
/// Linear Scaling Validation Rule - Ensures concurrent request handling scales linearly
/// </summary>
public class LinearScalingRule : ArchitectureRuleBase
{
    public override string RuleId => "PERF007";
    public override string Name => "Linear Scaling Rule";
    public override string Category => "Performance - Scalability";
    public override string Description => "Services must scale linearly up to 100 concurrent requests";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(IArchitectureContext context, CancellationToken cancellationToken)
    {
        var serviceTypes = context.Types
            .Where(t => t.Name.EndsWith("Service") || t.Name.EndsWith("Handler"))
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .ToList();

        var violations = new List<RuleViolation>();
        
        foreach (var serviceType in serviceTypes)
        {
            if (HasBlockingOperations(serviceType))
            {
                violations.Add(new RuleViolation
                {
                    TypeName = serviceType.FullName ?? serviceType.Name,
                    AssemblyName = serviceType.Assembly.GetName().Name ?? "Unknown",
                    Message = $"Service {serviceType.Name} contains blocking operations that prevent linear scaling",
                    Severity = RuleSeverity.Error
                });
            }
        }
        
        await Task.CompletedTask;
        return violations;
    }

    private static bool HasBlockingOperations(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        return methods.Any(method =>
        {
            // Check for synchronous database calls
            var hasSync = method.GetParameters()
                .Any(p => p.ParameterType.Name.Contains("DbContext")) &&
                !method.ReturnType.Name.Contains("Task");

            // Check for Thread.Sleep or blocking calls
            var methodBody = method.GetMethodBody();
            // Note: In a real implementation, we'd analyze IL code for blocking calls
            
            return hasSync;
        });
    }
}
