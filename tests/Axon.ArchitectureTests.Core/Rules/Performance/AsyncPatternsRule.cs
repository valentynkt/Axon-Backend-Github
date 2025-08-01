using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Performance;

/// <summary>
/// Rule to validate proper async/await patterns and avoid blocking synchronous calls.
/// </summary>
public class AsyncPatternsRule : ArchitectureRuleBase
{
    public override string RuleId => "PERF001";
    public override string Name => "Async Patterns Rule";
    public override string Description => "Ensures proper async/await patterns are used and blocking calls are avoided";
    public override string Category => "Performance";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();

        foreach (var assembly in context.Assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => IsApplicationType(t))
                .ToList();

            foreach (var type in types)
            {
                await CheckAsyncPatterns(type, violations);
            }
        }

        return violations;
    }

    private static bool IsApplicationType(Type type)
    {
        return !type.IsInterface &&
               !type.IsAbstract &&
               type.IsClass &&
               (type.Name.EndsWith("Service") ||
                type.Name.EndsWith("Controller") ||
                type.Name.EndsWith("Handler") ||
                type.Name.EndsWith("Repository") ||
                type.Name.EndsWith("Client"));
    }

    private async Task CheckAsyncPatterns(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.IsConstructor)
            .ToList();

        foreach (var method in methods)
        {
            await CheckMethodAsyncPatterns(type, method, violations);
        }
    }

    private async Task CheckMethodAsyncPatterns(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Check for blocking calls in async methods
        if (method.ReturnType == typeof(Task) || method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            if (HasBlockingCall(method))
            {
                violations.Add(CreateViolation(type,
                    $"Async method '{method.Name}' contains blocking calls. Use async alternatives instead.",
                    "Use await with async methods like GetAsync(), ReadAsync(), etc."));
            }
        }

        // Check for synchronous I/O operations
        if (HasSynchronousIoOperation(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' uses synchronous I/O operations. Use async alternatives for better performance.",
                "Replace with async I/O methods (ReadAsync, WriteAsync, etc.)"));
        }

        // Check for missing ConfigureAwait(false) in library code
        if (IsLibraryCode(type) && method.ReturnType == typeof(Task) || 
            (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
        {
            if (ShouldUseConfigureAwait(method))
            {
                violations.Add(CreateViolation(type,
                    $"Library method '{method.Name}' should use ConfigureAwait(false) to avoid deadlocks.",
                    "Add .ConfigureAwait(false) to await calls in library code"));
            }
        }

        // Check for async void methods (except event handlers)
        if (method.ReturnType == typeof(void) && method.Name.EndsWith("Async"))
        {
            if (!IsEventHandler(method))
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' returns void but appears to be async. Use Task instead.",
                    "Change method signature to return Task instead of void"));
            }
        }

        // Check for unnecessary async wrappers
        if (IsUnnecessaryAsyncWrapper(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' appears to be an unnecessary async wrapper.",
                "Remove async/await and return Task directly, or add meaningful async work"));
        }

        await Task.CompletedTask;
    }

    private static bool HasBlockingCall(MethodInfo method)
    {
        // This would require IL analysis in real implementation
        // For architecture tests, we check method names and patterns
        var methodName = method.Name.ToLower();
        return methodName.Contains("wait") && !methodName.Contains("await") ||
               methodName.Contains("result") ||
               methodName.Contains("getsynchronously");
    }

    private static bool HasSynchronousIoOperation(MethodInfo method)
    {
        var methodName = method.Name.ToLower();
        return (methodName.Contains("read") || 
                methodName.Contains("write") || 
                methodName.Contains("save") ||
                methodName.Contains("load")) && 
               !methodName.Contains("async") &&
               method.ReturnType != typeof(Task) &&
               !(method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    private static bool IsLibraryCode(Type type)
    {
        return !type.Namespace?.Contains("Controllers") == true &&
               !type.Namespace?.Contains("Web") == true &&
               !type.Namespace?.Contains("Api") == true;
    }

    private static bool ShouldUseConfigureAwait(MethodInfo method)
    {
        // Simplified check - in real implementation would analyze IL
        return method.Name.Contains("Async") || 
               method.ReturnType == typeof(Task) ||
               (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    private static bool IsEventHandler(MethodInfo method)
    {
        return method.GetParameters().Length == 2 &&
               method.GetParameters()[0].ParameterType == typeof(object) &&
               method.GetParameters()[1].ParameterType.Name.Contains("EventArgs");
    }

    private static bool IsUnnecessaryAsyncWrapper(MethodInfo method)
    {
        // Simple heuristic - method is async but very short
        return method.Name.EndsWith("Async") &&
               (method.ReturnType == typeof(Task) || 
                (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))) &&
               method.GetMethodBody()?.GetILAsByteArray()?.Length < 50; // Simplified check
    }
}