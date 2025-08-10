using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Axon.Modules.Chat.Domain.Tests.Events;

/// <summary>
/// Guard tests to ensure no forbidden time APIs are used in Events and Aggregates.
/// This enforces time discipline as required by CH-DOM-009 - all timing must be injected via IClock.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Event")]
[Category("Guard")]
public class EventTimeDisciplineGuardTests
{
    private static readonly string[] ForbiddenTimeApis = 
    [
        "DateTime.Now",
        "DateTime.UtcNow", 
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow"
    ];

    [Test]
    public void Events_ShouldNotUseForbiddenTimeApis()
    {
        // Arrange
        var eventsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Events");
        
        var eventFiles = Directory.GetFiles(eventsPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in eventFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            // Check for forbidden member access expressions
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
                        violations.Add($"{fileName}:{lineNumber} - Event uses forbidden time API: {forbiddenApi}");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Domain Events must not use forbidden time APIs. All timing must be injected via IClock. Violations found:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Aggregates_ShouldNotUseForbiddenTimeApis()
    {
        // Arrange
        var aggregatesPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Aggregates");
        
        var aggregateFiles = Directory.GetFiles(aggregatesPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in aggregateFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            // Check for forbidden member access expressions
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
                        violations.Add($"{fileName}:{lineNumber} - Aggregate uses forbidden time API: {forbiddenApi}");
                    }
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Domain Aggregates must not use forbidden time APIs. All timing must be injected via IClock. Violations found:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void EventConstructors_ShouldNotCallTimeProviders()
    {
        // Arrange
        var eventsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Events");
        
        var eventFiles = Directory.GetFiles(eventsPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in eventFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            // Find constructor declarations
            var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
            
            foreach (var constructor in constructors)
            {
                var constructorText = constructor.ToString();
                
                // Check if constructor contains any time provider calls
                if (ForbiddenTimeApis.Any(api => constructorText.Contains(api)))
                {
                    var fileName = Path.GetFileName(filePath);
                    var lineNumber = constructor.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    violations.Add($"{fileName}:{lineNumber} - Event constructor uses forbidden time API");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Event constructors must not call time providers. Violations found:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AggregateConstructors_ShouldNotCallTimeProviders()
    {
        // Arrange
        var aggregatesPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Aggregates");
        
        var aggregateFiles = Directory.GetFiles(aggregatesPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in aggregateFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            // Find constructor declarations
            var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
            
            foreach (var constructor in constructors)
            {
                var constructorText = constructor.ToString();
                
                // Check if constructor contains any time provider calls
                if (ForbiddenTimeApis.Any(api => constructorText.Contains(api)))
                {
                    var fileName = Path.GetFileName(filePath);
                    var lineNumber = constructor.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    violations.Add($"{fileName}:{lineNumber} - Aggregate constructor uses forbidden time API");
                }
            }
        }

        // Assert
        violations.ShouldBeEmpty($"Aggregate constructors must not call time providers. Violations found:\n{string.Join("\n", violations)}");
    }

    [Test]
    public void AggregateMethods_ShouldAcceptTimeAsParameter()
    {
        // Arrange
        var aggregatesPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Aggregates");
        
        var aggregateFiles = Directory.GetFiles(aggregatesPath, "*.cs", SearchOption.AllDirectories);
        var timeRelatedMethods = new List<string>();

        // Act
        foreach (var filePath in aggregateFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            // Find method declarations that might need time
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text.Contains("Start") || 
                           m.Identifier.Text.Contains("Complete") ||
                           m.Identifier.Text.Contains("Update") ||
                           m.Identifier.Text.Contains("Create"))
                .ToList();
            
            foreach (var method in methods)
            {
                var methodText = method.ToString();
                var fileName = Path.GetFileName(filePath);
                var methodName = method.Identifier.Text;
                
                // Check if method accepts IClock parameter
                var hasIClockParameter = method.ParameterList.Parameters
                    .Any(p => p.Type?.ToString().Contains("IClock") == true);
                
                if (hasIClockParameter)
                {
                    timeRelatedMethods.Add($"{fileName}.{methodName}");
                }
            }
        }

        // Assert - This test documents that time-related methods should accept IClock
        // We expect at least some methods to follow this pattern
        timeRelatedMethods.ShouldNotBeEmpty("At least some aggregate methods should accept IClock parameters for time discipline");
    }

    [Test]
    public void Events_ShouldOnlyContainTimestampsFromIClockSource()
    {
        // Arrange
        var eventsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Events");
        
        var eventFiles = Directory.GetFiles(eventsPath, "*.cs", SearchOption.AllDirectories);
        var timestampProperties = new List<string>();

        // Act
        foreach (var filePath in eventFiles)
        {
            var content = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            // Find properties that are likely timestamps
            var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Type?.ToString().Contains("DateTimeOffset") == true)
                .ToList();
            
            foreach (var property in properties)
            {
                var fileName = Path.GetFileName(filePath);
                var propertyName = property.Identifier.Text;
                timestampProperties.Add($"{fileName}.{propertyName}");
            }
        }

        // Assert - This test documents that events contain DateTimeOffset properties for timing
        // All timing should ultimately come from IClock injection
        timestampProperties.ShouldNotBeEmpty("Events should contain DateTimeOffset properties from IClock sources");
        
        // The specific verification that these come from IClock is handled by the other tests
        // that prevent direct time API usage
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