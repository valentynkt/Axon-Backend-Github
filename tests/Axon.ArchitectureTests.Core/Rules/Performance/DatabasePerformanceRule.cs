using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Performance;

/// <summary>
/// Rule to validate database performance patterns and prevent common performance anti-patterns.
/// </summary>
public class DatabasePerformanceRule : ArchitectureRuleBase
{
    public override string RuleId => "PERF003";
    public override string Name => "Database Performance Rule";
    public override string Description => "Validates database performance patterns and prevents N+1 queries, missing indexes, and other anti-patterns";
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
                .Where(t => IsDatabaseAccessType(t))
                .ToList();

            foreach (var type in types)
            {
                await CheckDatabasePerformancePatterns(type, violations);
            }
        }

        return violations;
    }

    private static bool IsDatabaseAccessType(Type type)
    {
        return !type.IsInterface &&
               !type.IsAbstract &&
               type.IsClass &&
               (type.Name.EndsWith("Repository") ||
                type.Name.EndsWith("Context") ||
                type.Name.EndsWith("Service") ||
                type.Name.EndsWith("DataAccess") ||
                type.BaseType?.Name.Contains("DbContext") == true ||
                type.GetInterfaces().Any(i => i.Name.Contains("Repository")));
    }

    private async Task CheckDatabasePerformancePatterns(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.IsConstructor)
            .ToList();

        foreach (var method in methods)
        {
            await CheckNPlusOneQueries(type, method, violations);
            await CheckSelectNPlus1Patterns(type, method, violations);
            await CheckLazyLoadingIssues(type, method, violations);
            await CheckBulkOperations(type, method, violations);
            await CheckProjectionPatterns(type, method, violations);
            await CheckIndexHints(type, method, violations);
        }

        await CheckEntityConfiguration(type, violations);
    }

    private async Task CheckNPlusOneQueries(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Check for potential N+1 query patterns
        if (HasLoopWithDatabaseCall(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' may have N+1 query problem - database calls inside loops.",
                "Use Include() for eager loading or batch operations to avoid N+1 queries"));
        }

        // Check for missing Include() calls
        if (NavigatesRelatedData(method) && !HasEagerLoading(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' accesses related data but doesn't use eager loading.",
                "Use Include() or ThenInclude() to eagerly load related data and prevent lazy loading queries"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckSelectNPlus1Patterns(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (ReturnsCollection(method) && HasNestedDataAccess(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' returns collection with nested data access that may cause SELECT N+1.",
                "Use projection (Select) or Include to fetch all data in single query"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckLazyLoadingIssues(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (type.BaseType?.Name.Contains("DbContext") == true)
        {
            // Check if lazy loading is enabled but not controlled
            if (HasLazyLoadingEnabled(type) && !HasProperLazyLoadingControl(method))
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' may cause uncontrolled lazy loading.",
                    "Disable lazy loading or use explicit loading patterns for better performance control"));
            }
        }

        await Task.CompletedTask;
    }

    private async Task CheckBulkOperations(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (IsBulkOperation(method) && !UsesBulkOperationPattern(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' performs bulk operations but doesn't use efficient bulk patterns.",
                "Use AddRange, UpdateRange, RemoveRange, or bulk extensions for better performance"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckProjectionPatterns(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (ReturnsLimitedData(method) && !UsesProjection(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' returns limited data but doesn't use projection.",
                "Use Select() to project only needed data and improve query performance"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckIndexHints(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (HasComplexQuery(method) && !HasIndexConsiderations(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' has complex queries but may not consider indexing.",
                "Review query patterns and ensure appropriate indexes exist for performance"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckEntityConfiguration(Type type, List<RuleViolation> violations)
    {
        if (type.BaseType?.Name.Contains("DbContext") == true)
        {
            // Check for missing query tracking configuration
            if (!HasQueryTrackingConfiguration(type))
            {
                violations.Add(CreateViolation(type,
                    $"DbContext '{type.Name}' should configure query tracking behavior.",
                    "Use AsNoTracking() for read-only queries or configure default tracking behavior"));
            }

            // Check for connection management
            if (!HasProperConnectionManagement(type))
            {
                violations.Add(CreateViolation(type,
                    $"DbContext '{type.Name}' may not have proper connection management.",
                    "Ensure proper connection pooling and disposal patterns"));
            }
        }

        await Task.CompletedTask;
    }

    private static bool HasLoopWithDatabaseCall(MethodInfo method)
    {
        // Simplified check - would need IL analysis for accurate detection
        var name = method.Name.ToLower();
        return (name.Contains("foreach") || name.Contains("loop")) && 
               (name.Contains("get") || name.Contains("find") || name.Contains("query"));
    }

    private static bool NavigatesRelatedData(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("include") || name.Contains("with") || name.Contains("related");
    }

    private static bool HasEagerLoading(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("include") || name.Contains("theninclude");
    }

    private static bool ReturnsCollection(MethodInfo method)
    {
        return method.ReturnType.IsGenericType && 
               (method.ReturnType.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                method.ReturnType.GetGenericTypeDefinition() == typeof(List<>) ||
                method.ReturnType.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                method.ReturnType.GetGenericTypeDefinition() == typeof(IQueryable<>));
    }

    private static bool HasNestedDataAccess(MethodInfo method)
    {
        // Simplified check - would analyze method body in real implementation
        return method.Name.Contains("Select") || method.Name.Contains("Navigation");
    }

    private static bool HasLazyLoadingEnabled(Type type)
    {
        // Check if lazy loading proxies are enabled
        return type.GetProperties().Any(p => p.Name.Contains("LazyLoading"));
    }

    private static bool HasProperLazyLoadingControl(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("asnotracking") || name.Contains("explicit");
    }

    private static bool IsBulkOperation(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("bulk") || 
               name.Contains("batch") || 
               (name.Contains("add") && name.Contains("range")) ||
               (name.Contains("update") && name.Contains("range")) ||
               (name.Contains("delete") && name.Contains("range"));
    }

    private static bool UsesBulkOperationPattern(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("range") || name.Contains("bulk") || name.Contains("batch");
    }

    private static bool ReturnsLimitedData(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("summary") || 
               name.Contains("preview") || 
               name.Contains("list") ||
               name.Contains("brief");
    }

    private static bool UsesProjection(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("select") || name.Contains("project");
    }

    private static bool HasComplexQuery(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("where") || 
               name.Contains("join") || 
               name.Contains("group") ||
               name.Contains("order") ||
               name.Contains("search");
    }

    private static bool HasIndexConsiderations(MethodInfo method)
    {
        // Simplified check - would analyze query patterns in real implementation
        var name = method.Name.ToLower();
        return name.Contains("index") || name.Contains("optimized");
    }

    private static bool HasQueryTrackingConfiguration(Type type)
    {
        var methods = type.GetMethods();
        return methods.Any(m => m.Name.Contains("AsNoTracking") || m.Name.Contains("QueryTracking"));
    }

    private static bool HasProperConnectionManagement(Type type)
    {
        // Check for proper disposal and connection patterns
        var hasDisposePattern = type.GetInterfaces().Any(i => i.Name == "IDisposable");
        var hasAsyncDisposal = type.GetInterfaces().Any(i => i.Name == "IAsyncDisposable");
        
        return hasDisposePattern || hasAsyncDisposal;
    }
}