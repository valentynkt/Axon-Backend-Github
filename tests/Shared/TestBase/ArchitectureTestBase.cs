using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;

namespace Axon.Tests.Shared.TestBase;

/// <summary>
/// Base class for Architecture tests providing common Framework utilities and patterns
/// </summary>
[TestFixture]
public abstract class ArchitectureTestBase
{
    protected RuleEngine RuleEngine { get; private set; } = null!;
    protected ArchitectureContext Context { get; private set; } = null!;
    protected ArchitectureSettings Configuration { get; private set; } = null!;

    [SetUp]
    public virtual void SetUp()
    {
        // Create fresh instances per test to ensure test isolation
        var assemblies = AssemblyAnalyzer.LoadProductionAssemblies();
        Configuration = new ArchitectureSettings();
        Context = new ArchitectureContext(assemblies, Configuration);
        RuleEngine = new RuleEngine();
    }

    /// <summary>
    /// Executes a single rule and validates the result with detailed violation reporting
    /// </summary>
    protected async Task<RuleResult> ExecuteRuleAndValidateAsync(IArchitectureRule rule, string expectedRuleId)
    {
        // Arrange
        RuleEngine.RegisterRule(rule);

        // Act
        var result = await RuleEngine.ExecuteAsync(Context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == expectedRuleId);
        
        if (!ruleResult.IsSuccess)
        {
            LogViolations(ruleResult, rule.Name);
        }

        return ruleResult;
    }

    /// <summary>
    /// Executes multiple rules and validates all results
    /// </summary>
    protected async Task<EngineResult> ExecuteRulesAndValidateAsync(IArchitectureRule[] rules, string testDescription)
    {
        // Arrange
        RuleEngine.RegisterRules(rules);

        // Act
        var result = await RuleEngine.ExecuteAsync(Context);

        // Assert
        result.ShouldNotBeNull();
        TestContext.WriteLine($"Execution Summary for {testDescription}: {result.GetSummary()}");
        
        foreach (var ruleResult in result.RuleResults.Where(r => !r.IsSuccess))
        {
            TestContext.WriteLine($"\nFailed Rule: {ruleResult.RuleId}");
            foreach (var violation in ruleResult.Violations)
            {
                TestContext.WriteLine($"  - {violation.Message} ({violation.TypeName})");
            }
        }

        return result;
    }

    /// <summary>
    /// Validates that a rule result is successful
    /// </summary>
    protected void AssertRuleSuccess(RuleResult ruleResult, string ruleName)
    {
        ruleResult.IsSuccess.ShouldBeTrue($"{ruleName} violations found: {ruleResult.Violations.Count}");
    }

    /// <summary>
    /// Validates that all rules in an execution result are successful
    /// </summary>
    protected void AssertAllRulesSuccess(EngineResult executionResult, string testDescription)
    {
        executionResult.IsSuccess.ShouldBeTrue($"{testDescription} violations found. Total violations: {executionResult.TotalViolations}");
    }

    /// <summary>
    /// Filters violations by specific criteria for focused testing
    /// </summary>
    protected IList<RuleViolation> FilterViolations(RuleResult ruleResult, params string[] keywords)
    {
        return ruleResult.Violations
            .Where(v => keywords.Any(keyword => 
                v.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Logs violations with detailed information for debugging
    /// </summary>
    protected void LogViolations(RuleResult ruleResult, string ruleName)
    {
        foreach (var violation in ruleResult.Violations)
        {
            TestContext.WriteLine($"{ruleName} Violation: {violation.Message}");
            TestContext.WriteLine($"Type: {violation.TypeName}");
            if (!string.IsNullOrEmpty(violation.SuggestedFix))
            {
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
            }
            TestContext.WriteLine("---");
        }
    }

    /// <summary>
    /// Logs a list of violations with detailed information for debugging
    /// </summary>
    protected void LogViolations(IList<RuleViolation> violations, string category)
    {
        foreach (var violation in violations)
        {
            TestContext.WriteLine($"{category} Violation: {violation.Message}");
            TestContext.WriteLine($"Type: {violation.TypeName}");
            if (!string.IsNullOrEmpty(violation.SuggestedFix))
            {
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
            }
            TestContext.WriteLine("---");
        }
    }

    /// <summary>
    /// Asserts that specific violation types don't exist (for critical issues)
    /// </summary>
    protected void AssertNoSpecificViolations(RuleResult ruleResult, string[] keywords, string violationType)
    {
        var specificViolations = FilterViolations(ruleResult, keywords);
        
        if (specificViolations.Any())
        {
            foreach (var violation in specificViolations)
            {
                TestContext.WriteLine($"{violationType} Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine("---");
            }
        }

        specificViolations.Count.ShouldBe(0, $"{violationType} violations found: {specificViolations.Count}");
    }

    /// <summary>
    /// Logs violations but doesn't fail the test (for informational purposes)
    /// </summary>
    protected void LogInformationalViolations(RuleResult ruleResult, string[] keywords, string violationType)
    {
        var informationalViolations = FilterViolations(ruleResult, keywords);
        
        if (informationalViolations.Any())
        {
            foreach (var violation in informationalViolations)
            {
                TestContext.WriteLine($"{violationType} (Informational): {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine("---");
            }
        }

        TestContext.WriteLine($"{violationType} occurrences (informational): {informationalViolations.Count}");
    }

    /// <summary>
    /// Creates a cancellation token with timeout for async tests
    /// </summary>
    protected CancellationToken CreateTimeoutToken(int milliseconds = 30000)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(milliseconds);
        return cts.Token;
    }

    /// <summary>
    /// Validates execution performance is within acceptable limits
    /// </summary>
    protected void AssertExecutionPerformance(EngineResult result, TimeSpan maxExpectedTime)
    {
        result.TotalExecutionTime.ShouldBeLessThan(maxExpectedTime, 
            $"Rule execution took {result.TotalExecutionTime.TotalMilliseconds}ms but should be under {maxExpectedTime.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Helper method to validate rule configuration
    /// </summary>
    protected void ValidateRuleConfiguration(IArchitectureRule rule)
    {
        rule.RuleId.ShouldNotBeNullOrEmpty("Rule ID must be specified");
        rule.Name.ShouldNotBeNullOrEmpty("Rule name must be specified");
        rule.Description.ShouldNotBeNullOrEmpty("Rule description must be specified");
        rule.Category.ShouldNotBeNullOrEmpty("Rule category must be specified");
        rule.Severity.ShouldBeOneOf(new[] { RuleSeverity.Info, RuleSeverity.Warning, RuleSeverity.Error, RuleSeverity.Critical }, "Rule severity must be specified");
    }

    /// <summary>
    /// Validates that the Framework context is properly initialized
    /// </summary>
    protected void ValidateFrameworkContext()
    {
        Context.ShouldNotBeNull("Architecture context should be initialized");
        Context.Assemblies.ShouldNotBeEmpty("Context should have loaded assemblies");
        Configuration.ShouldNotBeNull("Architecture configuration should be initialized");
        RuleEngine.ShouldNotBeNull("Rule engine should be initialized");
    }
}