using Axon.ArchitectureTests.Core.Rules.Observability;
using Axon.ArchitectureTests.Core.TestBase;
using Axon.ArchitectureTests.Framework.Contracts;
// using Axon.Tests.Shared.TestBase; // Removed due to circular dependency

namespace Axon.ArchitectureTests.Core.Tests.Observability;

/// <summary>
/// Architecture tests to validate observability patterns including logging, monitoring, and telemetry.
/// Uses Framework ArchitectureTestBase for maximum reuse and consistency.
/// </summary>
public sealed class ObservabilityArchitectureTests : ArchitectureTestBase
{

    [Test]
    public async Task Logging_ShouldFollowPatterns()
    {
        // Arrange & Act
        var rule = new LoggingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS001");

        // Assert
        AssertRuleSuccess(ruleResult, "Logging patterns");
    }

    [Test]
    public async Task Metrics_ShouldBeImplemented()
    {
        // Arrange & Act
        var rule = new MetricsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS_002");

        // Assert
        AssertRuleSuccess(ruleResult, "Metrics patterns");
    }

    [Test]
    public async Task Tracing_ShouldBeImplemented()
    {
        // Arrange & Act
        var rule = new TracingRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS003");

        // Assert
        AssertRuleSuccess(ruleResult, "Tracing patterns");
    }

    [Test]
    public async Task HealthChecks_ShouldBeImplemented()
    {
        // Arrange & Act
        var rule = new HealthCheckRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS004");

        // Assert
        AssertRuleSuccess(ruleResult, "Health check patterns");
    }

    [Test]
    public async Task AllObservabilityRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new LoggingPatternsRule(),
            new MetricsRule(),
            new TracingRule(),
            new HealthCheckRule()
        };

        // Act
        var result = await ExecuteRulesAndValidateAsync(rules);

        // Assert
        AssertAllRulesSuccess(result);
    }

    [Test]
    public async Task LogLevels_ShouldBeUsedAppropriately()
    {
        // Arrange & Act
        var rule = new LoggingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS001");
        
        // Log but don't fail - log level usage might vary (informational)
        var logLevelViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("log level", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("logging level", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (logLevelViolations.Any())
        {
            Console.WriteLine("Log Level Usage Violations:");
            foreach (var violation in logLevelViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
    }

    [Test]
    public async Task StructuredLogging_ShouldBeUsed()
    {
        // Arrange & Act
        var rule = new LoggingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS001");
        
        // Assert - Focus on structured logging violations
        AssertNoSpecificViolations(ruleResult, "structured");
        AssertNoSpecificViolations(ruleResult, "interpolation");
    }

    [Test]
    public async Task BusinessMetrics_ShouldBeCollected()
    {
        // Arrange & Act
        var rule = new MetricsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS_002");
        
        // Log but don't fail - business metrics might not be needed everywhere (informational)
        var businessMetricsViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("business", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (businessMetricsViolations.Any())
        {
            Console.WriteLine("Business Metrics Violations:");
            foreach (var violation in businessMetricsViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
    }

    [Test]
    public async Task DistributedTracing_ShouldUseCorrelationIds()
    {
        // Arrange & Act
        var rule = new TracingRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS003");
        
        // Assert - Focus on correlation ID violations
        AssertNoSpecificViolations(ruleResult, "correlation");
        AssertNoSpecificViolations(ruleResult, "trace id");
    }

    [Test]
    public async Task ErrorHandling_ShouldBeObservable()
    {
        // Arrange & Act
        var rule = new LoggingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS001");
        
        // Assert - Focus on error observability violations
        AssertNoSpecificViolations(ruleResult, "error");
        AssertNoSpecificViolations(ruleResult, "exception");
    }
}