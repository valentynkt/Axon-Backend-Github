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
}