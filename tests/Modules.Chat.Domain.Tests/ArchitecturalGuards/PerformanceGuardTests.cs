using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;
using Axon.Modules.Chat.Domain.Specifications;

namespace Axon.Modules.Chat.Domain.Tests.ArchitecturalGuards;

/// <summary>
/// Guards to ensure performance patterns and allocation hygiene are maintained.
/// These tests prevent regressions in hot path optimizations and EF-friendly patterns.
/// </summary>
public class PerformanceGuardTests
{
    [Fact]
    public void Specifications_ShouldNotUseToLowerInvariantInExpressions()
    {
        // Arrange
        var specificationTypes = typeof(ConversationTitleContainsSpec).Assembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Spec") && t.Namespace?.Contains("Specifications") == true)
            .ToList();

        // Act & Assert
        foreach (var specType in specificationTypes)
        {
            var sourceFile = GetSourceFilePath(specType);
            if (sourceFile == null) continue;

            var sourceCode = System.IO.File.ReadAllText(sourceFile);
            
            // Check that ToLowerInvariant doesn't appear in expression contexts
            // It should only be used in constructors/initialization, not in ToExpression() methods
            var expressionMethods = sourceCode.Split("public override Expression<Func<")
                .Skip(1); // Skip everything before the first expression method
                
            foreach (var expressionMethod in expressionMethods)
            {
                expressionMethod.Should().NotContain("ToLowerInvariant",
                    $"Specification {specType.Name} should not use ToLowerInvariant in expression methods. " +
                    "Use TextSlices.NormalizeForSearchConst for constants and .ToLower() on entity fields.");
            }
        }
    }

    [Fact]
    public void Specifications_ShouldUseToLowerOnEntityFields()
    {
        // Arrange
        var specificationTypes = typeof(ConversationTitleContainsSpec).Assembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Spec") && t.Namespace?.Contains("Specifications") == true)
            .Where(t => t.Name.Contains("Title")) // Focus on title search specs
            .ToList();

        // Act & Assert
        foreach (var specType in specificationTypes)
        {
            var sourceFile = GetSourceFilePath(specType);
            if (sourceFile == null) continue;

            var sourceCode = System.IO.File.ReadAllText(sourceFile);
            
            // If the spec deals with string comparisons, it should use .ToLower() on entity fields
            if (sourceCode.Contains("Title") && sourceCode.Contains("Contains"))
            {
                sourceCode.Should().MatchRegex(@"c\.Title.*\.ToLower\(\)",
                    $"Specification {specType.Name} should use .ToLower() on entity fields for EF translation");
            }
        }
    }

    [Fact]
    public void TextSlicesUsage_ShouldBeConsistentInDomain()
    {
        // Arrange - Find all domain files that might use string operations
        var domainAssembly = typeof(ConversationTitleContainsSpec).Assembly;
        var domainTypes = domainAssembly.GetTypes()
            .Where(t => t.Namespace?.Contains("Domain") == true)
            .ToList();

        var violatingFiles = new List<string>();

        // Act & Assert
        foreach (var type in domainTypes)
        {
            var sourceFile = GetSourceFilePath(type);
            if (sourceFile == null) continue;

            var sourceCode = System.IO.File.ReadAllText(sourceFile);
            
            // Check for raw substring operations that should use TextSlices.Preview
            if (sourceCode.Contains("[..100]") || 
                (sourceCode.Contains("Substring(0, 100)") && !sourceCode.Contains("TextSlices")))
            {
                violatingFiles.Add($"{type.Name}: Raw 100-char substring operations should use TextSlices.Preview");
            }
            
            // Check for raw ToLowerInvariant in specification expressions
            if (type.Name.Contains("Spec") && 
                sourceCode.Contains("ToExpression") && 
                sourceCode.Contains("ToLowerInvariant"))
            {
                violatingFiles.Add($"{type.Name}: ToLowerInvariant in expressions should use TextSlices.NormalizeForSearchConst");
            }
        }

        violatingFiles.Should().BeEmpty(
            "All domain code should use TextSlices utilities for consistent allocation hygiene. " +
            "Violations found: " + string.Join(", ", violatingFiles));
    }

    [Fact]
    public void PreviewOperations_ShouldUseTextSlicesUtility()
    {
        // Arrange
        var conversationSourceFile = GetSourceFilePathForType("Conversation");
        
        // Act & Assert
        if (conversationSourceFile != null)
        {
            var sourceCode = System.IO.File.ReadAllText(conversationSourceFile);
            
            // Should use TextSlices.Preview for preview operations
            if (sourceCode.Contains("CreateContentPreview"))
            {
                sourceCode.Should().Contain("TextSlices.Preview",
                    "Conversation.CreateContentPreview should use TextSlices.Preview for allocation hygiene");
                    
                sourceCode.Should().NotMatch("*content[..100]*",
                    "Raw range operators for 100-char limits should use TextSlices.Preview");
            }
        }
    }

    private static string? GetSourceFilePath(Type type)
    {
        // Simple heuristic to find source file based on type name and namespace
        var namespaceParts = type.Namespace?.Split('.') ?? [];
        var pathParts = namespaceParts.Skip(2).ToArray(); // Skip "Axon.Modules"
        
        var basePath = "/Users/valentynkit/Repos/Axon-Backend/src/Modules";
        var relativePath = string.Join("/", pathParts);
        var fileName = $"{type.Name}.cs";
        
        var possiblePaths = new[]
        {
            $"{basePath}/{relativePath}/{fileName}",
            $"{basePath}/{relativePath}/Aggregates/Conversation/{fileName}",
            $"{basePath}/{relativePath}/Specifications/{fileName}",
            $"{basePath}/{relativePath}/Internal/Text/{fileName}"
        };

        return possiblePaths.FirstOrDefault(System.IO.File.Exists);
    }

    private static string? GetSourceFilePathForType(string typeName)
    {
        var searchPaths = new[]
        {
            "/Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain",
        };

        foreach (var basePath in searchPaths)
        {
            var files = System.IO.Directory.GetFiles(basePath, $"{typeName}.cs", 
                System.IO.SearchOption.AllDirectories);
            if (files.Length > 0)
                return files[0];
        }

        return null;
    }
}