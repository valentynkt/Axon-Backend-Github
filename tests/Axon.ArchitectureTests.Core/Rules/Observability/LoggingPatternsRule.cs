using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Observability;

/// <summary>
/// Rule to validate logging patterns including structured logging, appropriate log levels, and proper error logging.
/// </summary>
public sealed class LoggingPatternsRule : ArchitectureRuleBase
{
    public override string RuleId => "OBS001";
    public override string Name => "Logging Patterns";
    public override string Description => "Validates that logging patterns follow structured logging practices and appropriate log levels";
    public override string Category => "Observability";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var type in context.Types.Where(t => !t.IsAbstract && !t.IsInterface))
            {
                // Check for string interpolation in logging
                ValidateStructuredLogging(type, violations);
                
                // Check for appropriate log levels
                ValidateLogLevels(type, violations);
                
                // Check for error logging patterns
                ValidateErrorLogging(type, violations);
                
                // Check for logging dependency injection
                ValidateLoggingDependencyInjection(type, violations);
                
                // Check for sensitive data in logs
                ValidateSensitiveDataLogging(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private void ValidateStructuredLogging(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // Check for string interpolation in logging calls (should use structured logging)
            var methodBody = method.ToString() ?? "";
            if (HasLoggingCall(method))
            {
                // Look for potential string interpolation patterns
                if (methodBody.Contains("$\"") || methodBody.Contains("string.Format"))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' appears to use string interpolation for logging. Use structured logging instead",
                        "Use logger.LogInformation(\"Message with {Parameter}\", parameterValue) instead of string interpolation"));
                }
            }
        }
    }

    private void ValidateLogLevels(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            if (HasLoggingCall(method))
            {
                // Check for overuse of LogError when LogWarning might be appropriate
                var methodName = method.Name.ToLowerInvariant();
                if (methodName.Contains("validate") || methodName.Contains("check"))
                {
                    // Validation methods should typically use Warning or Information, not Error
                    var parameters = method.GetParameters();
                    if (parameters.Any(p => p.ParameterType.Name.Contains("ILogger")))
                    {
                        violations.Add(CreateViolation(
                            type,
                            $"Validation method '{method.Name}' should consider using appropriate log level (Warning/Info instead of Error for validation failures)",
                            "Use LogWarning for validation failures, LogError only for unexpected exceptions"));
                    }
                }
            }
        }
    }

    private void ValidateErrorLogging(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // Check for try-catch blocks that might not log exceptions properly
            if (method.Name.Contains("Handle") || method.Name.Contains("Process"))
            {
                var parameters = method.GetParameters();
                var hasExceptionParameter = parameters.Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType));
                var hasLoggerParameter = parameters.Any(p => p.ParameterType.Name.Contains("ILogger"));
                
                if (hasExceptionParameter && !hasLoggerParameter)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Exception handling method '{method.Name}' should include logger dependency for proper error observability",
                        "Add ILogger parameter to methods that handle exceptions"));
                }
            }
        }
    }

    private void ValidateLoggingDependencyInjection(Type type, List<RuleViolation> violations)
    {
        // Check if classes that need logging have proper logger injection
        if (IsServiceOrController(type))
        {
            var constructors = type.GetConstructors();
            var hasLoggerDependency = constructors.Any(c => 
                c.GetParameters().Any(p => p.ParameterType.Name.Contains("ILogger")));
            
            var hasLoggingUsage = type.GetMethods().Any(HasLoggingCall);
            
            if (hasLoggingUsage && !hasLoggerDependency)
            {
                violations.Add(CreateViolation(
                    type,
                    $"Service/Controller '{type.Name}' uses logging but doesn't inject ILogger via constructor",
                    "Inject ILogger<T> through constructor dependency injection"));
            }
        }
    }

    private void ValidateSensitiveDataLogging(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties();
        var fields = type.GetFields();
        
        // Check for properties/fields that might contain sensitive data
        var sensitiveNames = new[] { "password", "secret", "key", "token", "credential", "ssn", "social" };
        
        foreach (var property in properties)
        {
            if (sensitiveNames.Any(name => property.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                // Check if this property might be logged (we can't detect at compile time, but can warn)
                violations.Add(CreateViolation(
                    type,
                    $"Property '{property.Name}' appears to contain sensitive data and should not be logged",
                    "Ensure sensitive properties are not included in log messages or use data masking"));
            }
        }
    }

    private static bool HasLoggingCall(MethodInfo method)
    {
        // Simple heuristic - check method name and parameters for logging patterns
        var parameters = method.GetParameters();
        return parameters.Any(p => p.ParameterType.Name.Contains("ILogger")) ||
               method.Name.Contains("Log", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsServiceOrController(Type type)
    {
        return type.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
               type.GetCustomAttributes().Any(attr => 
                   attr.GetType().Name.Contains("Controller") ||
                   attr.GetType().Name.Contains("Service"));
    }
}