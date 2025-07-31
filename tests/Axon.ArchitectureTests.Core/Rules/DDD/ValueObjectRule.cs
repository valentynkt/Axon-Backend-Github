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
                ValidateValueObjectValidation(valueObjectType, violations);
                ValidateValueObjectFactoryMethods(valueObjectType, violations);
                ValidateValueObjectComparison(valueObjectType, violations);
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

    /// <summary>
    /// Validates that value objects have proper validation methods.
    /// </summary>
    private void ValidateValueObjectValidation(Type valueObjectType, List<RuleViolation> violations)
    {
        var constructors = GetConstructors(valueObjectType);
        var hasValidation = false;

        // Check for validation in constructors or factory methods
        foreach (var constructor in constructors.Where(c => c.IsPublic))
        {
            var parameters = constructor.GetParameters();
            if (parameters.Length > 0)
            {
                // Look for validation logic (this is heuristic)
                hasValidation = true; // Assume constructors with parameters do validation
                break;
            }
        }

        // Check for factory methods that might do validation
        var methods = valueObjectType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var hasFactoryMethods = methods.Any(m => 
            m.Name.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("From", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Parse", StringComparison.OrdinalIgnoreCase));

        if (!hasValidation && !hasFactoryMethods && !IsSimpleValueType(valueObjectType))
        {
            violations.Add(CreateViolation(
                valueObjectType,
                "Value objects should validate their invariants during construction",
                $"Add validation logic to '{valueObjectType.Name}' constructor or factory methods"));
        }
    }

    /// <summary>
    /// Validates that value objects use factory methods for complex creation.
    /// </summary>
    private void ValidateValueObjectFactoryMethods(Type valueObjectType, List<RuleViolation> violations)
    {
        var methods = valueObjectType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var constructors = GetConstructors(valueObjectType).Where(c => c.IsPublic).ToList();

        // If there are multiple constructors or complex validation, suggest factory methods
        if (constructors.Count > 1 && !methods.Any(m => m.Name.Contains("Create", StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add(CreateViolation(
                valueObjectType,
                "Value objects with multiple constructors should use factory methods for clarity",
                $"Consider adding factory methods like 'Create', 'From', or 'Parse' to '{valueObjectType.Name}'"));
        }

        // Check for factory methods returning Result pattern
        var factoryMethods = methods.Where(m => 
            m.ReturnType == valueObjectType ||
            (m.ReturnType.IsGenericType && m.ReturnType.GetGenericArguments().Any(t => t == valueObjectType))).ToList();

        foreach (var factoryMethod in factoryMethods)
        {
            if (!ReturnsResultType(factoryMethod) && HasComplexParameters(factoryMethod))
            {
                violations.Add(CreateViolation(
                    valueObjectType,
                    $"Factory method '{factoryMethod.Name}' should return Result<{valueObjectType.Name}> for proper error handling",
                    $"Change '{factoryMethod.Name}' to return Result<{valueObjectType.Name}>"));
            }
        }
    }

    /// <summary>
    /// Validates that comparable value objects implement IComparable.
    /// </summary>
    private void ValidateValueObjectComparison(Type valueObjectType, List<RuleViolation> violations)
    {
        // Check if this looks like a comparable value (numbers, dates, etc.)
        if (IsComparableValueType(valueObjectType))
        {
            var implementsComparable = ImplementsInterface(valueObjectType, typeof(IComparable)) ||
                                     ImplementsGenericInterface(valueObjectType, typeof(IComparable<>));

            if (!implementsComparable)
            {
                violations.Add(CreateViolation(
                    valueObjectType,
                    $"Comparable value object '{valueObjectType.Name}' should implement IComparable<T>",
                    $"Implement IComparable<{valueObjectType.Name}> in '{valueObjectType.Name}'"));
            }
        }

        // Check for comparison operators if IComparable is implemented
        if (ImplementsGenericInterface(valueObjectType, typeof(IComparable<>)))
        {
            var hasComparisonOperators = HasComparisonOperators(valueObjectType);
            if (!hasComparisonOperators)
            {
                violations.Add(CreateViolation(
                    valueObjectType,
                    "Value objects implementing IComparable should also define comparison operators",
                    $"Add comparison operators (==, !=, <, >, <=, >=) to '{valueObjectType.Name}'"));
            }
        }
    }

    /// <summary>
    /// Checks if a value object is a simple type that doesn't need complex validation.
    /// </summary>
    private static bool IsSimpleValueType(Type type)
    {
        return type.IsValueType && type.IsPrimitive;
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
    /// Checks if a method has complex parameters that might require validation.
    /// </summary>
    private static bool HasComplexParameters(System.Reflection.MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Length > 1 || 
               parameters.Any(p => p.ParameterType == typeof(string) || p.ParameterType.IsClass);
    }

    /// <summary>
    /// Checks if a value object represents a comparable value.
    /// </summary>
    private static bool IsComparableValueType(Type type)
    {
        return type.Name.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Time", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Age", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Score", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Quantity", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Price", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a type has comparison operators defined.
    /// </summary>
    private static bool HasComparisonOperators(Type type)
    {
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var operatorNames = new[] { "op_LessThan", "op_GreaterThan", "op_LessThanOrEqual", "op_GreaterThanOrEqual", "op_Equality", "op_Inequality" };
        
        return operatorNames.Any(op => methods.Any(m => m.Name == op));
    }
}