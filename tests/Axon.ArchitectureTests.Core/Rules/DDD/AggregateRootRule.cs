using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.DDD;

/// <summary>
/// Validates that Aggregate Roots follow DDD patterns and best practices.
/// </summary>
public sealed class AggregateRootRule : PatternComplianceRule
{
    public override string RuleId => "DDD001";
    public override string Name => "Aggregate Root Rule";
    public override string Description => "Aggregate roots must be sealed, inherit from AggregateRoot<T>, and be in Domain layer";
    public override string Category => "DDD";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var aggregateTypes = context.Types
                .Where(t => IsAggregateRoot(t))
                .ToList();

            foreach (var aggregateType in aggregateTypes)
            {
                ValidateAggregateLocation(aggregateType, violations);
                ValidateAggregateStructure(aggregateType, violations);
                ValidateAggregateNaming(aggregateType, violations);
                ValidateAggregateConstructors(aggregateType, violations);
                ValidateAggregateInvariants(aggregateType, violations);
                ValidateAggregateDomainEvents(aggregateType, violations);
                ValidateAggregateBusinessMethods(aggregateType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsAggregateRoot(Type type) =>
        InheritsFromAggregateRoot(type) || 
        type.Name.EndsWith("Aggregate", StringComparison.OrdinalIgnoreCase) ||
        IsInDomainNamespace(type) && type.IsClass && !type.IsAbstract;

    private static bool InheritsFromAggregateRoot(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.Name.Contains("AggregateRoot", StringComparison.OrdinalIgnoreCase))
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    private static bool IsInDomainNamespace(Type type) =>
        type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateAggregateLocation(Type aggregateType, List<RuleViolation> violations)
    {
        if (!IsInDomainNamespace(aggregateType))
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregate roots must be placed in the Domain layer",
                $"Move '{aggregateType.Name}' to a Domain namespace"));
        }
    }

    private void ValidateAggregateStructure(Type aggregateType, List<RuleViolation> violations)
    {
        if (!IsSealed(aggregateType))
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregate roots should be sealed to prevent inheritance",
                $"Make '{aggregateType.Name}' sealed"));
        }

        if (!InheritsFromAggregateRoot(aggregateType))
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregate roots should inherit from AggregateRoot<T> base class",
                $"Make '{aggregateType.Name}' inherit from AggregateRoot<TId>"));
        }
    }

    private void ValidateAggregateNaming(Type aggregateType, List<RuleViolation> violations)
    {
        var typeName = aggregateType.Name;
        
        // Should not have "Aggregate" suffix unless it's a specific naming convention
        if (typeName.EndsWith("Aggregate", StringComparison.OrdinalIgnoreCase) && 
            typeName != "Aggregate")
        {
            var suggestedName = typeName[..^9]; // Remove "Aggregate"
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregate roots should use business names, not technical suffixes",
                $"Rename '{typeName}' to '{suggestedName}'"));
        }
    }

    private void ValidateAggregateConstructors(Type aggregateType, List<RuleViolation> violations)
    {
        var constructors = GetConstructors(aggregateType);
        var publicConstructors = constructors.Where(c => c.IsPublic).ToList();

        if (publicConstructors.Count == 0)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregate roots must have at least one public constructor",
                $"Add a public constructor to '{aggregateType.Name}'"));
        }

        // Check for parameterless constructor (often needed for persistence)
        var hasParameterlessConstructor = publicConstructors.Any(c => c.GetParameters().Length == 0);
        if (!hasParameterlessConstructor)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregate roots should have a parameterless constructor for persistence frameworks",
                $"Add a parameterless constructor to '{aggregateType.Name}' (can be private)"));
        }
    }

    /// <summary>
    /// Validates that aggregates have proper invariant validation methods.
    /// </summary>
    private void ValidateAggregateInvariants(Type aggregateType, List<RuleViolation> violations)
    {
        var methods = aggregateType.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var hasInvariantValidation = methods.Any(m => 
            m.Name.Contains("Validate", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Invariant", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("EnsureValid", StringComparison.OrdinalIgnoreCase));

        var publicMethods = aggregateType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == aggregateType)
            .ToList();

        if (publicMethods.Any() && !hasInvariantValidation)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregates with business methods should have invariant validation",
                $"Add invariant validation methods to '{aggregateType.Name}'"));
        }

        // Check that business methods validate state
        foreach (var method in publicMethods.Where(m => !IsPropertyAccessor(m)))
        {
            if (!ReturnsResultType(method) && method.ReturnType != typeof(void))
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Business method '{method.Name}' should return Result type for proper error handling",
                    $"Make '{method.Name}' return Result or Result<T>"));
            }
        }
    }

    /// <summary>
    /// Validates that aggregates properly handle domain events.
    /// </summary>
    private void ValidateAggregateDomainEvents(Type aggregateType, List<RuleViolation> violations)
    {
        var properties = GetPublicProperties(aggregateType);
        var hasDomainEvents = properties.Any(p => 
            p.Name.Contains("DomainEvent", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Event", StringComparison.OrdinalIgnoreCase) ||
            p.PropertyType.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));

        var methods = aggregateType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var hasAddEventMethod = methods.Any(m => 
            m.Name.Contains("AddEvent", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("RaiseEvent", StringComparison.OrdinalIgnoreCase));

        // If aggregate has state-changing methods, it should support domain events
        var hasBusinessMethods = methods.Any(m => 
            !m.IsSpecialName && 
            m.DeclaringType == aggregateType && 
            !IsPropertyAccessor(m));

        if (hasBusinessMethods && !hasDomainEvents && !hasAddEventMethod)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregates with business methods should support domain events",
                $"Add domain event support to '{aggregateType.Name}'"));
        }
    }

    /// <summary>
    /// Validates that aggregate business methods follow DDD patterns.
    /// </summary>
    private void ValidateAggregateBusinessMethods(Type aggregateType, List<RuleViolation> violations)
    {
        var publicMethods = aggregateType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == aggregateType && !IsPropertyAccessor(m))
            .ToList();

        foreach (var method in publicMethods)
        {
            // Business methods should use domain language, not CRUD operations
            if (IsCrudMethodName(method.Name))
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Method '{method.Name}' uses CRUD language instead of domain language",
                    $"Rename '{method.Name}' to use ubiquitous domain language"));
            }

            // Methods that change state should be verbs
            if (method.ReturnType == typeof(void) || ReturnsResultType(method))
            {
                if (!IsVerbMethodName(method.Name))
                {
                    violations.Add(CreateViolation(
                        aggregateType,
                        $"Business method '{method.Name}' should use verb naming (e.g., ProcessOrder, ApproveRequest)",
                        $"Rename '{method.Name}' to use verb-based naming"));
                }
            }
        }
    }

    /// <summary>
    /// Checks if a method is a property accessor (getter/setter).
    /// </summary>
    private static bool IsPropertyAccessor(System.Reflection.MethodInfo method)
    {
        return method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"));
    }

    /// <summary>
    /// Checks if a method returns a Result type.
    /// </summary>
    private static bool ReturnsResultType(System.Reflection.MethodInfo method)
    {
        return method.ReturnType.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase) ||
               method.ReturnType.Namespace?.Contains("Result") == true;
    }

    /// <summary>
    /// Checks if a method name uses CRUD language.
    /// </summary>
    private static bool IsCrudMethodName(string methodName)
    {
        string[] crudPrefixes = { "Create", "Read", "Update", "Delete", "Insert", "Select", "Add", "Remove", "Get", "Set" };
        return crudPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a method name uses verb-based naming appropriate for business methods.
    /// </summary>
    private static bool IsVerbMethodName(string methodName)
    {
        // Simple heuristic: method names that don't start with CRUD operations
        // and don't start with "Is", "Has", "Can" (which are typically queries)
        string[] queryPrefixes = { "Is", "Has", "Can", "Should", "Will" };
        string[] crudPrefixes = { "Create", "Read", "Update", "Delete", "Insert", "Select", "Add", "Remove", "Get", "Set" };
        
        return !queryPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) &&
               !crudPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}