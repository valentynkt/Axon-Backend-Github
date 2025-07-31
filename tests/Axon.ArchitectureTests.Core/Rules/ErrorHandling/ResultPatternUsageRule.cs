using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.ErrorHandling;

/// <summary>
/// Validates that Result pattern is used correctly instead of exception-based error handling.
/// </summary>
public sealed class ResultPatternUsageRule : PatternComplianceRule
{
    public override string RuleId => "ERR001";
    public override string Name => "Result Pattern Usage Rule";
    public override string Description => "Application and Domain layers should use Result<T> pattern instead of throwing exceptions for business logic failures";
    public override string Category => "ErrorHandling";

    private static readonly string[] BusinessExceptionKeywords = 
    {
        "throw new", "ArgumentException", "ArgumentNullException", "InvalidOperationException", 
        "NotSupportedException", "BusinessException", "DomainException"
    };

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var applicationAndDomainTypes = context.Types
                .Where(t => IsInApplicationOrDomainLayer(t))
                .ToList();

            foreach (var type in applicationAndDomainTypes)
            {
                ValidateMethodReturnTypes(type, violations);
                ValidateExceptionUsage(type, violations);
                ValidateResultHandling(type, violations);
                ValidateResultComposition(type, violations);
                ValidateExceptionBoundaries(type, violations);
                ValidateNullHandling(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsInApplicationOrDomainLayer(Type type) =>
        type.Namespace?.Contains(".Application.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Application", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateMethodReturnTypes(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && ShouldUseResultPattern(m))
            .ToList();

        foreach (var method in methods)
        {
            if (!ReturnsResult(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' should return Result<T> for operations that can fail",
                    $"Change return type of '{method.Name}' to Result<T> or Result"));
            }
        }
    }

    private void ValidateExceptionUsage(Type type, List<RuleViolation> violations)
    {
        // This is a simplified check - in real implementation, you'd analyze IL code or use Roslyn
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        foreach (var method in methods)
        {
            if (IsInApplicationOrDomainLayer(type) && ThrowsBusinessExceptions(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' appears to throw business exceptions - use Result pattern instead",
                    $"Replace exception throwing with Result.Failure() in '{method.Name}'"));
            }
        }
    }

    private void ValidateResultHandling(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            var hasResultParameters = parameters.Any(p => IsResultType(p.ParameterType));
            
            if (hasResultParameters && !ReturnsResult(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' accepts Result parameters but doesn't return Result - ensure proper Result handling",
                    $"Consider returning Result from '{method.Name}' to chain Result operations"));
            }
        }
    }

    private static bool ShouldUseResultPattern(MethodInfo method)
    {
        // Methods that modify state or perform operations that can fail
        var methodName = method.Name.ToLower();
        
        return methodName.StartsWith("create") ||
               methodName.StartsWith("update") ||
               methodName.StartsWith("delete") ||
               methodName.StartsWith("process") ||
               methodName.StartsWith("execute") ||
               methodName.StartsWith("handle") ||
               methodName.StartsWith("validate") ||
               methodName.Contains("save") ||
               methodName.Contains("send") ||
               methodName.Contains("publish");
    }

    private static bool ReturnsResult(MethodInfo method)
    {
        return IsResultType(method.ReturnType) || 
               (method.ReturnType.IsGenericType && 
                method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>) &&
                IsResultType(method.ReturnType.GetGenericArguments()[0]));
    }

    private static bool IsResultType(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            return genericDefinition.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase);
        }
        
        return type.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ThrowsBusinessExceptions(MethodInfo method)
    {
        // Simplified check - in real implementation, analyze method body
        // For now, check if method has parameters that suggest validation
        var parameters = method.GetParameters();
        var methodName = method.Name.ToLower();
        
        return (methodName.Contains("validate") || 
                methodName.Contains("check") || 
                methodName.Contains("ensure") ||
                parameters.Any(p => p.Name?.ToLower().Contains("validation") == true)) &&
               !ReturnsResult(method);
    }

    /// <summary>
    /// Validates that Result types are properly composed and chained.
    /// </summary>
    private void ValidateResultComposition(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        foreach (var method in methods)
        {
            if (ReturnsResult(method))
            {
                var parameters = method.GetParameters();
                var hasMultipleResultParams = parameters.Count(p => IsResultType(p.ParameterType)) > 1;
                
                if (hasMultipleResultParams)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' accepts multiple Result parameters - consider using Result composition patterns",
                        $"Use Result.Combine() or similar patterns to compose multiple Results in '{method.Name}'"));
                }
            }
        }
    }

    /// <summary>
    /// Validates that exceptions are properly handled at infrastructure boundaries.
    /// </summary>
    private void ValidateExceptionBoundaries(Type type, List<RuleViolation> violations)
    {
        // Check if this is an infrastructure service that should wrap exceptions
        if (IsInfrastructureService(type))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
            foreach (var method in methods.Where(m => !m.IsSpecialName))
            {
                if (!ReturnsResult(method) && InteractsWithExternalSystems(method))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Infrastructure method '{method.Name}' should wrap external exceptions in Result pattern",
                        $"Wrap try-catch blocks in '{method.Name}' and return Result.Failure() for exceptions"));
                }
            }
        }
    }

    /// <summary>
    /// Validates that null handling follows Result pattern instead of null checks.
    /// </summary>
    private void ValidateNullHandling(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            
            // Methods with nullable reference parameters should consider Result pattern
            foreach (var param in parameters)
            {
                if (IsNullableReferenceType(param.ParameterType) && !IsResultType(param.ParameterType))
                {
                    var methodName = method.Name.ToLower();
                    if (methodName.Contains("find") || methodName.Contains("get") || methodName.Contains("retrieve"))
                    {
                        violations.Add(CreateViolation(
                            type,
                            $"Method '{method.Name}' with nullable parameter '{param.Name}' should consider Result pattern",
                            $"Consider using Result<T> instead of nullable types in '{method.Name}' for explicit success/failure handling"));
                    }
                }
            }

            // Methods returning nullable reference types should consider Result pattern
            if (IsNullableReferenceType(method.ReturnType) && !IsResultType(method.ReturnType))
            {
                var methodName = method.Name.ToLower();
                if (methodName.Contains("find") || methodName.Contains("get") || methodName.Contains("retrieve"))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' returns nullable type - consider Result<T> for explicit not-found handling",
                        $"Change '{method.Name}' to return Result<T> instead of nullable type"));
                }
            }
        }
    }

    /// <summary>
    /// Checks if a type is an infrastructure service.
    /// </summary>
    private static bool IsInfrastructureService(Type type)
    {
        return type.Namespace?.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true ||
               type.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Gateway", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a method likely interacts with external systems.
    /// </summary>
    private static bool InteractsWithExternalSystems(MethodInfo method)
    {
        var methodName = method.Name.ToLower();
        return methodName.Contains("save") ||
               methodName.Contains("load") ||
               methodName.Contains("send") ||
               methodName.Contains("receive") ||
               methodName.Contains("fetch") ||
               methodName.Contains("call") ||
               methodName.Contains("request") ||
               IsAsyncMethod(method);
    }

    /// <summary>
    /// Checks if a type is a nullable reference type.
    /// </summary>
    private static bool IsNullableReferenceType(Type type)
    {
        return !type.IsValueType && type != typeof(string);
    }

    /// <summary>
    /// Checks if a method is async.
    /// </summary>
    private static new bool IsAsyncMethod(MethodInfo method)
    {
        return method.ReturnType == typeof(Task) || 
               (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }
}