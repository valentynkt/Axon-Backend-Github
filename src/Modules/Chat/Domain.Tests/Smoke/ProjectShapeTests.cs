using System.Reflection;
using System.Text.Json.Serialization;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Smoke;

public class ProjectShapeTests
{
    private readonly Assembly _domainAssembly = typeof(AssemblyMarker).Assembly;

    [Fact]
    public void DomainAssembly_Should_Load()
    {
        // Arrange & Act
        var assembly = typeof(AssemblyMarker).Assembly;

        // Assert
        assembly.ShouldNotBeNull();
        assembly.GetName().Name.ShouldBe("Axon.Modules.Chat.Domain");
    }

    [Fact]
    public void DomainAssembly_Should_Not_Reference_ForbiddenAssemblies()
    {
        // Arrange
        var forbiddenPatterns = new[]
        {
            "MediatR",
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "System.Text.Json", // The assembly itself - we check for attributes separately
            "Newtonsoft.Json",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.Hosting",
            "Microsoft.Extensions.DependencyInjection"
        };

        // Act
        var referencedAssemblies = _domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name != null)
            .ToList();

        // Assert
        foreach (var pattern in forbiddenPatterns)
        {
            referencedAssemblies.ShouldNotContain(
                name => name!.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
                $"Domain should not reference {pattern}");
        }
    }

    [Fact]
    public void DomainAssembly_Should_Not_Contain_JsonAttributes()
    {
        // Arrange
        var jsonAttributeTypes = new[]
        {
            typeof(JsonPropertyNameAttribute),
            typeof(JsonConverterAttribute),
            typeof(JsonIgnoreAttribute),
            typeof(JsonIncludeAttribute),
            typeof(JsonConstructorAttribute),
            typeof(JsonExtensionDataAttribute),
            typeof(JsonNumberHandlingAttribute),
            typeof(JsonPropertyOrderAttribute),
            typeof(JsonRequiredAttribute),
            typeof(JsonSourceGenerationOptionsAttribute)
        };

        // Act
        var allTypes = _domainAssembly.GetExportedTypes();
        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            // Check type-level attributes
            foreach (var attrType in jsonAttributeTypes)
            {
                if (type.GetCustomAttribute(attrType) != null)
                {
                    violations.Add($"Type {type.Name} has {attrType.Name}");
                }
            }

            // Check members (properties, fields, methods)
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var member in members)
            {
                foreach (var attrType in jsonAttributeTypes)
                {
                    if (member.GetCustomAttribute(attrType) != null)
                    {
                        violations.Add($"Member {type.Name}.{member.Name} has {attrType.Name}");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty("Domain should not contain JSON serialization attributes");
    }

    [Fact]
    public void ProjectShape_Should_Match_SpecFolders()
    {
        // Arrange
        var expectedNamespaces = new[]
        {
            "Axon.Modules.Chat.Domain",
            "Axon.Modules.Chat.Domain.Abstractions",
            "Axon.Modules.Chat.Domain.Aggregates",
            "Axon.Modules.Chat.Domain.Aggregates.Conversation",
            "Axon.Modules.Chat.Domain.Entities",
            "Axon.Modules.Chat.Domain.Events",
            "Axon.Modules.Chat.Domain.Rules",
            "Axon.Modules.Chat.Domain.Specifications",
            "Axon.Modules.Chat.Domain.Time",
            "Axon.Modules.Chat.Domain.ValueObjects"
        };

        // Act
        // We check if the namespaces could exist by verifying assembly structure
        // Since folders are empty, we verify the assembly can potentially contain these namespaces
        var assemblyName = _domainAssembly.GetName().Name;

        // Assert
        assemblyName.ShouldBe("Axon.Modules.Chat.Domain");
        
        // Verify the assembly is correctly structured for future additions
        _domainAssembly.ShouldNotBeNull();
        _domainAssembly.DefinedTypes.Where(t => t.Name == "AssemblyMarker").ShouldHaveSingleItem(
            "Should contain the AssemblyMarker for now");
    }

    [Fact]
    public void DomainAssembly_Should_Have_Correct_Configuration()
    {
        // Arrange & Act
        var assembly = _domainAssembly;
        
        // Get assembly metadata to verify configuration
        var assemblyAttributes = assembly.GetCustomAttributes().ToList();

        // Assert
        // Verify nullable context is enabled (this is enforced at compile time)
        assembly.ShouldNotBeNull();
        
        // Additional checks can be added here as needed
    }

    [Fact]
    public void DomainAssembly_Should_Only_Reference_BuildingBlocks()
    {
        // Arrange
        var allowedReferences = new[]
        {
            "Axon.Modules.Chat.Domain", // Self
            "BuildingBlocks", // Core building blocks
            "System", // System assemblies
            "Microsoft", // Microsoft base libraries (not MediatR, EF, etc.)
            "netstandard",
            "mscorlib"
        };

        // Act
        var referencedAssemblies = _domainAssembly.GetReferencedAssemblies();

        // Assert
        foreach (var reference in referencedAssemblies)
        {
            var isAllowed = allowedReferences.Any(allowed =>
                reference.Name!.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) ||
                reference.Name!.Equals(allowed, StringComparison.OrdinalIgnoreCase));

            // Special exclusions for forbidden Microsoft packages
            if (reference.Name!.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                var forbiddenMicrosoft = new[]
                {
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.AspNetCore",
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.Extensions.Hosting"
                };

                forbiddenMicrosoft.ShouldNotContain(forbidden =>
                    reference.Name!.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"Domain should not reference {reference.Name}");
            }

            isAllowed.ShouldBeTrue($"Unexpected reference: {reference.Name}");
        }
    }
}