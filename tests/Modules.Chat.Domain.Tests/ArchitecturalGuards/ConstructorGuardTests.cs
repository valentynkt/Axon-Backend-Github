using System.Reflection;

namespace Axon.Modules.Chat.Domain.Tests.ArchitecturalGuards;

/// <summary>
/// Tests to ensure proper constructor discipline in the Chat domain.
/// Prevents accidental exposure of public parameterless constructors on aggregates/entities.
/// </summary>
[TestFixture]
public class ConstructorGuardTests
{
    /// <summary>
    /// Types that are allowed to have public parameterless constructors (none for aggregates/entities).
    /// Value objects and IDs may have private constructors + public factory methods.
    /// </summary>
    private static readonly HashSet<string> AllowedPublicParameterlessConstructors = new()
    {
        // Time abstractions (simple implementations)
        "Axon.Modules.Chat.Domain.Time.SystemClock",
        
        // Module marker (static class marker)
        "Axon.Modules.Chat.Domain.AssemblyMarker"
    };

    [Test]
    public void ChatDomain_AggregatesAndEntities_ShouldNotHavePublicParameterlessConstructors()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var publicTypes = assembly.GetTypes()
            .Where(type => type.IsPublic)
            .ToList();
            
        var violations = new List<string>();
        
        // Act & Assert
        foreach (var type in publicTypes)
        {
            // Skip types that are allowed to have public parameterless constructors
            if (AllowedPublicParameterlessConstructors.Contains(type.FullName!))
                continue;
                
            // Skip static classes, enums, interfaces
            if (type.IsAbstract && type.IsSealed) // static class
                continue;
            if (type.IsEnum)
                continue;  
            if (type.IsInterface)
                continue;
                
            // Check for public parameterless constructor
            var parameterlessConstructor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance, 
                null, 
                Type.EmptyTypes, 
                null);
                
            if (parameterlessConstructor != null)
            {
                violations.Add($"{type.FullName} has a public parameterless constructor");
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Found types with prohibited public parameterless constructors:\n  - {violationList}\n\n" +
                       "Aggregates and entities should use factory methods or private constructors. " +
                       "This ensures proper validation and prevents invalid state creation. " +
                       "Value objects should use private constructors + public Create() factory methods.");
        }
    }

    [Test]
    public void ChatDomain_ValueObjectsAndStrongIds_ShouldUseFactoryPattern()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var valueObjectTypes = assembly.GetTypes()
            .Where(type => type.IsPublic && (
                IsValueObjectType(type) || IsStrongIdType(type)))
            .ToList();
            
        var violations = new List<string>();
        
        // Act & Assert
        foreach (var type in valueObjectTypes)
        {
            var hasPublicCreateMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(method => method.Name == "Create" || method.Name == "New");
                
            if (!hasPublicCreateMethod)
            {
                violations.Add($"{type.FullName} missing public Create() or New() factory method");
            }
            
            // Verify constructors are not public (except for record primary constructors on StrongIds)
            var publicConstructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(ctor => !IsRecordPrimaryConstructor(ctor))
                .ToList();
                
            if (publicConstructors.Any())
            {
                violations.Add($"{type.FullName} has public constructors (should use factory pattern)");
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Value objects and Strong IDs should follow factory pattern:\n  - {violationList}\n\n" +
                       "Use private constructors with public Create()/New() factory methods for validation and immutability.");
        }
    }

    [Test]
    public void ChatDomain_Aggregates_ShouldUseFactoryMethods()
    {
        // Arrange - Find aggregate root types
        var assembly = typeof(AssemblyMarker).Assembly;
        var aggregateTypes = assembly.GetTypes()
            .Where(type => type.IsPublic && IsAggregateRootType(type))
            .ToList();
            
        var violations = new List<string>();
        
        // Act & Assert
        foreach (var type in aggregateTypes)
        {
            // Verify no public constructors
            var publicConstructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .ToList();
                
            if (publicConstructors.Any())
            {
                violations.Add($"{type.FullName} has public constructors (should use static factory methods)");
            }
            
            // Verify has static factory methods (like Start, Create, etc.)
            var staticFactoryMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.ReturnType.IsGenericType && 
                               method.ReturnType.GetGenericTypeDefinition().Name.Contains("Result"))
                .ToList();
                
            if (!staticFactoryMethods.Any())
            {
                violations.Add($"{type.FullName} missing static factory methods returning Result<T>");
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Aggregate roots should follow factory pattern:\n  - {violationList}\n\n" +
                       "Use private constructors with public static factory methods (Start, Create, etc.) " +
                       "that return Result<T> for validation and controlled creation.");
        }
    }

    private static bool IsValueObjectType(Type type)
    {
        return type.BaseType?.Name == "ValueObject";
    }

    private static bool IsStrongIdType(Type type)
    {
        return type.BaseType?.IsGenericType == true &&
               type.BaseType.GetGenericTypeDefinition().Name == "StrongId`1";
    }

    private static bool IsAggregateRootType(Type type)
    {
        return type.BaseType?.IsGenericType == true &&
               type.BaseType.GetGenericTypeDefinition().Name == "AggregateRoot`1";
    }

    private static bool IsRecordPrimaryConstructor(ConstructorInfo constructor)
    {
        // Record primary constructors are allowed for StrongId pattern
        var declaringType = constructor.DeclaringType!;
        return declaringType.IsValueType == false && 
               declaringType.BaseType?.IsGenericType == true &&
               declaringType.BaseType.GetGenericTypeDefinition().Name == "StrongId`1";
    }
}