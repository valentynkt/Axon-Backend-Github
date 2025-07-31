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
}