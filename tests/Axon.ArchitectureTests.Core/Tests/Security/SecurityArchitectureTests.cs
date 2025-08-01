using Axon.ArchitectureTests.Core.Rules.Security;
using Axon.Tests.Shared.TestBase;

namespace Axon.ArchitectureTests.Core.Tests.Security;

/// <summary>
/// Architecture tests to validate security patterns and practices across the application.
/// Uses Framework ArchitectureTestBase for maximum reuse and consistency.
/// </summary>
public sealed class SecurityArchitectureTests : ArchitectureTestBase
{
    [Test]
    public async Task Authentication_ShouldFollowSecurityPatterns()
    {
        // Arrange & Act
        var rule = new AuthenticationPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC001");

        // Assert
        AssertRuleSuccess(ruleResult, "Authentication patterns");
    }

    [Test]
    public async Task Authorization_ShouldFollowSecurityPatterns()
    {
        // Arrange & Act
        var rule = new AuthorizationPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC002");

        // Assert
        AssertRuleSuccess(ruleResult, "Authorization patterns");
    }

    [Test]
    public async Task InputValidation_ShouldFollowSecurityPatterns()
    {
        // Arrange & Act
        var rule = new InputValidationRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC003");

        // Assert
        AssertRuleSuccess(ruleResult, "Input validation patterns");
    }

    [Test]
    public async Task SecretsManagement_ShouldFollowSecurityPatterns()
    {
        // Arrange & Act
        var rule = new SecretsManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC004");

        // Assert
        AssertRuleSuccess(ruleResult, "Secrets management patterns");
    }

    [Test]
    public async Task DataProtection_ShouldFollowSecurityPatterns()
    {
        // Arrange & Act
        var rule = new DataProtectionRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC005");

        // Assert
        AssertRuleSuccess(ruleResult, "Data protection patterns");
    }

    [Test] 
    public async Task SecurityHeaders_ShouldBeConfigured()
    {
        // Arrange & Act
        var rule = new SecurityHeadersRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC006");

        // Assert
        AssertRuleSuccess(ruleResult, "Security headers patterns");
    }

    [Test]
    public async Task AllSecurityRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new AuthenticationPatternsRule(),
            new AuthorizationPatternsRule(),
            new InputValidationRule(),
            new SecretsManagementRule(),
            new DataProtectionRule(),
            new SecurityHeadersRule()
        };

        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "All Security Rules");

        // Assert
        AssertAllRulesSuccess(result, "Security architecture compliance");
    }

    [Test]
    public async Task ApiEndpoints_ShouldHaveSecurityAttributes()
    {
        // Arrange & Act
        var rule = new AuthorizationPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC002");
        
        // Assert - Focus on API endpoint security violations
        AssertNoSpecificViolations(ruleResult, new[] { "endpoint", "authorize" }, "API Security");
    }

    [Test]
    public async Task SensitiveData_ShouldNotBeLogged()
    {
        // Arrange & Act
        var rule = new DataProtectionRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC005");
        
        // Assert - Focus on logging violations
        AssertNoSpecificViolations(ruleResult, new[] { "logging", "sensitive" }, "Data Logging");
    }

    [Test]
    public async Task Dependencies_ShouldHaveSecurityValidation()
    {
        // Arrange & Act
        var rule = new InputValidationRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC003");
        
        // Assert - Log but don't fail (informational)
        LogInformationalViolations(ruleResult, new[] { "dependency" }, "Dependency Security");
    }

    [Test]
    public async Task Encryption_ShouldBeUsedForSensitiveData()
    {
        // Arrange & Act
        var rule = new DataProtectionRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "SEC005");
        
        // Assert - Focus on encryption violations
        AssertNoSpecificViolations(ruleResult, new[] { "encryption", "plaintext" }, "Encryption");
    }
}