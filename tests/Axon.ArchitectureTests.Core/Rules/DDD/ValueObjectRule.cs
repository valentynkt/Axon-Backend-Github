using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.DDD;

/// <summary>
/// Validates that Value Objects follow DDD patterns and best practices.
/// </summary>
public sealed class ValueObjectRule : PatternComplianceRule
{
    public override string RuleId => "DDD002";
    public override string Name => "Value Object Rule";
    public override string Description => "Value objects must be immutable records or readonly structs in Domain layer";
    public override string Category => "DDD";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var valueObjectTypes = context.Types
                .Where(t => IsValueObject(t))
                .ToList();

            foreach (var valueObjectType in valueObjectTypes)
            {
                ValidateValueObjectLocation(valueObjectType, violations);
                ValidateValueObjectImmutability(valueObjectType, violations);
                ValidateValueObjectStructure(valueObjectType, violations);
                ValidateValueObjectEquality(valueObjectType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsValueObject(Type type) =>
        IsInDomainNamespace(type) && 
        (type.IsValueType || IsRecord(type) || HasValueObjectNaming(type));

    private static bool IsInDomainNamespace(Type type) =>
        type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;

    private static bool HasValueObjectNaming(Type type) =>
        type.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("Value", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Address", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Money", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecord(Type type) =>
        type.GetMethod("Equals", new[] { type }) != null &&
        type.GetMethod("GetHashCode", Type.EmptyTypes) != null &&
        type.BaseType == typeof(object);

    private void ValidateValueObjectLocation(Type valueObjectType, List<RuleViolation> violations)
    {
        if (!IsInDomainNamespace(valueObjectType))
        {
            violations.Add(CreateViolation(
                valueObjectType,
                "Value objects must be placed in the Domain layer",
                $"Move '{valueObjectType.Name}' to a Domain namespace"));
        }
    }

    private void ValidateValueObjectImmutability(Type valueObjectType, List<RuleViolation> violations)
    {
        if (!valueObjectType.IsValueType && !IsRecord(valueObjectType))
        {
            // Check for mutable properties
            var mutableProperties = GetPublicProperties(valueObjectType)
                .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
                .ToList();

            if (mutableProperties.Any())
            {
                violations.Add(CreateViolation(
                    valueObjectType,
                    "Value objects must be immutable - avoid public setters",
                    $"Make properties in '{valueObjectType.Name}' read-only or convert to record"));
            }
        }
    }

    private void ValidateValueObjectStructure(Type valueObjectType, List<RuleViolation> violations)
    {
        // Prefer records or readonly structs for value objects
        if (!valueObjectType.IsValueType && !IsRecord(valueObjectType))
        {
            violations.Add(CreateViolation(
                valueObjectType,
                "Value objects should be implemented as records or readonly structs",
                $"Convert '{valueObjectType.Name}' to a record or readonly struct"));
        }

        // Should be sealed if it's a class
        if (valueObjectType.IsClass && !IsSealed(valueObjectType) && !IsRecord(valueObjectType))
        {
            violations.Add(CreateViolation(
                valueObjectType,
                "Value object classes should be sealed",
                $"Make '{valueObjectType.Name}' sealed"));
        }
    }

    private void ValidateValueObjectEquality(Type valueObjectType, List<RuleViolation> violations)
    {
        // Check if type overrides Equals and GetHashCode
        var hasEqualsOverride = valueObjectType.GetMethod("Equals", new[] { typeof(object) })?.DeclaringType == valueObjectType;
        var hasGetHashCodeOverride = valueObjectType.GetMethod("GetHashCode", Type.EmptyTypes)?.DeclaringType == valueObjectType;

        if (!IsRecord(valueObjectType) && !valueObjectType.IsValueType)
        {
            if (!hasEqualsOverride)
            {
                violations.Add(CreateViolation(
                    valueObjectType,
                    "Value objects must override Equals method for structural equality",
                    $"Override Equals(object) in '{valueObjectType.Name}' or convert to record"));
            }

            if (!hasGetHashCodeOverride)
            {
                violations.Add(CreateViolation(
                    valueObjectType,
                    "Value objects must override GetHashCode method",
                    $"Override GetHashCode() in '{valueObjectType.Name}' or convert to record"));
            }
        }
    }
}