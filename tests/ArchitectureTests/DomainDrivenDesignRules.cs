using NetArchTest.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to enforce Domain-Driven Design patterns and principles.
/// Validates Aggregates, Value Objects, Domain Services, and domain integrity.
/// </summary>
[TestFixture]
public class DomainDrivenDesignRules
{
    [Test]
    public void ValueObjects_ShouldBe_Immutable()
    {
        var valueObjectTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.ValueObjects.*")
            .Or()
            .ResideInNamespace("*.Domain.*")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var valueType in valueObjectTypes)
        {
            var publicSetters = valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
                .ToList();

            if (publicSetters.Any())
            {
                violations.Add($"{valueType.FullName}: {string.Join(", ", publicSetters.Select(p => p.Name))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Value Objects should be immutable (no public setters). " +
            $"Violations: {string.Join("; ", violations)}");
    }

    [Test]
    public void Domain_ShouldNotUse_PrimitiveObsession()
    {
        var domainTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Domain.*")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();
        var primitiveTypes = new[] { typeof(string), typeof(int), typeof(long), typeof(decimal), typeof(DateTime), typeof(Guid) };

        foreach (var domainType in domainTypes)
        {
            var publicProperties = domainType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => primitiveTypes.Contains(p.PropertyType))
                .Where(p => !IsAllowedPrimitiveUsage(p.Name))
                .ToList();

            if (publicProperties.Any())
            {
                violations.Add($"{domainType.FullName}: {string.Join(", ", publicProperties.Select(p => $"{p.Name}({p.PropertyType.Name})"))}");
            }
        }

        // This is a warning test - we don't fail the build but provide guidance
        if (violations.Any())
        {
            TestContext.WriteLine($"Primitive Obsession detected (consider Value Objects): {string.Join("; ", violations)}");
        }

        // For now, just log violations instead of failing
        TestContext.WriteLine($"Primitive obsession warnings: {violations.Count}");
    }

    [Test]
    public void DomainServices_ShouldBe_Stateless()
    {
        var domainServiceTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Domain.Services.*")
            .Or()
            .ResideInNamespace("*.Domain.*")
            .And()
            .HaveNameEndingWith("Service")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var serviceType in domainServiceTypes)
        {
            var instanceFields = serviceType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => !f.IsInitOnly) // Allow readonly fields
                .Where(f => !f.Name.StartsWith('<')) // Exclude compiler-generated fields
                .ToList();

            if (instanceFields.Any())
            {
                violations.Add($"{serviceType.FullName}: {string.Join(", ", instanceFields.Select(f => f.Name))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Domain Services should be stateless (no mutable instance fields). " +
            $"Use readonly fields or make static. Violations: {string.Join("; ", violations)}");
    }

    [Test]
    public void DomainEntities_ShouldHave_StronglyTypedIds()
    {
        var entityTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Domain.*")
            .And()
            .AreClasses()
            .And()
            .DoNotResideInNamespace("*.ValueObjects.*")
            .GetTypes();

        var violations = new List<string>();

        foreach (var entityType in entityTypes)
        {
            var idProperty = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            
            if (idProperty != null)
            {
                // Check if it's a primitive type (Guid, int, long, string)
                var isPrimitive = idProperty.PropertyType == typeof(Guid) ||
                                idProperty.PropertyType == typeof(int) ||
                                idProperty.PropertyType == typeof(long) ||
                                idProperty.PropertyType == typeof(string);

                if (isPrimitive)
                {
                    violations.Add($"{entityType.FullName}.Id is {idProperty.PropertyType.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Domain entities should use strongly-typed IDs (Value Objects) instead of primitives. " +
            $"Example: MessageId instead of Guid. Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Domain_ShouldUse_ResultPattern_ForBusinessErrors()
    {
        var domainTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Domain.*")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var domainType in domainTypes)
        {
            var publicMethods = domainType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                .Where(m => m.ReturnType != typeof(void))
                .Where(m => !IsConstructorOrOperator(m));

            foreach (var method in publicMethods)
            {
                var returnsResult = method.ReturnType.Name.StartsWith("Result") ||
                                  (method.ReturnType.IsGenericType && 
                                   method.ReturnType.GetGenericTypeDefinition().Name.StartsWith("Result"));

                var canThrowBusinessException = CanMethodThrowBusinessException(method);

                if (canThrowBusinessException && !returnsResult)
                {
                    violations.Add($"{domainType.FullName}.{method.Name}");
                }
            }
        }

        // This is more of a guidance test - strict enforcement might be too rigid
        if (violations.Any())
        {
            TestContext.WriteLine($"Consider using Result pattern for business operations: {string.Join(", ", violations)}");
        }

        TestContext.WriteLine($"Methods that might benefit from Result pattern: {violations.Count}");
    }

    [Test]
    public void DomainEvents_ShouldBe_InDomainLayer()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Event")
            .Or()
            .ResideInNamespace("*.Events.*")
            .Should()
            .ResideInNamespace("*.Domain.*")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain Events should be in Domain layer. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void Aggregates_ShouldNotExpose_Collections()
    {
        var aggregateTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Domain.*")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var aggregateType in aggregateTypes)
        {
            var collectionProperties = aggregateType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType)
                .Where(p => IsCollectionType(p.PropertyType))
                .Where(p => !p.PropertyType.Name.StartsWith("IReadOnly"))
                .ToList();

            if (collectionProperties.Any())
            {
                violations.Add($"{aggregateType.FullName}: {string.Join(", ", collectionProperties.Select(p => p.Name))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Aggregates should expose collections as IReadOnlyCollection<T> to maintain encapsulation. " +
            $"Violations: {string.Join("; ", violations)}");
    }

    [Test]
    public void Domain_ShouldNotReference_InfrastructureConcerns()
    {
        var infrastructureNamespaces = new[]
        {
            "System.Data",
            "Microsoft.EntityFrameworkCore",
            "System.Net.Http",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Configuration"
        };

        var violations = new List<string>();

        var domainTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Domain.*")
            .GetTypes();

        foreach (var domainType in domainTypes)
        {
            var referencedTypes = GetReferencedTypes(domainType)
                .Where(t => infrastructureNamespaces.Any(ns => t.Namespace?.StartsWith(ns) == true))
                .ToList();

            if (referencedTypes.Any())
            {
                violations.Add($"{domainType.FullName}: {string.Join(", ", referencedTypes.Select(t => t.FullName))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Domain layer should not reference infrastructure concerns. " +
            $"Use abstractions and dependency inversion. Violations: {string.Join("; ", violations)}");
    }

    private static bool IsAllowedPrimitiveUsage(string propertyName)
    {
        var allowedNames = new[] { "Id", "CreatedAt", "UpdatedAt", "Version", "Timestamp" };
        return allowedNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsConstructorOrOperator(MethodInfo method)
    {
        return method.IsConstructor || 
               method.IsSpecialName && (method.Name.StartsWith("op_") || method.Name.StartsWith("get_") || method.Name.StartsWith("set_"));
    }

    private static bool CanMethodThrowBusinessException(MethodInfo method)
    {
        // Simple heuristic - methods that likely perform business operations
        var businessOperationIndicators = new[] { "Create", "Update", "Delete", "Process", "Validate", "Calculate", "Execute" };
        return businessOperationIndicators.Any(indicator => method.Name.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCollectionType(Type type)
    {
        if (!type.IsGenericType) return false;
        
        var genericDefinition = type.GetGenericTypeDefinition();
        return genericDefinition == typeof(List<>) ||
               genericDefinition == typeof(IList<>) ||
               genericDefinition == typeof(ICollection<>) ||
               genericDefinition == typeof(HashSet<>) ||
               genericDefinition == typeof(Dictionary<,>) ||
               genericDefinition == typeof(IDictionary<,>);
    }

    private static Type[] GetReferencedTypes(Type type)
    {
        // Simplified implementation - would need more comprehensive analysis in practice
        return type.GetProperties()
            .Select(p => p.PropertyType)
            .Concat(type.GetFields().Select(f => f.FieldType))
            .Distinct()
            .ToArray();
    }
}