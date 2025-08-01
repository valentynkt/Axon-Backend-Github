using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Observability;

/// <summary>
/// Rule to validate tracing and correlation ID patterns for distributed system observability.
/// </summary>
public sealed class TracingRule : ArchitectureRuleBase
{
    public override string RuleId => "OBS003";
    public override string Name => "Tracing Patterns";
    public override string Description => "Validates that tracing and correlation ID patterns are properly implemented for distributed tracing";
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
                // Check for correlation ID handling in API controllers
                ValidateCorrelationIdHandling(type, violations);
                
                // Check for distributed tracing in service calls
                ValidateDistributedTracing(type, violations);
                
                // Check for activity/span creation in business operations
                ValidateActivityTracking(type, violations);
                
                // Check for proper trace context propagation
                ValidateTraceContextPropagation(type, violations);
                
                // Check for trace sampling strategies
                ValidateTraceSampling(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private void ValidateCorrelationIdHandling(Type type, List<RuleViolation> violations)
    {
        if (IsApiController(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var httpMethods = methods.Where(IsHttpEndpoint).ToList();
            
            if (httpMethods.Any())
            {
                var hasCorrelationIdHandling = HasCorrelationIdSupport(type);
                
                if (!hasCorrelationIdHandling)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"API Controller '{type.Name}' should implement correlation ID handling for request tracing",
                        "Add correlation ID extraction from headers and propagation to downstream services"));
                }
            }
        }
    }

    private void ValidateDistributedTracing(Type type, List<RuleViolation> violations)
    {
        if (IsServiceType(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var externalCallMethods = methods.Where(MakesExternalCalls).ToList();
            
            if (externalCallMethods.Any())
            {
                var hasTracingImplementation = HasTracingImplementation(type);
                
                if (!hasTracingImplementation)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Service '{type.Name}' makes external calls but doesn't implement distributed tracing",
                        "Add Activity/Span creation and trace context propagation for external service calls"));
                }
            }
        }
    }

    private void ValidateActivityTracking(Type type, List<RuleViolation> violations)
    {
        if (IsBusinessLogicType(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var businessMethods = methods.Where(IsBusinessCriticalMethod).ToList();
            
            if (businessMethods.Any())
            {
                var hasActivityTracking = HasActivityTracking(type);
                
                if (!hasActivityTracking && businessMethods.Count > 3)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Business logic type '{type.Name}' should implement activity tracking for important operations",
                        "Add Activity creation for business-critical operations to enable end-to-end tracing"));
                }
            }
        }
    }

    private void ValidateTraceContextPropagation(Type type, List<RuleViolation> violations)
    {
        if (IsMiddlewareOrHandler(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var processingMethods = methods.Where(m => 
                m.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Invoke", StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (processingMethods.Any())
            {
                var hasTraceContextHandling = HasTraceContextHandling(type);
                
                if (!hasTraceContextHandling)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Middleware/Handler '{type.Name}' should properly handle trace context propagation",
                        "Ensure trace context is properly extracted, used, and propagated through the request pipeline"));
                }
            }
        }
    }

    private void ValidateTraceSampling(Type type, List<RuleViolation> violations)
    {
        // Check for trace configuration or sampling logic
        if (IsConfigurationType(type) || IsStartupType(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var configMethods = methods.Where(m => 
                m.Name.Contains("Configure", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (configMethods.Any())
            {
                var hasTracingConfiguration = HasTracingConfiguration(type);
                
                if (!hasTracingConfiguration)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Configuration type '{type.Name}' should include tracing setup and sampling configuration",
                        "Configure distributed tracing with appropriate sampling rates for production environments"));
                }
            }
        }
    }

    private static bool IsApiController(Type type)
    {
        return type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               type.GetCustomAttributes().Any(attr => 
                   attr.GetType().Name.Contains("Controller") ||
                   attr.GetType().Name.Contains("ApiController"));
    }

    private static bool IsServiceType(Type type)
    {
        return type.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Gateway", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Proxy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBusinessLogicType(Type type)
    {
        return type.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Query", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace?.Contains("Application", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.Contains("Domain", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsMiddlewareOrHandler(Type type)
    {
        return type.Name.Contains("Middleware", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Filter", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("Handler"));
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

    private static bool IsHttpEndpoint(MethodInfo method)
    {
        return method.GetCustomAttributes().Any(attr => 
            attr.GetType().Name.Contains("HttpGet") ||
            attr.GetType().Name.Contains("HttpPost") ||
            attr.GetType().Name.Contains("HttpPut") ||
            attr.GetType().Name.Contains("HttpDelete") ||
            attr.GetType().Name.Contains("Route"));
    }

    private static bool MakesExternalCalls(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Any(p => 
            p.ParameterType.Name.Contains("HttpClient") ||
            p.ParameterType.Name.Contains("Client") ||
            p.ParameterType.Name.Contains("Gateway") ||
            p.ParameterType.Name.Contains("Proxy")) ||
            method.Name.Contains("Call", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Send", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Request", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBusinessCriticalMethod(MethodInfo method)
    {
        return method.Name.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCorrelationIdSupport(Type type)
    {
        var constructors = type.GetConstructors();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var properties = type.GetProperties();
        
        // Check for correlation ID related dependencies, fields, or properties
        return constructors.Any(c => c.GetParameters().Any(p => 
                p.Name?.Contains("correlation", StringComparison.OrdinalIgnoreCase) == true ||
                p.Name?.Contains("trace", StringComparison.OrdinalIgnoreCase) == true ||
                p.ParameterType.Name.Contains("CorrelationId") ||
                p.ParameterType.Name.Contains("TraceId"))) ||
               fields.Any(f => f.Name.Contains("correlation", StringComparison.OrdinalIgnoreCase) ||
                              f.Name.Contains("trace", StringComparison.OrdinalIgnoreCase)) ||
               properties.Any(p => p.Name.Contains("correlation", StringComparison.OrdinalIgnoreCase) ||
                                  p.Name.Contains("trace", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasTracingImplementation(Type type)
    {
        var constructors = type.GetConstructors();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        return constructors.Any(c => c.GetParameters().Any(p => 
                p.ParameterType.Name.Contains("Activity") ||
                p.ParameterType.Name.Contains("Tracer") ||
                p.ParameterType.Name.Contains("Telemetry") ||
                p.ParameterType.Name.Contains("Tracing"))) ||
               fields.Any(f => f.FieldType.Name.Contains("Activity") ||
                              f.FieldType.Name.Contains("Tracer") ||
                              f.FieldType.Name.Contains("Telemetry"));
    }

    private static bool HasActivityTracking(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        return methods.Any(m => m.Name.Contains("Activity", StringComparison.OrdinalIgnoreCase) ||
                               m.Name.Contains("Trace", StringComparison.OrdinalIgnoreCase)) ||
               fields.Any(f => f.FieldType.Name.Contains("Activity") ||
                              f.Name.Contains("activity", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasTraceContextHandling(Type type)
    {
        var parameters = type.GetMethods()
            .SelectMany(m => m.GetParameters())
            .ToList();
        
        return parameters.Any(p => 
            p.ParameterType.Name.Contains("TraceContext") ||
            p.ParameterType.Name.Contains("ActivityContext") ||
            p.Name?.Contains("context", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HasTracingConfiguration(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        return methods.Any(m => 
            m.Name.Contains("Tracing", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Activity", StringComparison.OrdinalIgnoreCase) ||
            m.GetParameters().Any(p => 
                p.ParameterType.Name.Contains("Tracing") ||
                p.ParameterType.Name.Contains("Telemetry") ||
                p.ParameterType.Name.Contains("Activity")));
    }
}