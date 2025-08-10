using System.Reflection;
using BuildingBlocks.Core.Domain.Events;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Tests to verify the structural contract of all domain events.
/// Ensures events conform to the canonical schema and maintain stability.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
[Category("Contract")]
public class EventShapeTests
{
    private readonly Type[] _eventTypes = 
    [
        typeof(ConversationStartedEvent),
        typeof(UserMessageAppendedEvent),
        typeof(AssistantMessageAppendedEvent),
        typeof(ConversationTitleUpdatedEvent),
        typeof(ConversationCompletedEvent)
    ];

    [Test]
    public void AllEvents_ShouldDeriveDomainEvent()
    {
        foreach (var eventType in _eventTypes)
        {
            eventType.Should().BeAssignableTo<DomainEvent>(
                $"Event {eventType.Name} must derive from DomainEvent");
        }
    }

    [Test]
    public void AllEvents_ShouldBeSealed()
    {
        foreach (var eventType in _eventTypes)
        {
            eventType.IsSealed.ShouldBeTrue($"Event {eventType.Name} must be sealed");
        }
    }

    [Test]
    public void AllEvents_ShouldBeRecords()
    {
        foreach (var eventType in _eventTypes)
        {
            // Records have a generated <Clone>$ method and inherit from record base types
            var hasCloneMethod = eventType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => m.Name.Contains("Clone"));
            var isRecord = hasCloneMethod || eventType.BaseType?.Name == "DomainEvent";
            
            isRecord.ShouldBeTrue($"Event {eventType.Name} must be a record type");
        }
    }

    [Test]
    public void AllEvents_ShouldHaveEventSuffix()
    {
        foreach (var eventType in _eventTypes)
        {
            eventType.Name.Should().EndWith("Event", 
                $"Event {eventType.Name} must end with 'Event' suffix");
        }
    }

    [Test]
    public void AllEvents_ShouldHaveVersionOne()
    {
        // This tests the default version value from DomainEvent base class
        var testEvent = new ConversationStartedEvent(
            new(Guid.NewGuid()), 
            new(Guid.NewGuid()), 
            "test", 
            false, 
            DateTimeOffset.UtcNow);

        testEvent.Version.ShouldBe(1);
    }

    [Test]
    public void AllEvents_ShouldNotExposeDisallowedTypes()
    {
        var disallowedTypes = new[]
        {
            typeof(Conversation),  // No aggregate references
            typeof(Message),       // No entity references
            typeof(object),        // Must be specific types
        };

        foreach (var eventType in _eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                foreach (var disallowedType in disallowedTypes)
                {
                    property.PropertyType.Should().NotBe(disallowedType,
                        $"Event {eventType.Name} property {property.Name} cannot be of type {disallowedType.Name}");
                }
            }
        }
    }

    [Test]
    public void AllEvents_ShouldOnlyUseAllowedPropertyTypes()
    {
        var allowedTypes = new[]
        {
            // Primitives
            typeof(string),
            typeof(int),
            typeof(bool),
            typeof(DateTimeOffset),
            typeof(uint),
            typeof(Guid),
            
            // Value Objects from Chat Domain
            typeof(Axon.Modules.Chat.Domain.ValueObjects.ConversationId),
            typeof(Axon.Modules.Chat.Domain.ValueObjects.MessageId),
            typeof(Axon.Modules.Chat.Domain.ValueObjects.UserId),
            typeof(Axon.Modules.Chat.Domain.ValueObjects.MessageRole),
            
            // DomainEvent base properties
            typeof(DateTime), // For base DomainEvent properties
        };

        foreach (var eventType in _eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                allowedTypes.Should().Contain(property.PropertyType,
                    $"Event {eventType.Name} property {property.Name} has disallowed type {property.PropertyType.Name}");
            }
        }
    }

    [Test]
    public void ConversationStartedEvent_ShouldHaveCanonicalShape()
    {
        var eventType = typeof(ConversationStartedEvent);
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        // Should have exactly these properties (excluding inherited DomainEvent properties)
        var expectedProperties = new Dictionary<string, Type>
        {
            { "ConversationId", typeof(Axon.Modules.Chat.Domain.ValueObjects.ConversationId) },
            { "OwnerId", typeof(Axon.Modules.Chat.Domain.ValueObjects.UserId) },
            { "Title", typeof(string) },
            { "IsDefaultTitle", typeof(bool) },
            { "StartedAt", typeof(DateTimeOffset) }
        };

        properties.Length.ShouldBe(expectedProperties.Count);
        
        foreach (var expected in expectedProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == expected.Key);
            property.ShouldNotBeNull($"ConversationStartedEvent should have {expected.Key} property");
            property!.PropertyType.ShouldBe(expected.Value);
        }
    }

    [Test]
    public void MessageAppendedEvents_ShouldHaveCanonicalShape()
    {
        var eventTypes = new[] { typeof(UserMessageAppendedEvent), typeof(AssistantMessageAppendedEvent) };
        
        var expectedProperties = new Dictionary<string, Type>
        {
            { "ConversationId", typeof(Axon.Modules.Chat.Domain.ValueObjects.ConversationId) },
            { "MessageId", typeof(Axon.Modules.Chat.Domain.ValueObjects.MessageId) },
            { "Sequence", typeof(int) },
            { "ContentPreview", typeof(string) },
            { "CreatedAt", typeof(DateTimeOffset) }
        };

        foreach (var eventType in eventTypes)
        {
            var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
            properties.Length.ShouldBe(expectedProperties.Count);
            
            foreach (var expected in expectedProperties)
            {
                var property = properties.FirstOrDefault(p => p.Name == expected.Key);
                property.ShouldNotBeNull($"{eventType.Name} should have {expected.Key} property");
                property!.PropertyType.ShouldBe(expected.Value);
            }
        }
    }

    [Test]
    public void ConversationTitleUpdatedEvent_ShouldHaveCanonicalShape()
    {
        var eventType = typeof(ConversationTitleUpdatedEvent);
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        var expectedProperties = new Dictionary<string, Type>
        {
            { "ConversationId", typeof(Axon.Modules.Chat.Domain.ValueObjects.ConversationId) },
            { "Title", typeof(string) },
            { "IsDefaultTitle", typeof(bool) },
            { "UpdatedAt", typeof(DateTimeOffset) }
        };

        properties.Length.ShouldBe(expectedProperties.Count);
        
        foreach (var expected in expectedProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == expected.Key);
            property.ShouldNotBeNull($"ConversationTitleUpdatedEvent should have {expected.Key} property");
            property!.PropertyType.ShouldBe(expected.Value);
        }
    }

    [Test]
    public void ConversationCompletedEvent_ShouldHaveCanonicalShape()
    {
        var eventType = typeof(ConversationCompletedEvent);
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        var expectedProperties = new Dictionary<string, Type>
        {
            { "ConversationId", typeof(Axon.Modules.Chat.Domain.ValueObjects.ConversationId) },
            { "MessageCount", typeof(int) },
            { "CompletedAt", typeof(DateTimeOffset) }
        };

        properties.Length.ShouldBe(expectedProperties.Count);
        
        foreach (var expected in expectedProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == expected.Key);
            property.ShouldNotBeNull($"ConversationCompletedEvent should have {expected.Key} property");
            property!.PropertyType.ShouldBe(expected.Value);
        }
    }
}