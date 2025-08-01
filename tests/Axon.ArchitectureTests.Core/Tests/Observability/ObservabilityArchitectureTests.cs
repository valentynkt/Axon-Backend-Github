using Axon.ArchitectureTests.Core.Rules.Observability;
using Axon.Tests.Shared.TestBase;

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
        var result = await ExecuteRulesAndValidateAsync(rules, "All Observability Rules");

        // Assert
        AssertAllRulesSuccess(result, "Observability architecture compliance");
    }

    [Test]
    public async Task LogLevels_ShouldBeUsedAppropriately()
    {
        // Arrange & Act
        var rule = new LoggingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS001");
        
        // Log but don't fail - log level usage might vary (informational)
        LogInformationalViolations(ruleResult, new[] { "log level", "logging level" }, "Log Level Usage");
    }

    [Test]
    public async Task StructuredLogging_ShouldBeUsed()
    {
        // Arrange & Act
        var rule = new LoggingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS001");
        
        // Assert - Focus on structured logging violations using Framework helper
        AssertNoSpecificViolations(ruleResult, new[] { "structured", "interpolation" }, "Structured Logging");
    }

    [Test]
    public async Task BusinessMetrics_ShouldBeCollected()
    {
        // Arrange & Act
        var rule = new MetricsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS_002");
        
        // Log but don't fail - business metrics might not be needed everywhere (informational)
        LogInformationalViolations(ruleResult, new[] { "business" }, "Business Metrics");
    }

    [Test]
    public async Task DistributedTracing_ShouldUseCorrelationIds()
    {
        // Arrange & Act
        var rule = new TracingRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS003");
        
        // Assert - Focus on correlation ID violations using Framework helper
        AssertNoSpecificViolations(ruleResult, new[] { "correlation", "trace id" }, "Correlation ID");
    }

    [Test]
    public async Task ErrorHandling_ShouldBeObservable()
    {
        // Arrange & Act
        var rule = new LoggingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "OBS001");
        
        // Assert - Focus on error observability violations using Framework helper
        AssertNoSpecificViolations(ruleResult, new[] { "error", "exception" }, "Error Observability");
    }
}