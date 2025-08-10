using System.Reflection;

namespace Axon.Modules.Chat.Domain.Tests.ArchitecturalGuards;

/// <summary>
/// Tests to ensure only whitelisted types are public in the Chat domain.
/// This prevents accidental exposure of internal implementation details.
/// </summary>
[TestFixture]
public class PublicSurfaceWhitelistTests
{
    /// <summary>
    /// Whitelisted public types that form the stable public API of the Chat domain.
    /// </summary>
    private static readonly HashSet<string> WhitelistedPublicTypes = new()
    {
        // Module marker
        "Axon.Modules.Chat.Domain.AssemblyMarker",
        
        // Aggregates
        "Axon.Modules.Chat.Domain.Aggregates.Conversation.Conversation",
        "Axon.Modules.Chat.Domain.Aggregates.Conversation.ConversationStatus",
        
        // Child entity
        "Axon.Modules.Chat.Domain.Entities.Message",
        
        // Value Objects
        "Axon.Modules.Chat.Domain.ValueObjects.ConversationTitle",
        "Axon.Modules.Chat.Domain.ValueObjects.MessageContent", 
        "Axon.Modules.Chat.Domain.ValueObjects.MessageRole",
        
        // Strong IDs
        "Axon.Modules.Chat.Domain.ValueObjects.ConversationId",
        "Axon.Modules.Chat.Domain.ValueObjects.MessageId",
        "Axon.Modules.Chat.Domain.ValueObjects.UserId",
        
        // Domain Events
        "Axon.Modules.Chat.Domain.Events.ConversationStartedEvent",
        "Axon.Modules.Chat.Domain.Events.UserMessageAppendedEvent",
        "Axon.Modules.Chat.Domain.Events.AssistantMessageAppendedEvent",
        "Axon.Modules.Chat.Domain.Events.ConversationTitleUpdatedEvent", 
        "Axon.Modules.Chat.Domain.Events.ConversationCompletedEvent",
        
        // Time abstractions
        "Axon.Modules.Chat.Domain.Time.IClock",
        "Axon.Modules.Chat.Domain.Time.SystemClock"
    };

    [Test]
    public void ChatDomain_ShouldOnlyExposeWhitelistedPublicTypes()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        
        // Act - Get all public types
        var publicTypes = assembly.GetTypes()
            .Where(type => type.IsPublic)
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToList();
            
        // Assert - All public types must be whitelisted
        var unauthorizedPublicTypes = publicTypes
            .Where(typeName => !WhitelistedPublicTypes.Contains(typeName))
            .ToList();
            
        if (unauthorizedPublicTypes.Any())
        {
            var unauthorizedList = string.Join("\n  - ", unauthorizedPublicTypes);
            Assert.Fail($"Found unauthorized public types in Chat domain:\n  - {unauthorizedList}\n\n" +
                       "These types should either be marked as 'internal' or added to the whitelist " +
                       "if they are intended to be part of the stable public API.");
        }
        
        // Also verify all whitelisted types actually exist and are public
        var missingTypes = WhitelistedPublicTypes
            .Where(typeName => !publicTypes.Contains(typeName))
            .ToList();
            
        if (missingTypes.Any())
        {
            var missingList = string.Join("\n  - ", missingTypes);
            Assert.Fail($"Whitelisted types not found as public in Chat domain:\n  - {missingList}\n\n" +
                       "Either the types were renamed/removed or their visibility changed. " +
                       "Update the whitelist accordingly.");
        }
    }

    [Test]  
    public void ChatDomain_PublicTypes_ShouldNotExposePublicSetters()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var publicTypes = assembly.GetTypes()
            .Where(type => type.IsPublic && WhitelistedPublicTypes.Contains(type.FullName!))
            .ToList();
            
        var violations = new List<string>();
        
        // Act & Assert
        foreach (var type in publicTypes)
        {
            var publicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(prop => prop.CanWrite && prop.SetMethod?.IsPublic == true)
                .ToList();
                
            foreach (var property in publicProperties)
            {
                // Allow public setters only on value object/strong ID constructors (init-only)
                if (property.SetMethod?.IsInitOnly != true)
                {
                    violations.Add($"{type.FullName}.{property.Name}");
                }
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Found public properties with mutable setters:\n  - {violationList}\n\n" +
                       "Domain objects should be immutable. Use private setters or init-only properties.");
        }
    }

    [Test]
    public void ChatDomain_PublicTypes_ShouldNotExposeMutableCollections()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var publicTypes = assembly.GetTypes()
            .Where(type => type.IsPublic && WhitelistedPublicTypes.Contains(type.FullName!))
            .ToList();
            
        var violations = new List<string>();
        
        // Act & Assert  
        foreach (var type in publicTypes)
        {
            var publicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToList();
                
            foreach (var property in publicProperties)
            {
                var propertyType = property.PropertyType;
                
                // Check if property exposes mutable collection types
                if (IsMutableCollectionType(propertyType))
                {
                    violations.Add($"{type.FullName}.{property.Name} exposes {propertyType.Name}");
                }
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"Found properties exposing mutable collections:\n  - {violationList}\n\n" +
                       "Use IReadOnlyList<T>, IReadOnlyCollection<T>, or IReadOnlyDictionary<K,V> instead.");
        }
    }

    private static bool IsMutableCollectionType(Type type)
    {
        // Check for specific mutable collection types
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef == typeof(List<>) ||
                   genericTypeDef == typeof(Dictionary<,>) ||
                   genericTypeDef == typeof(HashSet<>) ||
                   genericTypeDef == typeof(Collection<>);
        }
        
        return false;
    }
}