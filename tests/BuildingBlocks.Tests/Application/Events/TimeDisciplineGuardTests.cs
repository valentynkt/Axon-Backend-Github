using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using Xunit;

namespace BuildingBlocks.Tests.Application.Events;

/// <summary>
/// Guard tests to ensure no forbidden time APIs are used in BuildingBlocks Application layer.
/// This enforces time discipline - all timing must be injected via IClock or provided as parameters.
/// Applies to: behaviors, dispatchers, collectors, publishers, and event infrastructure.
/// </summary>
public class TimeDisciplineGuardTests
{
    private static readonly string[] ForbiddenTimeApis = 
    [
        "DateTime.Now",
        "DateTime.UtcNow", 
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow"
    ];

    [Fact]
    public void ApplicationBehaviors_ShouldNotUseForbiddenTimeApis()
    {
        // Arrange
        var behaviorsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "BuildingBlocks", "Application", "Behaviors");
        
        if (!Directory.Exists(behaviorsPath))
        {
            return; // No behaviors to test
        }

        var behaviorFiles = Directory.GetFiles(behaviorsPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in behaviorFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            
            foreach (var memberAccess in memberAccesses)
            {
                var fullExpression = memberAccess.ToString();
                
                foreach (var forbiddenApi in ForbiddenTimeApis)
                {
                    if (fullExpression.Contains(forbiddenApi))
                    {
                        var fileName = Path.GetFileName(filePath);
                        var lineNumber = memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        violations.Add($"{fileName}:{lineNumber} - Behavior uses forbidden time API: {forbiddenApi}");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Application Behaviors must not use forbidden time APIs. Use IClock or accept time as parameters. Violations found:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void EventDispatchersAndCollectors_ShouldNotUseForbiddenTimeApis()
    {
        // Arrange
        var eventsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "BuildingBlocks", "Application", "Events");
        
        if (!Directory.Exists(eventsPath))
        {
            return; // No event infrastructure to test
        }

        var eventFiles = Directory.GetFiles(eventsPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in eventFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            
            foreach (var memberAccess in memberAccesses)
            {
                var fullExpression = memberAccess.ToString();
                
                foreach (var forbiddenApi in ForbiddenTimeApis)
                {
                    if (fullExpression.Contains(forbiddenApi))
                    {
                        var fileName = Path.GetFileName(filePath);
                        var lineNumber = memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        violations.Add($"{fileName}:{lineNumber} - Event infrastructure uses forbidden time API: {forbiddenApi}");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Event dispatchers/collectors must not use forbidden time APIs. Time should come from domain events or IClock. Violations found:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void OutboxInfrastructure_ShouldNotUseForbiddenTimeApis()
    {
        // Arrange
        var outboxPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "BuildingBlocks", "Application", "Outbox");
        
        if (!Directory.Exists(outboxPath))
        {
            return; // No outbox infrastructure to test
        }

        var outboxFiles = Directory.GetFiles(outboxPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in outboxFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            
            foreach (var memberAccess in memberAccesses)
            {
                var fullExpression = memberAccess.ToString();
                
                foreach (var forbiddenApi in ForbiddenTimeApis)
                {
                    if (fullExpression.Contains(forbiddenApi))
                    {
                        var fileName = Path.GetFileName(filePath);
                        var lineNumber = memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        violations.Add($"{fileName}:{lineNumber} - Outbox infrastructure uses forbidden time API: {forbiddenApi}");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Outbox infrastructure must not use forbidden time APIs. Time should come from domain events or be injected. Violations found:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void EnvelopeContextInfrastructure_ShouldAllowSystemTimeForCorrelation()
    {
        // Arrange
        var envelopingPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "BuildingBlocks", "Application", "Events", "Enveloping");
        
        if (!Directory.Exists(envelopingPath))
        {
            return; // No enveloping infrastructure to test
        }

        var envelopeFiles = Directory.GetFiles(envelopingPath, "*.cs", SearchOption.AllDirectories);
        var systemTimeUsages = new List<string>();

        // Act - Document any system time usage (allowed for correlation/tracing)
        foreach (var filePath in envelopeFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            
            foreach (var memberAccess in memberAccesses)
            {
                var fullExpression = memberAccess.ToString();
                
                foreach (var forbiddenApi in ForbiddenTimeApis)
                {
                    if (fullExpression.Contains(forbiddenApi))
                    {
                        var fileName = Path.GetFileName(filePath);
                        systemTimeUsages.Add($"{fileName} - Uses {forbiddenApi} (allowed for correlation headers)");
                    }
                }
            }
        }

        // Assert - This is informational only, system time is allowed for envelope context
        // The key insight: envelope context is for correlation/tracing, not business logic
        // If this changes, we'd need to inject IClock into CommandTransactionBehavior
        systemTimeUsages.Count.ShouldBeLessThanOrEqualTo(10, 
            $"Envelope context may use system time for correlation headers, but usage should be minimal: {string.Join(", ", systemTimeUsages)}");
    }

    [Fact]
    public void CommandTransactionBehavior_ShouldUseActivityCurrentForTracing()
    {
        // Arrange
        var behaviorPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "BuildingBlocks", "Application", "Behaviors", "CommandTransactionBehavior.cs");
        
        if (!File.Exists(behaviorPath))
        {
            return; // Behavior doesn't exist
        }

        var content = File.ReadAllText(behaviorPath);

        // Act & Assert - Activity.Current is the proper source for correlation/tracing
        content.ShouldContain("Activity.Current", 
            "CommandTransactionBehavior should use Activity.Current for distributed tracing correlation");

        // System time APIs should not be used for business logic
        foreach (var forbiddenApi in ForbiddenTimeApis)
        {
            content.ShouldNotContain(forbiddenApi, 
                $"CommandTransactionBehavior should not use {forbiddenApi} - use Activity.Current for tracing, IClock for business time");
        }
    }

    private static string GetSolutionRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        while (currentDirectory != null && !File.Exists(Path.Combine(currentDirectory, "Axon.sln")))
        {
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }
        
        return currentDirectory ?? throw new InvalidOperationException("Could not find solution root directory");
    }
}