using System.Reflection;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Guard tests to enforce naming conventions for domain events.
/// Ensures all events follow the canonical naming and type patterns.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
[Category("Guard")]
public class EventNamingGuardTests
{
    private Assembly ChatDomainAssembly => typeof(Axon.Modules.Chat.Domain.AssemblyMarker).Assembly;

    [Test]
    public void AllEventTypes_ShouldEndWithEventSuffix()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            if (!eventType.Name.EndsWith("Event"))
            {
                violations.Add($"Type {eventType.FullName} does not end with 'Event'");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"All event types must end with 'Event' suffix. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventTypes_ShouldBeSealedRecords()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            // Check if sealed
            if (!eventType.IsSealed)
            {
                violations.Add($"Event {eventType.Name} is not sealed");
            }

            // Check if it's a record (records inherit from record base types or have specific characteristics)
            var isRecord = IsRecordType(eventType);
            if (!isRecord)
            {
                violations.Add($"Event {eventType.Name} is not a record type");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"All event types must be sealed records. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventTypes_ShouldBeInEventsNamespace()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();
        const string expectedNamespace = "Axon.Modules.Chat.Domain.Events";

        // Act
        foreach (var eventType in eventTypes)
        {
            if (eventType.Namespace != expectedNamespace)
            {
                violations.Add($"Event {eventType.Name} is in namespace '{eventType.Namespace}' but should be in '{expectedNamespace}'");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"All event types must be in the Events namespace. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventTypes_ShouldHavePublicVisibility()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            if (!eventType.IsPublic)
            {
                violations.Add($"Event {eventType.Name} is not public");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"All event types must be public. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AllEventTypes_ShouldFollowPascalCaseNaming()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var eventType in eventTypes)
        {
            var name = eventType.Name;
            
            // Check if first character is uppercase (PascalCase)
            if (!char.IsUpper(name[0]))
            {
                violations.Add($"Event {name} does not start with uppercase letter (PascalCase)");
            }
            
            // Check for underscores (should not be present in PascalCase)
            if (name.Contains('_'))
            {
                violations.Add($"Event {name} contains underscores (should use PascalCase)");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"All event types must follow PascalCase naming. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void EventTypes_ShouldHaveDescriptiveNames()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();
        var minNameLength = 5; // Minimum reasonable length for an event name

        // Act
        foreach (var eventType in eventTypes)
        {
            var nameWithoutSuffix = eventType.Name.Replace("Event", "");
            
            if (nameWithoutSuffix.Length < minNameLength)
            {
                violations.Add($"Event {eventType.Name} has too short a descriptive name ('{nameWithoutSuffix}')");
            }
            
            // Check for generic/vague names
            var vagueNames = new[] { "Data", "Info", "Item", "Thing", "Object", "Generic" };
            if (vagueNames.Any(vague => nameWithoutSuffix.Contains(vague)))
            {
                violations.Add($"Event {eventType.Name} has vague/generic naming");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Event types should have descriptive names. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void EventTypes_ShouldFollowDomainLanguage()
    {
        // Arrange
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();
        
        // Expected domain terms for Chat domain events
        var expectedDomainTerms = new[] 
        { 
            "Conversation", 
            "Message", 
            "User", 
            "Assistant", 
            "Title", 
            "Started", 
            "Completed", 
            "Updated", 
            "Appended" 
        };

        // Act
        foreach (var eventType in eventTypes)
        {
            var nameWithoutSuffix = eventType.Name.Replace("Event", "");
            var containsDomainTerm = expectedDomainTerms.Any(term => nameWithoutSuffix.Contains(term));
            
            if (!containsDomainTerm)
            {
                violations.Add($"Event {eventType.Name} does not contain recognizable domain language");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Event types should use domain language. Violations:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void EventsDirectory_ShouldOnlyContainEventTypes()
    {
        // Arrange
        var allTypesInEventsNamespace = ChatDomainAssembly.GetTypes()
            .Where(t => t.Namespace == "Axon.Modules.Chat.Domain.Events")
            .ToList();
        
        var eventTypes = GetAllEventTypes();
        var violations = new List<string>();

        // Act
        foreach (var type in allTypesInEventsNamespace)
        {
            if (!eventTypes.Contains(type))
            {
                violations.Add($"Type {type.Name} is in Events namespace but is not a domain event");
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Events namespace should only contain domain event types. Violations:\n{string.Join("\n", violations)}");
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
    /// Determines if a type is a record type
    /// </summary>
    private static bool IsRecordType(Type type)
    {
        // Records have compiler-generated members and specific inheritance patterns
        var hasEqualityContract = type.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance) != null;
        var hasCloneMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.Name.Contains("Clone") || m.Name == "<Clone>$");
        var hasRecordMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.Name == "PrintMembers" || m.Name == "ToString");
            
        return hasEqualityContract || hasCloneMethod || hasRecordMethods;
    }
}