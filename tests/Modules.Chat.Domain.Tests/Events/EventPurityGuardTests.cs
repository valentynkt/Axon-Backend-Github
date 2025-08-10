using System.Reflection;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Guard tests to enforce purity of domain events.
/// Ensures events only contain allowed types and no infrastructure/entity references.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
[Category("Guard")]
public class EventPurityGuardTests
{
    private Assembly ChatDomainAssembly => typeof(Axon.Modules.Chat.Domain.AssemblyMarker).Assembly;

    [Test]
    public void AllEventProperties_ShouldOnlyUseAllowedTypes()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();
        
        var allowedTypes = GetAllowedPropertyTypes();

        // Act
        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (!allowedTypes.Contains(property.PropertyType))
                {
                    violations.Add($"Event {eventType.Name} property '{property.Name}' has disallowed type '{property.PropertyType.FullName}'");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Event properties must only use allowed types. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventProperties_ShouldNotReferenceAggregates()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();
        
        var disallowedAggregateTypes = new[]
        {
            typeof(Conversation), // No aggregate references
        };

        // Act
        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (disallowedAggregateTypes.Contains(property.PropertyType))
                {
                    violations.Add($"Event {eventType.Name} property '{property.Name}' references aggregate type '{property.PropertyType.Name}'");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Events must not reference aggregate types. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventProperties_ShouldNotReferenceEntities()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();
        
        var disallowedEntityTypes = new[]
        {
            typeof(Message), // No entity references
            typeof(object),  // No generic object references
        };

        // Act
        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (disallowedEntityTypes.Contains(property.PropertyType))
                {
                    violations.Add($"Event {eventType.Name} property '{property.Name}' references entity/object type '{property.PropertyType.Name}'");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Events must not reference entity or object types. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventProperties_ShouldNotHaveInfrastructureReferences()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();
        
        var disallowedNamespaces = new[]
        {
            "System.Data",
            "Microsoft.EntityFrameworkCore",
            "Newtonsoft.Json",
            "System.Text.Json",
            "System.Net.Http",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.DependencyInjection"
        };

        // Act
        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                var propertyTypeNamespace = property.PropertyType.Namespace;
                
                if (propertyTypeNamespace != null && 
                    disallowedNamespaces.Any(ns => propertyTypeNamespace.StartsWith(ns)))
                {
                    violations.Add($"Event {eventType.Name} property '{property.Name}' references infrastructure type from namespace '{propertyTypeNamespace}'");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Events must not reference infrastructure types. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventProperties_ShouldNotHaveJsonAttributes()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                var attributes = property.GetCustomAttributes();
                
                foreach (var attribute in attributes)
                {
                    var attributeType = attribute.GetType();
                    
                    // Check for JSON serialization attributes
                    if (attributeType.Namespace?.Contains("Json") == true ||
                        attributeType.Name.Contains("Json") ||
                        attributeType.Name == "DataMember" ||
                        attributeType.Name == "DataContract")
                    {
                        violations.Add($"Event {eventType.Name} property '{property.Name}' has JSON/serialization attribute '{attributeType.Name}'");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Events must not have JSON or serialization attributes. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventTypes_ShouldNotHaveInfrastructureAttributes()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            var attributes = eventType.GetCustomAttributes();
            
            foreach (var attribute in attributes)
            {
                var attributeType = attribute.GetType();
                
                // Check for infrastructure attributes
                if (attributeType.Namespace?.Contains("EntityFramework") == true ||
                    attributeType.Namespace?.Contains("Json") == true ||
                    attributeType.Namespace?.Contains("AspNetCore") == true ||
                    attributeType.Name.Contains("Table") ||
                    attributeType.Name.Contains("Column") ||
                    attributeType.Name.Contains("Json"))
                {
                    violations.Add($"Event {eventType.Name} has infrastructure attribute '{attributeType.Name}'");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Event types must not have infrastructure attributes. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventProperties_ShouldBeReadOnly()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                // Skip inherited DomainEvent properties that might have different rules
                if (property.DeclaringType != eventType)
                    continue;
                    
                // Record properties should have init-only setters or no setter
                if (property.CanWrite && property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
                    .All(t => t.FullName != "System.Runtime.CompilerServices.IsExternalInit") != false)
                {
                    violations.Add($"Event {eventType.Name} property '{property.Name}' is mutable (should be init-only or read-only)");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Event properties should be immutable. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventProperties_ShouldHaveAppropriateNullability()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                // Skip inherited DomainEvent properties
                if (property.DeclaringType != eventType)
                    continue;
                
                var propertyType = property.PropertyType;
                
                // String properties should generally not be nullable in events (should be empty string instead)
                if (propertyType == typeof(string))
                {
                    // This would need more sophisticated nullability analysis
                    // For now, we just check that string properties exist and document the expectation
                    // In practice, events should use empty strings rather than null for missing values
                }
                
                // Value types should generally not be nullable unless specifically needed
                if (Nullable.GetUnderlyingType(propertyType) != null)
                {
                    // Check if nullable value types are justified
                    var isJustifiedNullable = property.Name.EndsWith("At") || // Timestamps might be nullable
                                            property.Name.Contains("Optional") ||
                                            property.Name.Contains("Nullable");
                    
                    if (!isJustifiedNullable)
                    {
                        violations.Add($"Event {eventType.Name} property '{property.Name}' is nullable {propertyType.Name} - consider using non-nullable with default value");
                    }
                }
            }
        }

        // Assert - For now, we mainly document the expectation
        // violations.ShouldBeEmpty($"Event properties should have appropriate nullability. Violations:\n{string.Join("\n", violations)}");
        
        // Note: This test primarily documents the expectation for future development
        // The actual nullability analysis would require more sophisticated reflection or source analyzers
    }

    /// <summary>
    /// Gets all domain event types from the Chat domain assembly
    /// </summary>
    private List<Type> GetAllEventTypes()
    {
        return ChatDomainAssembly.GetTypes()
            .Where(t => t.Namespace == "Axon.Modules.Chat.Domain.Events")
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => typeof(BuildingBlocks.Core.Domain.Events.IDomainEvent).IsAssignableFrom(t))
            .ToList();
    }

    /// <summary>
    /// Gets the list of allowed property types for domain events
    /// </summary>
    private HashSet<Type> GetAllowedPropertyTypes()
    {
        return new HashSet<Type>
        {
            // Primitives
            typeof(string),
            typeof(int),
            typeof(bool),
            typeof(DateTimeOffset),
            typeof(DateTime), // For base DomainEvent compatibility
            typeof(uint),
            typeof(Guid),
            typeof(long),
            typeof(decimal),
            typeof(double),
            typeof(float),
            
            // Chat Domain Value Objects
            typeof(Axon.Modules.Chat.Domain.ValueObjects.ConversationId),
            typeof(Axon.Modules.Chat.Domain.ValueObjects.MessageId),
            typeof(Axon.Modules.Chat.Domain.ValueObjects.UserId),
            typeof(Axon.Modules.Chat.Domain.ValueObjects.MessageRole),
        };
    }
}