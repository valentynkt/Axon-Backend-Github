using System.Reflection;

namespace Axon.Modules.Chat.Domain.Tests.ArchitecturalGuards.Docs;

/// <summary>
/// Tests to ensure the README maintains required structure and accuracy.
/// Guards against documentation drift and missing sections.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Documentation")]
[Category("Guards")]
public class ReadmeShapeTests
{
    private static readonly string ReadmePath = Path.Combine(
        TestContext.CurrentContext.TestDirectory, 
        "..", "..", "..", "..", "..", "..",
        "src", "Modules", "Chat", "README.md");

    private static readonly string[] RequiredSections = new[]
    {
        "# Chat Domain Module — Source of Truth (MVP)",
        "## Overview", 
        "## Public API Surface (Locked)",
        "## Invariants & Business Rules (MVP)",
        "## Error Catalog Mapping (Operation → Errors)",
        "## Domain Events (Stable Contracts)",
        "## Usage Cookbook (Domain-only)",
        "## Specifications (Query Recipes)", 
        "## Testing Cookbook",
        "## Versioning & Compatibility",
        "## Contribution Checklist"
    };

    private static readonly string[] RequiredEventNames = new[]
    {
        "ConversationStartedEvent",
        "UserMessageAppendedEvent", 
        "AssistantMessageAppendedEvent",
        "ConversationTitleUpdatedEvent",
        "ConversationCompletedEvent"
    };

    private static readonly string[] RequiredPublicTypes = new[]
    {
        "Conversation",
        "ConversationStatus", 
        "Message",
        "ConversationTitle",
        "MessageContent",
        "MessageRole",
        "ConversationId",
        "MessageId", 
        "UserId",
        "ConversationStartedEvent",
        "UserMessageAppendedEvent",
        "AssistantMessageAppendedEvent", 
        "ConversationTitleUpdatedEvent",
        "ConversationCompletedEvent",
        "IClock",
        "SystemClock",
        "AssemblyMarker"
    };

    [Test]
    public void README_ShouldExist()
    {
        // Act & Assert
        File.Exists(ReadmePath).Should().BeTrue($"README not found at {ReadmePath}");
    }

    [Test]
    public void README_ShouldContainAllRequiredSections()
    {
        // Arrange
        var readmeContent = File.ReadAllText(ReadmePath);
        var missingHeaders = new List<string>();
        
        // Act & Assert
        foreach (var requiredSection in RequiredSections)
        {
            if (!readmeContent.Contains(requiredSection))
            {
                missingHeaders.Add(requiredSection);
            }
        }
        
        if (missingHeaders.Any())
        {
            var missing = string.Join("\n  - ", missingHeaders);
            Assert.Fail($"README is missing required sections:\n  - {missing}");
        }
    }

    [Test]
    public void README_EventNames_ShouldMatchActualEventTypes()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        var actualEventTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.Contains("Events") == true && 
                       t.Name.EndsWith("Event") && 
                       t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(name => name)
            .ToList();
            
        var readmeContent = File.ReadAllText(ReadmePath);
        var missingEvents = new List<string>();
        var extraEvents = new List<string>();
        
        // Act & Assert - Check required events are mentioned in README
        foreach (var eventName in RequiredEventNames)
        {
            if (!readmeContent.Contains(eventName))
            {
                missingEvents.Add(eventName);
            }
        }
        
        // Check for events mentioned in README that don't exist in code
        foreach (var eventName in RequiredEventNames)
        {
            if (!actualEventTypes.Contains(eventName))
            {
                extraEvents.Add(eventName);
            }
        }
        
        var violations = new List<string>();
        
        if (missingEvents.Any())
        {
            violations.Add($"Events missing from README: {string.Join(", ", missingEvents)}");
        }
        
        if (extraEvents.Any())
        {
            violations.Add($"Events in README but not in code: {string.Join(", ", extraEvents)}");
        }
        
        if (violations.Any())
        {
            Assert.Fail($"Event documentation mismatch:\n  - {string.Join("\n  - ", violations)}");
        }
        
        // Verify we found the expected number of events
        actualEventTypes.Should().HaveCount(RequiredEventNames.Length,
            "Expected number of domain events should match required events list");
    }

    [Test]
    public void README_PublicAPIList_ShouldMatchWhitelistedTypes()
    {
        // Arrange - Get actual whitelisted types from the whitelist test
        var whitelistTestType = typeof(PublicSurfaceWhitelistTests);
        var whitelistedField = whitelistTestType.GetField("WhitelistedPublicTypes", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        whitelistedField.Should().NotBeNull("WhitelistedPublicTypes field should exist in PublicSurfaceWhitelistTests");
        
        var whitelistedTypes = (HashSet<string>)whitelistedField!.GetValue(null)!;
        var whitelistedTypeNames = whitelistedTypes
            .Select(fullName => fullName.Split('.').Last()) // Get simple type name
            .OrderBy(name => name)
            .ToList();
            
        var readmeContent = File.ReadAllText(ReadmePath);
        var missingTypes = new List<string>();
        
        // Act & Assert
        foreach (var typeName in RequiredPublicTypes)
        {
            if (!readmeContent.Contains(typeName))
            {
                missingTypes.Add(typeName);
            }
        }
        
        if (missingTypes.Any())
        {
            var missing = string.Join("\n  - ", missingTypes);
            Assert.Fail($"README is missing required public types:\n  - {missing}\n\n" +
                       "Ensure the 'Public API Surface (Locked)' section lists all whitelisted types.");
        }
        
        // Cross-check: All required types should be in the actual whitelist
        var requiredButNotWhitelisted = RequiredPublicTypes
            .Where(typeName => !whitelistedTypeNames.Contains(typeName))
            .ToList();
            
        if (requiredButNotWhitelisted.Any())
        {
            var missing = string.Join("\n  - ", requiredButNotWhitelisted);
            Assert.Fail($"Types required in README but not in whitelist:\n  - {missing}\n\n" +
                       "Either add to whitelist or remove from required list.");
        }
    }

    [Test]
    public void README_ShouldMentionAllOperationMethods()
    {
        // Arrange
        var readmeContent = File.ReadAllText(ReadmePath);
        var requiredMethods = new[]
        {
            "Start(",
            "AppendUserMessage(",
            "AppendAssistantMessage(",
            "UpdateTitle(",
            "Complete("
        };
        
        var missingMethods = new List<string>();
        
        // Act & Assert
        foreach (var method in requiredMethods)
        {
            if (!readmeContent.Contains(method))
            {
                missingMethods.Add(method);
            }
        }
        
        if (missingMethods.Any())
        {
            var missing = string.Join("\n  - ", missingMethods);
            Assert.Fail($"README is missing required public method signatures:\n  - {missing}\n\n" +
                       "Ensure the 'Public API Surface' section includes all public methods.");
        }
    }

    [Test]
    public void README_ShouldHaveContributionChecklist()
    {
        // Arrange
        var readmeContent = File.ReadAllText(ReadmePath);
        var requiredChecklistItems = new[]
        {
            "[ ] **Public API Snapshot**",
            "[ ] **Error Codes**", 
            "[ ] **Guard Tests Green**",
            "[ ] **Business Rules Internal**",
            "[ ] **Unit Tests**",
            "[ ] **Domain Events**",
            "[ ] **Time Discipline**",
            "[ ] **Result Pattern**"
        };
        
        var missingItems = new List<string>();
        
        // Act & Assert
        foreach (var item in requiredChecklistItems)
        {
            if (!readmeContent.Contains(item))
            {
                missingItems.Add(item);
            }
        }
        
        if (missingItems.Any())
        {
            var missing = string.Join("\n  - ", missingItems);
            Assert.Fail($"README contribution checklist is missing items:\n  - {missing}");
        }
    }
}