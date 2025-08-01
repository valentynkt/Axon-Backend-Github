using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Performance;

/// <summary>
/// Rule to validate proper caching implementation and strategy patterns.
/// </summary>
public class CachingPatternsRule : ArchitectureRuleBase
{
    public override string RuleId => "PERF002";
    public override string Name => "Caching Patterns Rule";
    public override string Description => "Validates proper caching implementation and strategy patterns";
    public override string Category => "Performance";
    public override RuleSeverity Severity => RuleSeverity.Warning;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();

        foreach (var assembly in context.Assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => IsServiceType(t))
                .ToList();

            foreach (var type in types)
            {
                await CheckCachingPatterns(type, violations);
            }
        }

        return violations;
    }

    private static bool IsServiceType(Type type)
    {
        return !type.IsInterface &&
               !type.IsAbstract &&
               type.IsClass &&
               (type.Name.EndsWith("Service") ||
                type.Name.EndsWith("Repository") ||
                type.Name.EndsWith("Provider") ||
                type.Name.EndsWith("Manager"));
    }

    private async Task CheckCachingPatterns(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.IsConstructor)
            .ToList();

        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .ToList();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .ToList();

        // Check for expensive operations without caching
        foreach (var method in methods)
        {
            await CheckExpensiveOperationsNeedCaching(type, method, violations);
            await CheckCacheImplementation(type, method, violations);
        }

        // Check for proper cache invalidation patterns
        await CheckCacheInvalidationPatterns(type, violations);

        // Check for cache dependencies injection
        await CheckCacheDependencies(type, violations);
    }

    private async Task CheckExpensiveOperationsNeedCaching(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (IsExpensiveOperation(method))
        {
            var hasCache = HasCachingImplementation(type, method);
            if (!hasCache)
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' appears to be an expensive operation but doesn't implement caching.",
                    "Consider implementing caching for expensive operations using IMemoryCache or IDistributedCache"));
            }
        }

        await Task.CompletedTask;
    }

    private async Task CheckCacheImplementation(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (HasCachingImplementation(type, method))
        {
            // Check for proper cache key generation
            if (!HasProperCacheKeyGeneration(method))
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' uses caching but may not have secure cache key generation.",
                    "Use consistent, secure cache key generation to avoid cache pollution"));
            }

            // Check for cache expiration policy
            if (!HasCacheExpirationPolicy(method))
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' uses caching but doesn't specify expiration policy.",
                    "Always specify cache expiration policies to prevent stale data"));
            }

            // Check for cache miss handling
            if (!HasProperCacheMissHandling(method))
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' may not handle cache misses properly.",
                    "Implement proper fallback logic for cache misses"));
            }
        }

        await Task.CompletedTask;
    }

    private async Task CheckCacheInvalidationPatterns(Type type, List<RuleViolation> violations)
    {
        var updateMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => IsUpdateOperation(m))
            .ToList();

        foreach (var method in updateMethods)
        {
            if (!HasCacheInvalidation(method))
            {
                violations.Add(CreateViolation(type,
                    $"Update method '{method.Name}' doesn't invalidate related cache entries.",
                    "Implement cache invalidation for data modification operations"));
            }
        }

        await Task.CompletedTask;
    }

    private async Task CheckCacheDependencies(Type type, List<RuleViolation> violations)
    {
        var constructors = type.GetConstructors();
        var hasMemoryCache = false;
        var hasDistributedCache = false;

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            hasMemoryCache = parameters.Any(p => p.ParameterType.Name.Contains("MemoryCache"));
            hasDistributedCache = parameters.Any(p => p.ParameterType.Name.Contains("DistributedCache"));
        }

        if (RequiresCaching(type) && !hasMemoryCache && !hasDistributedCache)
        {
            violations.Add(CreateViolation(type,
                $"Service '{type.Name}' appears to need caching but doesn't inject IMemoryCache or IDistributedCache.",
                "Inject appropriate caching dependencies via constructor"));
        }

        await Task.CompletedTask;
    }

    private static bool IsExpensiveOperation(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("calculate") ||
               name.Contains("compute") ||
               name.Contains("process") ||
               name.Contains("analyze") ||
               name.Contains("transform") ||
               name.Contains("aggregate") ||
               (name.Contains("get") && (name.Contains("all") || name.Contains("list"))) ||
               method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
               method.GetParameters().Length == 0 && method.ReturnType != typeof(void); // Parameterless getters
    }

    private static bool HasCachingImplementation(Type type, MethodInfo method)
    {
        // Check if type has cache dependencies
        var constructors = type.GetConstructors();
        var hasCacheDependency = constructors.Any(c => 
            c.GetParameters().Any(p => 
                p.ParameterType.Name.Contains("Cache") ||
                p.ParameterType.Name.Contains("IMemoryCache") ||
                p.ParameterType.Name.Contains("IDistributedCache")));

        // Check method name suggests caching
        var methodName = method.Name.ToLower();
        var hasCachingInName = methodName.Contains("cache") || methodName.Contains("cached");

        return hasCacheDependency || hasCachingInName;
    }

    private static bool HasProperCacheKeyGeneration(MethodInfo method)
    {
        // Simplified check - would need IL analysis for complete validation
        var parameters = method.GetParameters();
        return parameters.Length > 0; // Has parameters that can be used for key generation
    }

    private static bool HasCacheExpirationPolicy(MethodInfo method)
    {
        // Simplified check - would examine method body in real implementation
        return method.Name.Contains("Cache") || method.DeclaringType?.Name.Contains("Cache") == true;
    }

    private static bool HasProperCacheMissHandling(MethodInfo method)
    {
        // Simplified check - would analyze method implementation
        return method.ReturnType != typeof(void) && !method.ReturnType.IsValueType;
    }

    private static bool IsUpdateOperation(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.StartsWith("update") ||
               name.StartsWith("create") ||
               name.StartsWith("delete") ||
               name.StartsWith("save") ||
               name.StartsWith("add") ||
               name.StartsWith("remove") ||
               name.StartsWith("modify");
    }

    private static bool HasCacheInvalidation(MethodInfo method)
    {
        // Simplified check - would analyze method body for cache removal calls
        return method.Name.Contains("Cache") || 
               method.DeclaringType?.GetMethods().Any(m => m.Name.Contains("RemoveCache") || m.Name.Contains("InvalidateCache")) == true;
    }

    private static bool RequiresCaching(Type type)
    {
        return type.Name.EndsWith("Service") ||
               type.Name.EndsWith("Repository") ||
               type.Name.EndsWith("Provider") ||
               type.GetMethods().Any(m => IsExpensiveOperation(m));
    }
}