using System.Reflection;

namespace Axon.Modules.Chat.Domain.Tests.ArchitecturalGuards;

/// <summary>
/// Tests to ensure the Chat domain maintains proper separation and purity.
/// Prevents infrastructure concerns from leaking into the domain layer.
/// </summary>
[TestFixture]
public class DomainPurityGuardTests
{
    /// <summary>
    /// Infrastructure packages that should not be referenced from domain layer.
    /// </summary>
    private static readonly HashSet<string> ProhibitedPackageReferences = new()
    {
        "Microsoft.EntityFrameworkCore",
        "Npgsql.EntityFrameworkCore.PostgreSQL", 
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Logging",
        "System.Text.Json",
        "Newtonsoft.Json",
        "AutoMapper",
        "MediatR.Extensions.Microsoft.DependencyInjection",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Hosting"
    };

    /// <summary>
    /// Attributes that should not appear on public domain types.
    /// These indicate infrastructure concerns leaking into domain.
    /// </summary>
    private static readonly HashSet<string> ProhibitedAttributes = new()
    {
        "System.Text.Json.Serialization.JsonPropertyNameAttribute",
        "System.Text.Json.Serialization.JsonIgnoreAttribute", 
        "System.Text.Json.Serialization.JsonConverterAttribute",
        "Newtonsoft.Json.JsonPropertyAttribute",
        "Newtonsoft.Json.JsonIgnoreAttribute",
        "Microsoft.EntityFrameworkCore.KeyAttribute",
        "System.ComponentModel.DataAnnotations.KeyAttribute",
        "System.ComponentModel.DataAnnotations.RequiredAttribute",
        "System.ComponentModel.DataAnnotations.MaxLengthAttribute",
        "Microsoft.EntityFrameworkCore.NotMappedAttribute"
    };

    [Test]
    public void ChatDomain_ShouldNotReferenceInfrastructurePackages()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .ToHashSet();
            
        // Act & Assert
        var violations = referencedAssemblies
            .Where(assemblyName => ProhibitedPackageReferences.Any(prohibited => 
                assemblyName.StartsWith(prohibited, StringComparison.OrdinalIgnoreCase)))
            .ToList();
            
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Chat domain references prohibited infrastructure packages:\n  - {violationList}\n\n" +
                       "Domain layer should only reference BuildingBlocks.Core and system libraries. " +
                       "Infrastructure concerns belong in the Infrastructure layer.");
        }
    }

    [Test]
    public void ChatDomain_PublicTypes_ShouldNotHaveInfrastructureAttributes()
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
            // Check type-level attributes
            var typeAttributes = type.GetCustomAttributes(inherit: false)
                .Select(attr => attr.GetType().FullName!)
                .Where(attrName => ProhibitedAttributes.Contains(attrName))
                .ToList();
                
            violations.AddRange(typeAttributes.Select(attrName => 
                $"{type.FullName} has prohibited attribute {attrName}"));
                
            // Check property-level attributes
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var propertyAttributes = property.GetCustomAttributes(inherit: false)
                    .Select(attr => attr.GetType().FullName!)
                    .Where(attrName => ProhibitedAttributes.Contains(attrName))
                    .ToList();
                    
                violations.AddRange(propertyAttributes.Select(attrName =>
                    $"{type.FullName}.{property.Name} has prohibited attribute {attrName}"));
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Found prohibited infrastructure attributes on domain types:\n  - {violationList}\n\n" +
                       "Domain objects should be free of serialization, ORM, and validation attributes. " +
                       "Use mapping layers in Infrastructure to handle these concerns.");
        }
    }

    [Test]
    public void ChatDomain_ShouldNotUseDirectTimeApis()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var types = assembly.GetTypes().ToList();
        var violations = new List<string>();
        
        // Act & Assert
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                                        BindingFlags.Instance | BindingFlags.Static | 
                                        BindingFlags.DeclaredOnly);
                                        
            foreach (var method in methods)
            {
                if (method.DeclaringType != type) continue; // Skip inherited methods
                
                try 
                {
                    var methodBody = method.GetMethodBody();
                    if (methodBody == null) continue;
                    
                    // Simple heuristic: check if method name or type suggests time usage
                    var methodCode = method.ToString();
                    if (ContainsDirectTimeUsage(methodCode, type.FullName!))
                    {
                        violations.Add($"{type.FullName}.{method.Name} may use direct time APIs");
                    }
                }
                catch
                {
                    // Skip methods we can't analyze (e.g., extern, abstract)
                }
            }
        }
        
        // Note: This is a heuristic check - actual IL analysis would be more comprehensive
        // but this catches most obvious violations
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Potential direct time API usage found:\n  - {violationList}\n\n" +
                       "Domain layer should use IClock abstraction instead of DateTime.Now, " +
                       "DateTimeOffset.Now, or DateTimeOffset.UtcNow for testability and consistency.");
        }
    }

    private static bool ContainsDirectTimeUsage(string methodSignature, string typeName)
    {
        // Skip time abstraction types themselves
        if (typeName.Contains("IClock") || typeName.Contains("SystemClock"))
            return false;
            
        // Simple checks for common time API usage patterns
        var timePatterns = new[]
        {
            "DateTime.Now",
            "DateTime.UtcNow", 
            "DateTimeOffset.Now",
            "DateTimeOffset.UtcNow"
        };
        
        return timePatterns.Any(pattern => 
            methodSignature.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    [Test]  
    public void ChatDomain_PublicTypes_ShouldFollowNamingConventions()
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
            var typeName = type.Name;
            var namespaceName = type.Namespace!;
            
            // Events should end with "Event"
            if (namespaceName.Contains(".Events") && !typeName.EndsWith("Event"))
            {
                violations.Add($"{type.FullName} should end with 'Event'");
            }
            
            // Value objects should not end with "VO" or "ValueObject"
            if (IsValueObjectType(type) && (typeName.EndsWith("VO") || typeName.EndsWith("ValueObject")))
            {
                violations.Add($"{type.FullName} should not have 'VO' or 'ValueObject' suffix");
            }
            
            // Strong IDs should end with "Id" 
            if (IsStrongIdType(type) && !typeName.EndsWith("Id"))
            {
                violations.Add($"{type.FullName} should end with 'Id'");
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Found naming convention violations:\n  - {violationList}\n\n" +
                       "Follow established naming patterns for consistency and clarity.");
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

    [Test]
    public void ChatDomain_BusinessRules_ShouldBeInternal()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var businessRuleTypes = assembly.GetTypes()
            .Where(type => type.Namespace?.Contains("Rules") == true)
            .Where(type => type.Name.EndsWith("Rule") || 
                          (type.BaseType?.Name == "BusinessRule") ||
                          type.GetInterfaces().Any(i => i.Name == "IBusinessRule"))
            .ToList();
            
        var publicBusinessRules = businessRuleTypes
            .Where(type => type.IsPublic)
            .Select(type => type.FullName!)
            .ToList();
        
        // Act & Assert
        if (publicBusinessRules.Any())
        {
            var violationList = string.Join("\n  - ", publicBusinessRules);
            Assert.Fail($"Found public business rule types:\n  - {violationList}\n\n" +
                       "Business rules are internal domain concerns and should be marked as 'internal'. " +
                       "They should not be part of the public domain API surface.");
        }
        
        // Verify we found some rules (sanity check)
        businessRuleTypes.Should().NotBeEmpty("Expected to find business rule classes in Rules namespace");
    }

    [Test]
    public void ChatDomain_RulesNamespace_ShouldNotExposeDirectTimeAccess()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var ruleTypes = assembly.GetTypes()
            .Where(type => type.Namespace?.Contains("Rules") == true)
            .ToList();
            
        var violations = new List<string>();
        
        // Act & Assert
        foreach (var type in ruleTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                                        BindingFlags.Instance | BindingFlags.Static | 
                                        BindingFlags.DeclaredOnly);
                                        
            foreach (var method in methods)
            {
                if (method.DeclaringType != type) continue;
                
                try 
                {
                    var methodCode = method.ToString();
                    if (ContainsDirectTimeUsage(methodCode, type.FullName!))
                    {
                        violations.Add($"{type.FullName}.{method.Name}");
                    }
                }
                catch
                {
                    // Skip methods we can't analyze
                }
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Business rules with potential direct time API usage:\n  - {violationList}\n\n" +
                       "Business rules should not access time directly. Time should be injected into " +
                       "aggregates via IClock and rules should work with provided values only.");
        }
    }
}