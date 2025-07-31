using System.Diagnostics;
using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.ArchitectureTests.Framework.Logging;

/// <summary>
/// Logger for architecture test execution with performance metrics.
/// </summary>
public sealed class ArchitectureTestLogger
{
    private static readonly Lazy<ArchitectureTestLogger> _instance = new(() => new ArchitectureTestLogger());
    
    /// <summary>
    /// Gets the singleton instance of the logger.
    /// </summary>
    public static ArchitectureTestLogger Instance => _instance.Value;

    /// <summary>
    /// Logs the start of rule execution.
    /// </summary>
    public void LogRuleStarted(string ruleId, string ruleName)
    {
        if (IsDetailedLoggingEnabled())
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting rule: {ruleId} - {ruleName}");
        }
    }

    /// <summary>
    /// Logs the completion of rule execution.
    /// </summary>
    public void LogRuleCompleted(RuleResult result)
    {
        if (IsDetailedLoggingEnabled())
        {
            var status = result.IsSuccess ? "PASSED" : "FAILED";
            var violationCount = result.Violations.Count;
            
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Completed rule: {result.RuleId} - {status} " +
                            $"({result.ExecutionTime.TotalMilliseconds:F0}ms, {violationCount} violations)");
        }
    }

    /// <summary>
    /// Logs the start of engine execution.
    /// </summary>
    public void LogEngineStarted(int ruleCount)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Architecture validation started with {ruleCount} rules");
    }

    /// <summary>
    /// Logs the completion of engine execution.
    /// </summary>
    public void LogEngineCompleted(EngineResult result)
    {
        var status = result.IsSuccess ? "PASSED" : "FAILED";
        var passedCount = result.RuleResults.Count(r => r.IsSuccess);
        var failedCount = result.RuleResults.Count(r => !r.IsSuccess);

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Architecture validation completed: {status}");
        Console.WriteLine($"  Total Time: {result.TotalExecutionTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Rules: {passedCount} passed, {failedCount} failed");
        Console.WriteLine($"  Violations: {result.TotalViolations} total");

        if (IsPerformanceMetricsEnabled())
        {
            LogPerformanceMetrics(result);
        }
    }

    /// <summary>
    /// Logs a rule violation.
    /// </summary>
    public void LogViolation(RuleViolation violation)
    {
        var severityColor = GetSeverityColor(violation.Severity);
        Console.WriteLine($"  {severityColor}[{violation.Severity}]{ResetColor()} {violation.TypeName}: {violation.Message}");
        
        if (!string.IsNullOrEmpty(violation.SuggestedFix))
        {
            Console.WriteLine($"    💡 Suggested fix: {violation.SuggestedFix}");
        }
    }

    /// <summary>
    /// Logs performance metrics for rules.
    /// </summary>
    private void LogPerformanceMetrics(EngineResult result)
    {
        Console.WriteLine("\n📊 Performance Metrics:");
        
        var slowestRules = result.RuleResults
            .OrderByDescending(r => r.ExecutionTime)
            .Take(5);

        foreach (var rule in slowestRules)
        {
            Console.WriteLine($"  {rule.RuleId}: {rule.ExecutionTime.TotalMilliseconds:F0}ms");
        }

        var avgExecutionTime = result.RuleResults.Average(r => r.ExecutionTime.TotalMilliseconds);
        Console.WriteLine($"  Average rule execution: {avgExecutionTime:F0}ms");
    }

    private static bool IsDetailedLoggingEnabled() => 
        Environment.GetEnvironmentVariable("ARCHITECTURE_DETAILED_LOGGING") == "true";

    private static bool IsPerformanceMetricsEnabled() => 
        Environment.GetEnvironmentVariable("ARCHITECTURE_PERFORMANCE_METRICS") != "false";

    private static string GetSeverityColor(RuleSeverity severity) => severity switch
    {
        RuleSeverity.Critical => "\u001b[91m", // Bright red
        RuleSeverity.Error => "\u001b[31m",    // Red
        RuleSeverity.Warning => "\u001b[33m",  // Yellow
        RuleSeverity.Info => "\u001b[36m",     // Cyan
        _ => ""
    };

    private static string ResetColor() => "\u001b[0m";
}