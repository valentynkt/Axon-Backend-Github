using System.Reflection;
using Shouldly;
using PublicApiGenerator;
using Axon.Modules.Chat.Domain;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Smoke;

/// <summary>
/// API Surface Guard Tests for Chat Domain public contract.
/// These tests ensure the public API surface remains stable and only exposes intended types.
/// </summary>
public class ApiSurfaceTests
{
    private readonly Assembly _domainAssembly = typeof(AssemblyMarker).Assembly;
    
    [Fact]
    public void PublicApi_Should_Match_ApprovedSnapshot()
    {
        // Arrange
        var publicApi = _domainAssembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false,
            AllowNamespacePrefixes = new[] { "Axon.Modules.Chat.Domain" }
        });
        
        var expectedSnapshotPath = Path.Combine(
            Path.GetDirectoryName(typeof(ApiSurfaceTests).Assembly.Location)!,
            "..", "..", "..", "..", 
            "Smoke", 
            "PublicApi.approved.txt");
        
        expectedSnapshotPath = Path.GetFullPath(expectedSnapshotPath);

        // Act & Assert
        if (File.Exists(expectedSnapshotPath))
        {
            var expectedApi = File.ReadAllText(expectedSnapshotPath);
            publicApi.ShouldBe(expectedApi, 
                "Public API has changed. If this is intentional, update the snapshot file at: " + expectedSnapshotPath);
        }
        else
        {
            // First run - create the snapshot file
            Directory.CreateDirectory(Path.GetDirectoryName(expectedSnapshotPath)!);
            File.WriteAllText(expectedSnapshotPath, publicApi);
            
            publicApi.ShouldNotBeNullOrEmpty("Generated API snapshot and saved to: " + expectedSnapshotPath);
        }
    }

    [Fact]
    public void PublicTypes_Should_Only_Be_WhitelistedTypes()
    {
        // Arrange - Whitelisted public types per CH-DOM-012 policy
        var allowedPublicTypes = new HashSet<string>
        {
            // Aggregates
            "Conversation",
            "ConversationStatus",
            
            // Child entities
            "Message",
            
            // Value Objects
            "ConversationTitle",
            "MessageContent", 
            "MessageRole",
            
            // Strong IDs
            "ConversationId",
            "MessageId",
            "UserId",
            
            // Domain Events
            "ConversationStartedEvent",
            "UserMessageAppendedEvent",
            "AssistantMessageAppendedEvent",
            "ConversationTitleUpdatedEvent",
            "ConversationCompletedEvent",
            
            // Time abstractions
            "IClock",
            "SystemClock"
        };

        // Act
        var publicTypes = _domainAssembly.GetExportedTypes()
            .Where(t => t.IsPublic)
            .Select(t => t.Name)
            .ToList();

        // Assert
        foreach (var publicType in publicTypes)
        {
            allowedPublicTypes.ShouldContain(publicType,
                $"Type '{publicType}' is public but not in the whitelist. " +
                "Either mark it internal or add to whitelist if it's part of the public contract.");
        }
        
        // Verify we're not missing expected public types
        var missingTypes = allowedPublicTypes.Except(publicTypes).ToList();
        missingTypes.ShouldBeEmpty(
            "Expected public types are missing from the assembly. " +
            "Verify the types exist and are marked public: {0}",
            string.Join(", ", missingTypes));
    }

    [Fact]
    public void PublicTypes_Should_Not_Expose_MutableState()
    {
        // Arrange
        var publicTypes = _domainAssembly.GetExportedTypes().Where(t => t.IsPublic).ToList();
        var violations = new List<string>();

        // Act
        foreach (var type in publicTypes)
        {
            var publicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in publicProperties)
            {
                // Check for public setters
                if (property.SetMethod?.IsPublic == true)
                {
                    violations.Add($"Type {type.Name} has public setter on property {property.Name}");
                }

                // Check for exposed mutable collections (should be IReadOnlyList<T>, not List<T>)
                if (property.PropertyType.IsGenericType)
                {
                    var genericTypeDefinition = property.PropertyType.GetGenericTypeDefinition();
                    if (genericTypeDefinition == typeof(List<>) || 
                        genericTypeDefinition == typeof(IList<>) ||
                        genericTypeDefinition == typeof(ICollection<>))
                    {
                        violations.Add($"Type {type.Name} exposes mutable collection {property.Name} of type {property.PropertyType.Name}. Use IReadOnlyList<T> instead.");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty("Public types should not expose mutable state:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void PublicAggregatesAndEntities_Should_Not_Have_PublicParameterlessConstructors()
    {
        // Arrange - Types that should not have public parameterless constructors
        var aggregateAndEntityTypes = _domainAssembly.GetExportedTypes()
            .Where(t => t.IsPublic && t.IsClass && !t.IsAbstract)
            .Where(t => 
                t.Name == "Conversation" || 
                t.Name == "Message" ||
                IsEntity(t))
            .ToList();

        var violations = new List<string>();

        // Act
        foreach (var type in aggregateAndEntityTypes)
        {
            var parameterlessConstructor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance, 
                null, 
                Type.EmptyTypes, 
                null);

            if (parameterlessConstructor != null)
            {
                violations.Add($"Type {type.Name} has a public parameterless constructor");
            }
        }

        // Assert  
        violations.ShouldBeEmpty(
            "Aggregates and entities should not have public parameterless constructors. " +
            "Use factory methods or private/internal constructors for ORM:\n" + 
            string.Join("\n", violations));
    }

    private static bool IsEntity(Type type)
    {
        // Check if type inherits from Entity<T> or AggregateRoot<T>
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType)
            {
                var genericTypeDef = baseType.GetGenericTypeDefinition();
                if (genericTypeDef.Name.Contains("Entity") || genericTypeDef.Name.Contains("AggregateRoot"))
                {
                    return true;
                }
            }
            baseType = baseType.BaseType;
        }
        return false;
    }
}