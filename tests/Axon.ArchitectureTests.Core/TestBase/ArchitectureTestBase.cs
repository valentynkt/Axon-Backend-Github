using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Configuration;

namespace Axon.ArchitectureTests.Core.TestBase;

/// <summary>
/// Base class for architecture tests providing common functionality.
/// Simplified version to avoid circular dependencies.
/// </summary>
public abstract class ArchitectureTestBase
{
    protected static IArchitectureContext CreateArchitectureContext()
    {
        // Filter to only Axon application assemblies to avoid scanning system assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("Axon.", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
        
        var settings = new ArchitectureSettings();
        return new ArchitectureContext(assemblies, settings);
    }

    protected static async Task<EngineResult> ValidateRulesAsync(IArchitectureContext context, params IArchitectureRule[] rules)
    {
        var engine = new RuleEngine();
        foreach (var rule in rules)
        {
            engine.RegisterRule(rule);
        }
        return await engine.ExecuteAsync(context, CancellationToken.None);
    }

    protected static async Task<RuleResult> ExecuteRuleAndValidateAsync(IArchitectureRule rule, string? expectedRuleId = null)
    {
        var context = CreateArchitectureContext();
        var result = await rule.ValidateAsync(context);
        
        if (expectedRuleId != null && result.RuleId != expectedRuleId)
        {
            throw new InvalidOperationException($"Expected rule ID '{expectedRuleId}' but got '{result.RuleId}'");
        }
        
        return result;
    }

    protected static async Task<EngineResult> ExecuteRulesAndValidateAsync(params IArchitectureRule[] rules)
    {
        var context = CreateArchitectureContext();
        return await ValidateRulesAsync(context, rules);
    }

    protected static void AssertRuleSuccess(RuleResult result, string? testDescription = null)
    {
        var description = testDescription ?? $"Rule {result.RuleId}";
        result.IsSuccess.ShouldBeTrue($"{description} failed with violations: {string.Join(", ", result.Violations?.Select(v => v.Message) ?? Enumerable.Empty<string>())}");
    }

    protected static void AssertNoSpecificViolations(RuleResult result, string violationType)
    {
        var specificViolations = result.Violations?.Where(v => v.Message.Contains(violationType, StringComparison.OrdinalIgnoreCase)).ToList();
        specificViolations?.Count.ShouldBe(0, $"Found {specificViolations.Count} violations of type '{violationType}': {string.Join(", ", specificViolations.Select(v => v.Message))}");
    }

    protected static void AssertAllRulesSuccess(EngineResult result)
    {
        result.IsSuccess.ShouldBeTrue($"Engine execution failed. {result.GetSummary()}");
        var failedRules = result.RuleResults.Where(r => !r.IsSuccess).ToList();
        foreach (var failedRule in failedRules)
        {
            Console.WriteLine($"Failed Rule: {failedRule.RuleId}");
        }
    }
}