using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Performance;

/// <summary>
/// Rule to validate proper resource disposal and management patterns.
/// </summary>
public class ResourceManagementRule : ArchitectureRuleBase
{
    public override string RuleId => "PERF004";
    public override string Name => "Resource Management Rule";
    public override string Description => "Validates proper resource disposal, memory management, and collection usage patterns";
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
                await CheckResourceManagement(type, violations);
            }
        }

        return violations;
    }

    private static bool IsApplicationType(Type type)
    {
        return !type.IsInterface &&
               !type.IsAbstract &&
               type.IsClass &&
               type.Namespace?.Contains("System") != true &&
               type.Namespace?.Contains("Microsoft") != true;
    }

    private async Task CheckResourceManagement(Type type, List<RuleViolation> violations)
    {
        // Check disposable pattern implementation
        await CheckDisposablePattern(type, violations);

        // Check field and property resource usage
        await CheckFieldsAndProperties(type, violations);

        // Check method resource usage
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.IsConstructor)
            .ToList();

        foreach (var method in methods)
        {
            await CheckMethodResourceUsage(type, method, violations);
        }

        // Check collection usage patterns
        await CheckCollectionUsage(type, violations);

        // Check memory allocation patterns
        await CheckMemoryAllocationPatterns(type, violations);
    }

    private async Task CheckDisposablePattern(Type type, List<RuleViolation> violations)
    {
        var implementsDisposable = type.GetInterfaces().Any(i => i.Name == "IDisposable");
        var implementsAsyncDisposable = type.GetInterfaces().Any(i => i.Name == "IAsyncDisposable");
        var hasDisposableFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(f => IsDisposableType(f.FieldType));

        // Check if type has disposable fields but doesn't implement IDisposable
        if (hasDisposableFields && !implementsDisposable && !implementsAsyncDisposable)
        {
            violations.Add(CreateViolation(type,
                $"Type '{type.Name}' contains disposable fields but doesn't implement IDisposable.",
                "Implement IDisposable and properly dispose of all disposable fields"));
        }

        // Check for proper dispose pattern implementation
        if (implementsDisposable)
        {
            var hasDisposeMethod = type.GetMethods().Any(m => m.Name == "Dispose" && m.GetParameters().Length == 0);
            var hasProtectedDisposeMethod = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(m => m.Name == "Dispose" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(bool));

            if (!hasDisposeMethod)
            {
                violations.Add(CreateViolation(type,
                    $"Type '{type.Name}' implements IDisposable but doesn't have a Dispose() method.",
                    "Implement the Dispose() method as required by IDisposable interface"));
            }

            if (!hasProtectedDisposeMethod && hasDisposableFields)
            {
                violations.Add(CreateViolation(type,
                    $"Type '{type.Name}' should implement the dispose pattern with protected Dispose(bool) method.",
                    "Implement the full dispose pattern with protected Dispose(bool disposing) method"));
            }

            // Check for finalizer
            var hasFinalizer = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(m => m.Name == "Finalize");

            if (hasDisposableFields && !hasFinalizer)
            {
                violations.Add(CreateViolation(type,
                    $"Type '{type.Name}' has disposable resources but no finalizer for cleanup safety.",
                    "Consider implementing a finalizer or use SafeHandle for unmanaged resources"));
            }
        }

        await Task.CompletedTask;
    }

    private async Task CheckFieldsAndProperties(Type type, List<RuleViolation> violations)
    {
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            await CheckResourceField(type, field, violations);
        }

        foreach (var property in properties)
        {
            await CheckResourceProperty(type, property, violations);
        }
    }

    private async Task CheckResourceField(Type type, FieldInfo field, List<RuleViolation> violations)
    {
        // Check for large collections stored as fields
        if (IsLargeCollectionType(field.FieldType))
        {
            violations.Add(CreateViolation(type,
                $"Field '{field.Name}' is a potentially large collection that could cause memory issues.",
                "Consider using lazy loading or pagination for large collections"));
        }

        // Check for unmanaged resource fields
        if (IsUnmanagedResourceType(field.FieldType))
        {
            violations.Add(CreateViolation(type,
                $"Field '{field.Name}' is an unmanaged resource that requires proper disposal.",
                "Ensure proper disposal in Dispose method or use using statements"));
        }

        // Check for static collections that can grow
        if (field.IsStatic && IsGrowableCollectionType(field.FieldType))
        {
            violations.Add(CreateViolation(type,
                $"Static field '{field.Name}' is a growable collection that could cause memory leaks.",
                "Use weak references, size limits, or periodic cleanup for static collections"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckResourceProperty(Type type, PropertyInfo property, List<RuleViolation> violations)
    {
        // Check for properties that create new instances every time
        if (CreatesNewInstancesOnAccess(property))
        {
            violations.Add(CreateViolation(type,
                $"Property '{property.Name}' creates new instances on each access, causing unnecessary allocations.",
                "Cache the instance or use lazy initialization to avoid repeated allocations"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckMethodResourceUsage(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Check for methods that don't use 'using' statements
        if (CreatesDisposableResource(method) && !UsesUsingStatement(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' creates disposable resources but may not dispose them properly.",
                "Use 'using' statements or 'using' declarations for automatic resource disposal"));
        }

        // Check for unnecessary string concatenation
        if (HasStringConcatenationInLoop(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' performs string concatenation in loops.",
                "Use StringBuilder for multiple string concatenations to improve performance"));
        }

        // Check for inefficient LINQ usage
        if (HasInefficientLinqUsage(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' may have inefficient LINQ usage.",
                "Avoid Count() > 0 (use Any()), avoid multiple enumerations, use appropriate collection types"));
        }

        // Check for boxing in performance-critical code
        if (HasBoxingOperations(method))
        {
            violations.Add(CreateViolation(type,
                $"Method '{method.Name}' may cause boxing of value types.",
                "Avoid boxing by using generic methods or value type-specific overloads"));
        }

        await Task.CompletedTask;
    }

    private async Task CheckCollectionUsage(Type type, List<RuleViolation> violations)
    {
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Check for inappropriate collection types
        foreach (var field in fields)
        {
            if (IsInappropriateCollectionType(field.FieldType))
            {
                violations.Add(CreateViolation(type,
                    $"Field '{field.Name}' uses suboptimal collection type '{field.FieldType.Name}'.",
                    "Consider using more appropriate collection types based on usage patterns"));
            }
        }

        foreach (var property in properties)
        {
            if (IsInappropriateCollectionType(property.PropertyType))
            {
                violations.Add(CreateViolation(type,
                    $"Property '{property.Name}' uses suboptimal collection type '{property.PropertyType.Name}'.",
                    "Consider using more appropriate collection types based on usage patterns"));
            }
        }

        await Task.CompletedTask;
    }

    private async Task CheckMemoryAllocationPatterns(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var method in methods.Where(m => !m.IsSpecialName))
        {
            if (HasExcessiveAllocations(method))
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' may have excessive memory allocations.",
                    "Consider object pooling, span usage, or allocation reduction techniques"));
            }
        }

        await Task.CompletedTask;
    }

    private static bool IsDisposableType(Type type)
    {
        return type.GetInterfaces().Any(i => i.Name == "IDisposable" || i.Name == "IAsyncDisposable") ||
               type.Name.Contains("Stream") ||
               type.Name.Contains("Reader") ||
               type.Name.Contains("Writer") ||
               type.Name.Contains("Connection") ||
               type.Name.Contains("Context");
    }

    private static bool IsLargeCollectionType(Type type)
    {
        return type.IsGenericType && 
               (type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                type.GetGenericTypeDefinition() == typeof(HashSet<>));
    }

    private static bool IsUnmanagedResourceType(Type type)
    {
        return type.Name.Contains("Handle") ||
               type.Name.Contains("Pointer") ||
               type.Name.Contains("IntPtr") ||
               type.Name.Contains("Stream") ||
               type.Name.Contains("Connection");
    }

    private static bool IsGrowableCollectionType(Type type)
    {
        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                type.GetGenericTypeDefinition() == typeof(HashSet<>) ||
                type.GetGenericTypeDefinition() == typeof(Queue<>) ||
                type.GetGenericTypeDefinition() == typeof(Stack<>));
    }

    private static bool CreatesNewInstancesOnAccess(PropertyInfo property)
    {
        // Simplified check - would analyze getter implementation in real scenario
        return property.CanRead && 
               property.PropertyType.IsClass && 
               property.PropertyType != typeof(string) &&
               !property.PropertyType.IsAbstract;
    }

    private static bool CreatesDisposableResource(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("create") || 
               name.Contains("open") || 
               name.Contains("new") ||
               method.ReturnType.GetInterfaces().Any(i => i.Name == "IDisposable");
    }

    private static bool UsesUsingStatement(MethodInfo method)
    {
        // Simplified check - would need IL analysis for accurate detection
        return method.Name.Contains("Using") || method.GetMethodBody()?.LocalVariables.Any() == true;
    }

    private static bool HasStringConcatenationInLoop(MethodInfo method)
    {
        // Simplified check - would analyze method body in real implementation
        var name = method.Name.ToLower();
        return name.Contains("concat") || name.Contains("append") || name.Contains('+');
    }

    private static bool HasInefficientLinqUsage(MethodInfo method)
    {
        var name = method.Name.ToLower();
        return name.Contains("count") || name.Contains("tolist") || name.Contains("multiple");
    }

    private static bool HasBoxingOperations(MethodInfo method)
    {
        // Simplified check - would analyze IL for actual boxing operations
        return method.GetParameters().Any(p => p.ParameterType == typeof(object)) ||
               method.ReturnType == typeof(object);
    }

    private static bool IsInappropriateCollectionType(Type type)
    {
        // Check for ArrayList, Hashtable, or other non-generic collections
        return type == typeof(System.Collections.ArrayList) ||
               type == typeof(System.Collections.Hashtable) ||
               type == typeof(System.Collections.Queue) ||
               type == typeof(System.Collections.Stack);
    }

    private static bool HasExcessiveAllocations(MethodInfo method)
    {
        // Simplified check - would analyze allocation patterns in real implementation
        var name = method.Name.ToLower();
        return name.Contains("new") || 
               name.Contains("create") || 
               name.Contains("allocate") ||
               method.GetParameters().Length > 10; // Many parameters might indicate allocation issues
    }
}