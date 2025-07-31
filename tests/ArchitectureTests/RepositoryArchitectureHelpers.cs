using System.Reflection;
using NetArchTest.Rules;

namespace Axon.ArchitectureTests;

/// <summary>
/// Helper methods for Repository Pattern architecture testing.
/// Contains repository detection and Entity Framework type checking logic.
/// </summary>
public static class RepositoryArchitectureHelpers
{
    private const string DomainLayer = "Axon.Modules.*.Domain";

    public static Type[] GetRepositoryImplementations()
    {
        return Types.InCurrentDomain()
            .That()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Repository")
            .GetTypes()
            .ToArray();
    }

    public static Type[] GetRepositoryInterfaces()
    {
        return Types.InCurrentDomain()
            .That()
            .AreInterfaces()
            .And()
            .HaveNameEndingWith("Repository")
            .GetTypes()
            .ToArray();
    }

    public static string[] GetEntityFrameworkTypes()
    {
        return new[]
        {
            "Microsoft.EntityFrameworkCore.DbContext",
            "Microsoft.EntityFrameworkCore.DbSet",
            "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry",
            "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade",
            "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction"
        };
    }

    public static bool ExposesEntityFrameworkTypes(MethodInfo method, string[] efTypes)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        // Check return type
        if (efTypes.Any(efType => method.ReturnType.FullName?.Contains(efType) == true))
            return true;

        // Check parameter types
        return method.GetParameters()
            .Any(param => efTypes.Any(efType => param.ParameterType.FullName?.Contains(efType) == true));
    }

    public static Type[] GetAggregateRootTypes()
    {
        // Look for types that inherit from AggregateRoot or have AggregateRoot in their inheritance hierarchy
        return Types.InCurrentDomain()
            .That()
            .ResideInNamespace(DomainLayer)
            .GetTypes()
            .Where(t => t.BaseType?.Name.Contains("AggregateRoot") == true ||
                       t.Name.Contains("AggregateRoot") ||
                       HasAggregateRootInHierarchy(t))
            .ToArray();
    }

    public static bool HasAggregateRootInHierarchy(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        
        var current = type.BaseType;
        while (current != null && current != typeof(object))
        {
            if (current.Name.Contains("AggregateRoot"))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    public static bool IsAggregateRoot(Type entityType, Type[] aggregateRootTypes)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        
        return aggregateRootTypes.Contains(entityType) ||
               entityType.BaseType?.Name.Contains("AggregateRoot") == true;
    }
}