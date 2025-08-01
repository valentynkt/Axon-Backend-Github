using Axon.ArchitectureTests.Framework.Contracts;
using Shouldly;

namespace Axon.Tests.Shared.Extensions;

/// <summary>
/// Extension methods for Architecture testing with Framework integration and improved readability
/// </summary>
public static class ArchitectureTestExtensions
{
    /// <summary>
    /// Asserts that the architecture rule execution result is successful
    /// </summary>
    public static void ShouldBeCompliant(this EngineResult result)
    {
        result.IsSuccess.ShouldBeTrue($"Architecture violations found. Total violations: {result.TotalViolations}");
    }

    /// <summary>
    /// Asserts that the architecture rule execution result is successful with custom message
    /// </summary>
    public static void ShouldBeCompliant(this EngineResult result, string customMessage)
    {
        result.IsSuccess.ShouldBeTrue($"{customMessage}. Total violations: {result.TotalViolations}");
    }

    /// <summary>
    /// Asserts that a specific rule result is successful
    /// </summary>
    public static void ShouldBeCompliantFor(this RuleResult ruleResult, string ruleName)
    {
        ruleResult.IsSuccess.ShouldBeTrue($"{ruleName} violations found: {ruleResult.Violations.Count}");
    }

    /// <summary>
    /// Asserts that a rule result has no violations of specific types
    /// </summary>
    public static void ShouldHaveNoViolationsContaining(this RuleResult ruleResult, params string[] keywords)
    {
        var violations = ruleResult.Violations
            .Where(v => keywords.Any(keyword => 
                v.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        violations.Count.ShouldBe(0, $"Found {violations.Count} violations containing keywords: {string.Join(", ", keywords)}");
    }

    /// <summary>
    /// Gets violations that match specific keywords for detailed analysis
    /// </summary>
    public static IList<RuleViolation> GetViolationsContaining(this RuleResult ruleResult, params string[] keywords)
    {
        return ruleResult.Violations
            .Where(v => keywords.Any(keyword => 
                v.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Asserts that execution time is within acceptable limits
    /// </summary>
    public static void ShouldCompleteWithin(this EngineResult result, TimeSpan maxTime)
    {
        result.TotalExecutionTime.ShouldBeLessThan(maxTime, 
            $"Rule execution took {result.TotalExecutionTime.TotalMilliseconds}ms but should complete within {maxTime.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Asserts that the rule has proper configuration
    /// </summary>
    public static void ShouldHaveValidConfiguration(this IArchitectureRule rule)
    {
        rule.RuleId.ShouldNotBeNullOrEmpty("Rule ID must be specified");
        rule.Name.ShouldNotBeNullOrEmpty("Rule name must be specified");
        rule.Description.ShouldNotBeNullOrEmpty("Rule description must be specified");
        rule.Category.ShouldNotBeNullOrEmpty("Rule category must be specified");
        rule.Severity.ShouldBeOneOf(new[] { RuleSeverity.Info, RuleSeverity.Warning, RuleSeverity.Error, RuleSeverity.Critical }, "Rule severity must be specified");
    }

    /// <summary>
    /// Gets a summary string for the execution result for logging
    /// </summary>
    public static string GetDetailedSummary(this EngineResult result)
    {
        var summary = $"Execution Summary: {result.GetSummary()}\n";
        summary += $"Total Rules: {result.RuleResults.Count}\n";
        summary += $"Successful Rules: {result.RuleResults.Count(r => r.IsSuccess)}\n";
        summary += $"Failed Rules: {result.RuleResults.Count(r => !r.IsSuccess)}\n";
        summary += $"Total Execution Time: {result.TotalExecutionTime.TotalMilliseconds}ms\n";
        
        if (!result.IsSuccess)
        {
            summary += "\nFailed Rules:\n";
            foreach (var failedRule in result.RuleResults.Where(r => !r.IsSuccess))
            {
                summary += $"  - {failedRule.RuleId}: {failedRule.Violations.Count} violations\n";
            }
        }

        return summary;
    }

    /// <summary>
    /// Validates that the architecture context is properly configured
    /// </summary>
    public static void ShouldBeValidContext(this IArchitectureContext context)
    {
        context.ShouldNotBeNull("Architecture context should be initialized");
        context.Assemblies.ShouldNotBeEmpty("Context should have loaded assemblies");
        context.Configuration.ShouldNotBeNull("Architecture configuration should be initialized");
    }

    /// <summary>
    /// Gets violation summary for a specific rule result
    /// </summary>
    public static string GetViolationSummary(this RuleResult ruleResult)
    {
        if (ruleResult.IsSuccess)
            return $"Rule {ruleResult.RuleId}: Compliant (No violations)";

        var summary = $"Rule {ruleResult.RuleId}: Non-compliant ({ruleResult.Violations.Count} violations)\n";
        
        var groupedViolations = ruleResult.Violations
            .GroupBy(v => v.Severity)
            .OrderByDescending(g => g.Key);

        foreach (var group in groupedViolations)
        {
            summary += $"  {group.Key}: {group.Count()} violations\n";
            foreach (var violation in group.Take(3)) // Show first 3 violations per severity
            {
                summary += $"    - {violation.Message} ({violation.TypeName})\n";
            }
            if (group.Count() > 3)
            {
                summary += $"    ... and {group.Count() - 3} more\n";
            }
        }

        return summary;
    }

    /// <summary>
    /// Filters violations by severity level
    /// </summary>
    public static IList<RuleViolation> GetViolationsBySeverity(this RuleResult ruleResult, RuleSeverity severity)
    {
        return ruleResult.Violations.Where(v => v.Severity == severity).ToList();
    }

    /// <summary>
    /// Asserts that there are no critical violations
    /// </summary>
    public static void ShouldHaveNoCriticalViolations(this RuleResult ruleResult)
    {
        var criticalViolations = ruleResult.GetViolationsBySeverity(RuleSeverity.Critical);
        criticalViolations.Count.ShouldBe(0, $"Found {criticalViolations.Count} critical violations that must be resolved");
    }

    /// <summary>
    /// Asserts that there are no high severity violations
    /// </summary>
    public static void ShouldHaveNoHighSeverityViolations(this RuleResult ruleResult)
    {
        var highViolations = ruleResult.GetViolationsBySeverity(RuleSeverity.Error);
        highViolations.Count.ShouldBe(0, $"Found {highViolations.Count} high severity violations that should be resolved");
    }
}