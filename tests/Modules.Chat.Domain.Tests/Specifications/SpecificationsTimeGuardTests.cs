using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Modules.Chat.Domain.Tests.Specifications;

/// <summary>
/// Guard tests to ensure no forbidden time APIs are used in Specification classes.
/// This enforces time discipline as required by CH-DOM-002.
/// </summary>
public class SpecificationsTimeGuardTests
{
    private static readonly string[] ForbiddenTimeApis = 
    [
        "DateTime.Now",
        "DateTime.UtcNow", 
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow"
    ];

    [Fact]
    public void Specifications_ShouldNotUseForbiddenTimeApis()
    {
        // Arrange
        var specificationsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Specifications");
        
        var specificationFiles = Directory.GetFiles(specificationsPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in specificationFiles)
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
                        violations.Add($"{fileName}:{lineNumber} - Uses forbidden API: {forbiddenApi}");
                    }
                }
            }
        }

        // Assert
        violations.Should().BeEmpty($"Specification classes must not use forbidden time APIs. Violations found:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void Specifications_ShouldAcceptTimeAsParameters()
    {
        // Arrange
        var specificationsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Specifications");
        
        var specificationFiles = Directory.GetFiles(specificationsPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("CreatedBetween") || f.Contains("UpdatedSince"))
            .ToList();

        // Act & Assert
        specificationFiles.Should().NotBeEmpty("Should find time-related specifications");

        foreach (var filePath in specificationFiles)
        {
            var content = File.ReadAllText(filePath);
            
            // Time-related specs should accept DateTimeOffset parameters
            content.Should().Contain("DateTimeOffset", 
                $"Time-related specification {Path.GetFileName(filePath)} should accept DateTimeOffset parameters");
        }
    }

    [Fact]
    public void SpecificationConstructors_ShouldNotCallTimeProviders()
    {
        // Arrange
        var specificationsPath = Path.Combine(
            GetSolutionRoot(), 
            "src", "Modules", "Chat", "Domain", "Specifications");
        
        var specificationFiles = Directory.GetFiles(specificationsPath, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act
        foreach (var filePath in specificationFiles)
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
                    violations.Add($"{fileName}:{lineNumber} - Constructor uses forbidden time API");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty($"Specification constructors must not call time providers. Violations found:\n{string.Join("\n", violations)}");
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