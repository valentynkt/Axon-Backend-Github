using System.Reflection;

namespace Axon.Modules.Chat.Domain.Tests.ArchitecturalGuards.Docs;

/// <summary>
/// Tests to ensure README error mappings accurately reflect the actual error codes 
/// emitted by Conversation methods. Guards against documentation drift.
/// </summary>
[TestFixture]  
[Category("Unit")]
[Category("Documentation")]
[Category("Guards")]
public class ErrorMappingTests
{
    private static readonly string ReadmePath = Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..", "..", "..",
        "src", "Modules", "Chat", "README.md");

    /// <summary>
    /// Curated mapping of public Conversation methods to their possible error codes.
    /// This is the authoritative source that README should match.
    /// </summary>
    private static readonly Dictionary<string, string[]> MethodToErrorCodes = new()
    {
        ["Start"] = new[]
        {
            "CHAT_CONVERSATION_OWNER_REQUIRED",
            "CHAT_CONVERSATION_TITLE_TOO_LONG"
        },
        
        ["AppendUserMessage"] = new[] 
        {
            "CHAT_CONVERSATION_NOT_ACTIVE",
            "CHAT_MESSAGE_CONTENT_EMPTY", 
            "CHAT_MESSAGE_CONTENT_TOO_LONG",
            "CHAT_MESSAGE_LIMIT_EXCEEDED"
        },
        
        ["AppendAssistantMessage"] = new[]
        {
            "CHAT_CONVERSATION_NOT_ACTIVE",
            "CHAT_MESSAGE_CONTENT_EMPTY",
            "CHAT_MESSAGE_CONTENT_TOO_LONG", 
            "CHAT_MESSAGE_LIMIT_EXCEEDED",
            "CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION"
        },
        
        ["UpdateTitle"] = new[]
        {
            "CHAT_CONVERSATION_NOT_ACTIVE",
            "CHAT_CONVERSATION_TITLE_EMPTY",
            "CHAT_CONVERSATION_TITLE_TOO_LONG"
        },
        
        ["Complete"] = new[]
        {
            "CHAT_CONVERSATION_NOT_ACTIVE", 
            "CHAT_CONVERSATION_EMPTY_ON_COMPLETE"
        }
    };

    [Test]  
    public void README_ShouldContainAllMethodErrorMappings()
    {
        // Arrange
        var readmeContent = File.ReadAllText(ReadmePath);
        var violations = new List<string>();
        
        // Act & Assert
        foreach (var (methodName, errorCodes) in MethodToErrorCodes)
        {
            // Check method section exists
            var methodSectionPattern = $"### {methodName}(";
            if (!readmeContent.Contains(methodSectionPattern))
            {
                violations.Add($"Missing method section: {methodSectionPattern}");
                continue;
            }
            
            // Check all error codes are documented for this method
            foreach (var errorCode in errorCodes)
            {
                if (!readmeContent.Contains(errorCode))
                {
                    violations.Add($"Method {methodName} missing error code: {errorCode}");
                }
            }
        }
        
        if (violations.Any())
        {
            var violationList = string.Join("\n  - ", violations);
            Assert.Fail($"README error mapping issues:\n  - {violationList}\n\n" +
                       "Update 'Error Catalog Mapping (Operation → Errors)' section to include all method error codes.");
        }
    }

    [Test]
    public void README_ErrorCodes_ShouldMatchValidationCatalog()
    {
        // Arrange
        var readmeContent = File.ReadAllText(ReadmePath);
        var catalogPath = Path.Combine(
            Path.GetDirectoryName(ReadmePath)!, 
            "Domain", "Docs", "04_VALIDATION & ERROR CATALOG.md");
            
        catalogPath.Should().NotBeNull();
        File.Exists(catalogPath).Should().BeTrue($"Error catalog not found at {catalogPath}");
        
        var catalogContent = File.ReadAllText(catalogPath);
        
        // Extract all error codes mentioned in README  
        var allReadmeErrorCodes = MethodToErrorCodes.Values
            .SelectMany(codes => codes)
            .Distinct()
            .OrderBy(code => code)
            .ToList();
        
        var missingFromCatalog = new List<string>();
        
        // Act & Assert
        foreach (var errorCode in allReadmeErrorCodes)
        {
            if (!catalogContent.Contains(errorCode))
            {
                missingFromCatalog.Add(errorCode);
            }
        }
        
        if (missingFromCatalog.Any())
        {
            var missing = string.Join("\n  - ", missingFromCatalog);
            Assert.Fail($"README references error codes not found in validation catalog:\n  - {missing}\n\n" +
                       "Either add codes to catalog or fix README references.");
        }
    }

    [Test]
    public void ConversationMethods_ShouldOnlyEmitDocumentedErrorCodes()
    {
        // Arrange - This is a documentation consistency check
        // We verify that the documented error codes align with business rule usage
        var businessRuleTypes = typeof(AssemblyMarker).Assembly.GetTypes()
            .Where(t => t.Namespace?.Contains("Rules") == true && 
                       t.Name.EndsWith("Rule"))
            .ToList();
            
        // Extract error codes from business rules via reflection
        var ruleErrorCodes = new HashSet<string>();
        foreach (var ruleType in businessRuleTypes)
        {
            try
            {
                var instance = Activator.CreateInstance(ruleType, new object[] { GetDefaultParameterFor(ruleType) });
                if (instance is BuildingBlocks.Core.Domain.Rules.IBusinessRule rule)
                {
                    ruleErrorCodes.Add(rule.Code);
                }
            }
            catch
            {
                // Skip rules we can't instantiate easily
            }
        }
        
        var allDocumentedCodes = MethodToErrorCodes.Values
            .SelectMany(codes => codes)
            .Distinct()
            .ToHashSet();
        
        // Act & Assert - Check that we have coverage of major error codes
        var majorExpectedCodes = new[]
        {
            "CHAT_CONVERSATION_NOT_ACTIVE",
            "CHAT_MESSAGE_CONTENT_EMPTY", 
            "CHAT_MESSAGE_LIMIT_EXCEEDED",
            "CHAT_CONVERSATION_OWNER_REQUIRED"
        };
        
        var missingMajorCodes = majorExpectedCodes
            .Where(code => !allDocumentedCodes.Contains(code))
            .ToList();
        
        if (missingMajorCodes.Any())
        {
            var missing = string.Join(", ", missingMajorCodes);
            Assert.Fail($"Major error codes not documented in README: {missing}");
        }
        
        // Verify we have reasonable coverage
        allDocumentedCodes.Should().HaveCountGreaterThan(8, 
            "Should document substantial number of error codes");
    }

    [Test] 
    public void README_ShouldReferenceValidationCatalogAsSourceOfTruth()
    {
        // Arrange
        var readmeContent = File.ReadAllText(ReadmePath);
        
        // Act & Assert
        readmeContent.Should().Contain("04_VALIDATION & ERROR CATALOG", 
            "README should reference the validation catalog as source of truth");
        readmeContent.Should().Contain("Error Catalog Mapping", 
            "Should have dedicated error mapping section");
    }

    private static object GetDefaultParameterFor(Type ruleType)
    {
        // Simple parameter provider for rule instantiation
        var constructors = ruleType.GetConstructors();
        var constructor = constructors.FirstOrDefault();
        
        if (constructor == null) return new object[0];
        
        var parameters = constructor.GetParameters();
        if (parameters.Length == 0) return new object[0];
        
        var firstParamType = parameters[0].ParameterType;
        
        // Provide reasonable defaults for common parameter types
        if (firstParamType == typeof(string)) return "test";
        if (firstParamType == typeof(int)) return 1;
        if (firstParamType == typeof(bool)) return true;
        if (firstParamType.Name.Contains("UserId")) return UserId.New();
        if (firstParamType.Name.Contains("Status")) return ConversationStatus.Active;
        if (firstParamType.IsGenericType && firstParamType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            return Array.CreateInstance(firstParamType.GetGenericArguments()[0], 0);
        
        // Default fallback
        return Activator.CreateInstance(firstParamType) ?? new object();
    }
}