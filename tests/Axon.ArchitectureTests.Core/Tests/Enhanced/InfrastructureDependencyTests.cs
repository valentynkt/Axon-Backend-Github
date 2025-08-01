using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;
using Axon.ArchitectureTests.Core.Rules.Enhanced;
using Axon.Tests.Shared.TestBase;

namespace Axon.ArchitectureTests.Core.Tests.Enhanced;

/// <summary>
/// Infrastructure layer dependency tests with external service pattern validation.
/// Ensures proper isolation and dependency management in the Infrastructure layer.
/// </summary>
[TestFixture]
public sealed class InfrastructureDependencyTests : ArchitectureTestBase
{
    [Test]
    public async Task ExternalServices_ShouldBeProperlyIsolated()
    {
        // Arrange
        var rule = new ExternalServiceIsolationRule();
        ValidateRuleConfiguration(rule);
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD001");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "external-leakage", "direct-external", "coupling" }, 
            "Critical External Service Leakage");
    }

    [Test]
    public async Task DatabaseContext_ShouldFollowPatterns()
    {
        // Arrange
        var rule = new DatabaseContextPatternRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD002");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "context-pattern", "unit-of-work", "repository" }, 
            "Database Context Patterns");
        
        var contextViolations = FilterViolations(ruleResult, 
            "poor-context", "direct-access", "missing-abstraction");
        
        LogViolations(contextViolations, "Database Context Issues");
    }

    [Test]
    public async Task HttpClients_ShouldUseTypedClients()
    {
        // Arrange
        var rule = new HttpClientPatternRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD003");
        
        // Assert
        var httpClientViolations = FilterViolations(ruleResult, 
            "raw-httpclient", "untyped", "direct-usage");
        
        LogViolations(httpClientViolations, "HTTP Client Pattern Issues");
        
        // Prefer typed clients over raw HttpClient usage
        LogInformationalViolations(ruleResult, 
            new[] { "typed-client", "resilience", "configuration" }, 
            "HTTP Client Best Practices");
    }

    [Test]
    public async Task Configuration_ShouldBeProperlyBound()
    {
        // Arrange
        var rule = new ConfigurationBindingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD004");
        
        // Assert
        var configViolations = FilterViolations(ruleResult, 
            "unbound-config", "magic-strings", "direct-access");
        
        LogViolations(configViolations, "Configuration Binding Issues");
        
        // Configuration should use strongly-typed options
        AssertNoSpecificViolations(ruleResult, 
            new[] { "critical-config", "security-config" }, 
            "Critical Configuration Issues");
    }

    [Test]
    public async Task ExternalDependencies_ShouldHaveCircuitBreakers()
    {
        // Arrange
        var rule = new CircuitBreakerRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD005");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "circuit-breaker", "resilience", "fallback" }, 
            "Resilience Patterns");
        
        var resilienceViolations = FilterViolations(ruleResult, 
            "no-circuit-breaker", "missing-resilience", "fragile");
        
        if (resilienceViolations.Any())
        {
            LogViolations(resilienceViolations, "Missing Resilience Patterns");
        }
    }

    [Test]
    public async Task MessageBrokers_ShouldFollowPublishSubscribePatterns()
    {
        // Arrange
        var rule = new MessageBrokerPatternRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD006");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "message-broker", "publish-subscribe", "async-messaging" }, 
            "Message Broker Patterns");
    }

    [Test]
    public async Task CachingStrategies_ShouldBeConsistent()
    {
        // Arrange
        var rule = new CachingStrategyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD007");
        
        // Assert
        var cachingViolations = FilterViolations(ruleResult, 
            "inconsistent-caching", "cache-abuse", "poor-strategy");
        
        LogViolations(cachingViolations, "Caching Strategy Issues");
        
        LogInformationalViolations(ruleResult, 
            new[] { "caching-strategy", "cache-aside", "distributed-cache" }, 
            "Caching Patterns");
    }

    [Test]
    public async Task DatabaseConnections_ShouldBeProperlyManaged()
    {
        // Arrange
        var rule = new DatabaseConnectionRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD008");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "connection-leak", "no-disposal", "pool-exhaustion" }, 
            "Critical Database Connection Issues");
        
        var connectionViolations = FilterViolations(ruleResult, 
            "poor-connection", "missing-pooling", "inefficient");
        
        LogViolations(connectionViolations, "Database Connection Management");
    }

    [Test]
    public async Task FileSystem_ShouldUseAbstractions()
    {
        // Arrange
        var rule = new FileSystemAbstractionRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD009");
        
        // Assert
        var fileSystemViolations = FilterViolations(ruleResult, 
            "direct-filesystem", "static-file", "hardcoded-path");
        
        LogViolations(fileSystemViolations, "File System Abstraction Issues");
        
        // File system access should be abstracted for testability
        LogInformationalViolations(ruleResult, 
            new[] { "abstraction", "testability", "isolation" }, 
            "File System Patterns");
    }

    [Test]
    public async Task Logging_ShouldFollowStructuredPatterns()
    {
        // Arrange
        var rule = new StructuredLoggingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD010");
        
        // Assert
        var loggingViolations = FilterViolations(ruleResult, 
            "unstructured-logging", "string-interpolation", "poor-logging");
        
        LogViolations(loggingViolations, "Logging Pattern Issues");
        
        // Structured logging should be used for better observability
        LogInformationalViolations(ruleResult, 
            new[] { "structured-logging", "observability", "monitoring" }, 
            "Logging Best Practices");
    }

    [Test]
    public async Task AllInfrastructureRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new ExternalServiceIsolationRule(),
            new DatabaseContextPatternRule(),
            new HttpClientPatternRule(),
            new ConfigurationBindingRule(),
            new CircuitBreakerRule(),
            new MessageBrokerPatternRule(),
            new CachingStrategyRule(),
            new DatabaseConnectionRule(),
            new FileSystemAbstractionRule(),
            new StructuredLoggingRule()
        };
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "Infrastructure Dependency Validation");
        
        // Assert
        ValidateFrameworkContext();
        AssertExecutionPerformance(result, TimeSpan.FromSeconds(45));
        
        // Critical infrastructure rules must pass
        var criticalFailures = result.RuleResults
            .Where(r => r.RuleId.StartsWith("IFD") && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("004") || 
                        r.RuleId.EndsWith("008")) && 
                       !r.IsSuccess)
            .ToList();

        criticalFailures.Count.ShouldBe(0, 
            $"Critical infrastructure rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");

        // Generate infrastructure health report
        await GenerateInfrastructureHealthReport(result);
    }

    [Test]
    public async Task ExternalServiceTimeouts_ShouldBeConfigured()
    {
        // Arrange
        var rule = new ServiceTimeoutRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD011");
        
        // Assert
        var timeoutViolations = FilterViolations(ruleResult, 
            "no-timeout", "default-timeout", "infinite-wait");
        
        LogViolations(timeoutViolations, "Service Timeout Configuration");
        
        // External services should have appropriate timeouts
        AssertNoSpecificViolations(ruleResult, 
            new[] { "critical-no-timeout", "blocking-call" }, 
            "Critical Timeout Issues");
    }

    [Test]
    public async Task RetryPolicies_ShouldBeImplemented()
    {
        // Arrange
        var rule = new RetryPolicyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD012");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "retry-policy", "exponential-backoff", "resilience" }, 
            "Retry Policy Patterns");
    }

    [Test]
    public async Task SecurityCredentials_ShouldBeSecurelyManaged()
    {
        // Arrange
        var rule = new CredentialManagementRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "IFD013");
        
        // Assert
        AssertRuleSuccess(ruleResult, "Credential Security Management");
        
        var securityViolations = FilterViolations(ruleResult, 
            "hardcoded-secret", "plain-text", "insecure-storage");
        
        securityViolations.Count.ShouldBe(0, 
            "Security credentials must be properly managed");
    }

    private async Task GenerateInfrastructureHealthReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== INFRASTRUCTURE LAYER HEALTH REPORT ===");
        
        var infraRules = result.RuleResults.Where(r => r.RuleId.StartsWith("IFD")).ToList();
        var passedRules = infraRules.Count(r => r.IsSuccess);
        var totalRules = infraRules.Count;
        
        await TestContext.Out.WriteLineAsync($"Infrastructure Health Score: {passedRules}/{totalRules} ({(passedRules * 100.0 / totalRules):F1}%)");
        
        // External Service integration health
        var externalServiceRules = infraRules.Where(r => 
            r.RuleId.EndsWith("001") || r.RuleId.EndsWith("003") || 
            r.RuleId.EndsWith("005") || r.RuleId.EndsWith("011") || r.RuleId.EndsWith("012"));
        var externalServiceHealth = externalServiceRules.Count(r => r.IsSuccess) * 100.0 / externalServiceRules.Count();
        
        await TestContext.Out.WriteLineAsync($"External Service Integration Health: {externalServiceHealth:F1}%");
        
        // Data Access health
        var dataAccessRules = infraRules.Where(r => 
            r.RuleId.EndsWith("002") || r.RuleId.EndsWith("007") || r.RuleId.EndsWith("008"));
        var dataAccessHealth = dataAccessRules.Count(r => r.IsSuccess) * 100.0 / dataAccessRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Data Access Pattern Health: {dataAccessHealth:F1}%");
        
        // Configuration and Security health
        var configSecurityRules = infraRules.Where(r => 
            r.RuleId.EndsWith("004") || r.RuleId.EndsWith("013"));
        var configSecurityHealth = configSecurityRules.Count(r => r.IsSuccess) * 100.0 / configSecurityRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Configuration & Security Health: {configSecurityHealth:F1}%");
        
        await TestContext.Out.WriteLineAsync("=============================================\n");
    }
}