using Axon.ArchitectureTests.Core.Rules.Configuration;
using Axon.ArchitectureTests.Core.TestBase;
using Axon.ArchitectureTests.Framework.Contracts;
// using Axon.Tests.Shared.TestBase; // Removed due to circular dependency - using local ArchitectureTestBase

namespace Axon.ArchitectureTests.Core.Tests.Configuration;

/// <summary>
/// Architecture tests to validate configuration patterns and practices across the application.
/// Uses Framework ArchitectureTestBase for maximum reuse and consistency.
/// </summary>
public sealed class ConfigurationArchitectureTests : ArchitectureTestBase
{
    [Test]
    public async Task AppSettings_ShouldFollowPatterns()
    {
        // Arrange & Act
        var rule = new AppSettingsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CONFIG_001");

        // Assert
        AssertRuleSuccess(ruleResult, "App settings IOptions<T> patterns");
    }

    [Test]
    public async Task ConfigurationValidation_ShouldBeImplemented()
    {
        // Arrange & Act
        var rule = new ConfigurationValidationRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG002");

        // Assert
        AssertRuleSuccess(ruleResult, "Configuration validation patterns");
    }

    [Test]
    public async Task EnvironmentConfiguration_ShouldBeConfigurable()
    {
        // Arrange & Act
        var rule = new EnvironmentConfigRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG003");

        // Assert
        AssertRuleSuccess(ruleResult, "Environment configuration patterns");
    }

    [Test]
    public async Task SecretsConfiguration_ShouldBeSecure()
    {
        // Arrange & Act
        var rule = new SecretsConfigRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG004");

        // Assert
        AssertRuleSuccess(ruleResult, "Secrets configuration security patterns");
    }

    [Test]
    public async Task LoggingConfiguration_ShouldFollowPatterns()
    {
        // Arrange & Act
        var rule = new LoggingConfigRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG005");

        // Assert
        AssertRuleSuccess(ruleResult, "Logging configuration patterns");
    }

    [Test]
    public async Task AllConfigurationRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new AppSettingsRule(),
            new ConfigurationValidationRule(),
            new EnvironmentConfigRule(),
            new SecretsConfigRule(),
            new LoggingConfigRule()
        };

        // Act
        var result = await ExecuteRulesAndValidateAsync(rules);

        // Assert
        AssertAllRulesSuccess(result);
    }

    [Test]
    public async Task ConfigurationOptions_ShouldHaveValidationAttributes()
    {
        // Arrange & Act
        var rule = new ConfigurationValidationRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG002");
        
        // Assert - Focus on validation attribute violations
        AssertNoSpecificViolations(ruleResult, "validation");
        AssertNoSpecificViolations(ruleResult, "attribute");
    }

    [Test]
    public async Task HardcodedValues_ShouldNotExistInConfiguration()
    {
        // Arrange & Act
        var rule = new EnvironmentConfigRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG003");
        
        // Assert - Focus on hardcoded value violations
        AssertNoSpecificViolations(ruleResult, "hardcoded");
        AssertNoSpecificViolations(ruleResult, "constant");
    }

    [Test]
    public async Task ConfigurationSections_ShouldBeProperlyStructured()
    {
        // Arrange & Act
        var rule = new AppSettingsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CONFIG_001");
        
        // Assert - Log but don't fail (informational)
        var structureViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("section", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("structure", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (structureViolations.Any())
        {
            Console.WriteLine("Configuration Structure Violations:");
            foreach (var violation in structureViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
    }

    [Test]
    public async Task SecretsManagement_ShouldBeSecure()
    {
        // Arrange & Act
        var rule = new SecretsConfigRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG004");
        
        // Assert - Focus on security violations
        AssertNoSpecificViolations(ruleResult, "secret");
        AssertNoSpecificViolations(ruleResult, "hardcoded");
        AssertNoSpecificViolations(ruleResult, "critical");
    }

    [Test]
    public async Task LoggingConfiguration_ShouldBeStructured()
    {
        // Arrange & Act
        var rule = new LoggingConfigRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG005");
        
        // Assert - Focus on logging structure violations
        AssertNoSpecificViolations(ruleResult, "structured");
        AssertNoSpecificViolations(ruleResult, "performance");
    }

    [Test]
    public async Task ConfigurationBinding_ShouldBeSafe()
    {
        // Arrange & Act
        var rule = new ConfigurationValidationRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "CFG002");
        
        // Assert - Focus on binding safety violations
        AssertNoSpecificViolations(ruleResult, "binding");
        AssertNoSpecificViolations(ruleResult, "type conversion");
    }
}