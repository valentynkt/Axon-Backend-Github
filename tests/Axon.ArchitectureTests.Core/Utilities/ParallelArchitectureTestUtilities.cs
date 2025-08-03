using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
// using Axon.Tests.Shared.Tests.Utilities; // Removed due to circular dependency

namespace Axon.ArchitectureTests.Core.Utilities;

/// <summary>
/// Utilities for parallel architecture test execution and coordination.
/// </summary>
public static class ParallelArchitectureTestUtilities
{
    /// <summary>
    /// Executes multiple rule sets in parallel with intelligent load balancing.
    /// </summary>
    public static async Task<ParallelExecutionResults> ExecuteRuleSetsInParallelAsync(
        IArchitectureContext context,
        Dictionary<string, List<IArchitectureRule>> ruleSets,
        int maxConcurrency = 0,
        CancellationToken cancellationToken = default)
    {
        maxConcurrency = maxConcurrency <= 0 ? Environment.ProcessorCount : maxConcurrency;
        var results = new ConcurrentDictionary<string, EngineResult>();
        var timings = new ConcurrentDictionary<string, TimeSpan>();
        var overallStopwatch = Stopwatch.StartNew();

        using (var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency))
        {
            var tasks = ruleSets.Select(async ruleSet =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var engine = new RuleEngine();
                    engine.RegisterRules(ruleSet.Value);
                    
                    var result = await engine.ExecuteAsync(context, cancellationToken);
                    stopwatch.Stop();
                    
                    results[ruleSet.Key] = result;
                    timings[ruleSet.Key] = stopwatch.Elapsed;
                    
                    Console.WriteLine($"✅ Completed rule set: {ruleSet.Key} ({ruleSet.Value.Count} rules) in {stopwatch.Elapsed.TotalSeconds:F2}s");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed rule set: {ruleSet.Key} - {ex.Message}");
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }

        overallStopwatch.Stop();
        
        return new ParallelExecutionResults
        {
            Results = results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ExecutionTimes = timings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            TotalExecutionTime = overallStopwatch.Elapsed,
            ParallelEfficiency = CalculateParallelEfficiency(timings.Values, overallStopwatch.Elapsed),
            MaxConcurrency = maxConcurrency
        };
    }

    /// <summary>
    /// Creates optimized rule distribution for parallel execution.
    /// </summary>
    public static Dictionary<string, List<IArchitectureRule>> CreateOptimalRuleDistribution(
        List<IArchitectureRule> allRules, 
        int targetGroups = 0)
    {
        targetGroups = targetGroups <= 0 ? Environment.ProcessorCount : targetGroups;
        var ruleGroups = new Dictionary<string, List<IArchitectureRule>>();
        
        // Group rules by category for logical separation
        var categorizedRules = allRules.GroupBy(r => r.Category).ToList();
        
        // Distribute categories across groups
        for (int i = 0; i < categorizedRules.Count; i++)
        {
            var groupName = $"RuleGroup_{(i % targetGroups) + 1}";
            
            if (!ruleGroups.TryGetValue(groupName, out List<IArchitectureRule>? value))
            {
                value = new List<IArchitectureRule>();
                ruleGroups[groupName] = value;
            }

            value.AddRange(categorizedRules[i]);
        }
        
        // Balance groups by moving rules if needed
        BalanceRuleGroups(ruleGroups);
        
        return ruleGroups;
    }

    /// <summary>
    /// Analyzes architecture test performance and suggests optimizations.
    /// </summary>
    public static PerformanceAnalysisResult AnalyzeTestPerformance(
        ParallelExecutionResults results)
    {
        var analysis = new PerformanceAnalysisResult
        {
            TotalRulesExecuted = results.Results.Values.Sum(r => r.RuleResults.Count),
            AverageRuleExecutionTime = CalculateAverageRuleExecutionTime(results),
            BottleneckCategories = IdentifyBottleneckCategories(results),
            OptimizationSuggestions = GenerateOptimizationSuggestions(results),
            ParallelizationBenefit = CalculateParallelizationBenefit(results),
            ResourceUtilization = CalculateResourceUtilization(results)
        };
        
        return analysis;
    }

    /// <summary>
    /// Validates architecture test quality and coverage.
    /// </summary>
    public static TestQualityMetrics ValidateTestQuality(
        List<IArchitectureRule> rules,
        IArchitectureContext context)
    {
        var metrics = new TestQualityMetrics();
        
        // Analyze rule coverage
        metrics.CategoryCoverage = AnalyzeCategoryCoverage(rules);
        metrics.SeverityDistribution = AnalyzeSeverityDistribution(rules);
        metrics.CodeCoverageEstimate = EstimateCodeCoverage(rules, context);
        metrics.RuleComplexityScore = CalculateRuleComplexity(rules);
        metrics.DuplicateRuleDetection = DetectDuplicateRules(rules);
        
        return metrics;
    }

    /// <summary>
    /// Generates comprehensive architecture health dashboard data.
    /// </summary>
    public static ArchitectureHealthDashboard GenerateHealthDashboard(
        ParallelExecutionResults results)
    {
        var dashboard = new ArchitectureHealthDashboard
        {
            GeneratedAt = DateTime.UtcNow,
            OverallHealthScore = CalculateOverallHealthScore(results),
            CategoryScores = CalculateCategoryScores(results),
            TrendAnalysis = AnalyzeTrends(),
            RiskAssessment = AssessArchitectureRisks(results),
            ComplianceStatus = AssessComplianceStatus(results),
            RecommendedActions = GenerateRecommendedActions(results)
        };
        
        return dashboard;
    }

    /// <summary>
    /// Creates architecture test execution plan with optimal scheduling.
    /// </summary>
    public static ExecutionPlan CreateOptimalExecutionPlan(
        List<IArchitectureRule> rules,
        ExecutionConstraints constraints)
    {
        var plan = new ExecutionPlan
        {
            TotalRules = rules.Count,
            EstimatedDuration = EstimateExecutionDuration(rules, constraints),
            ExecutionPhases = CreateExecutionPhases(rules),
            ResourceRequirements = CalculateResourceRequirements(rules),
            RiskMitigation = CreateRiskMitigationStrategy()
        };
        
        return plan;
    }

    #region Private Helper Methods

    private static double CalculateParallelEfficiency(IEnumerable<TimeSpan> individualTimes, TimeSpan totalTime)
    {
        var sequentialTime = individualTimes.Sum(t => t.TotalSeconds);
        return totalTime.TotalSeconds > 0 ? sequentialTime / totalTime.TotalSeconds : 1.0;
    }

    private static void BalanceRuleGroups(Dictionary<string, List<IArchitectureRule>> groups)
    {
        var averageSize = groups.Values.Sum(g => g.Count) / groups.Count;
        var tolerance = Math.Max(1, averageSize / 4); // 25% tolerance
        
        var oversizedGroups = groups.Where(g => g.Value.Count > averageSize + tolerance).ToList();
        var undersizedGroups = groups.Where(g => g.Value.Count < averageSize - tolerance).ToList();
        
        foreach (var oversized in oversizedGroups)
        {
            while (oversized.Value.Count > averageSize + tolerance && undersizedGroups.Any())
            {
                var undersized = undersizedGroups.First(g => g.Value.Count < averageSize - tolerance);
                var ruleToMove = oversized.Value.Last();
                
                oversized.Value.Remove(ruleToMove);
                undersized.Value.Add(ruleToMove);
                
                if (undersized.Value.Count >= averageSize - tolerance)
                {
                    undersizedGroups.Remove(undersized);
                }
            }
        }
    }

    private static TimeSpan CalculateAverageRuleExecutionTime(ParallelExecutionResults results)
    {
        var totalRules = results.Results.Values.Sum(r => r.RuleResults.Count);
        var totalTime = results.ExecutionTimes.Values.Sum(t => t.TotalMilliseconds);
        
        return totalRules > 0 ? TimeSpan.FromMilliseconds(totalTime / totalRules) : TimeSpan.Zero;
    }

    private static List<string> IdentifyBottleneckCategories(ParallelExecutionResults results)
    {
        var categoryTimes = new Dictionary<string, double>();
        
        foreach (var result in results.Results)
        {
            var executionTime = results.ExecutionTimes[result.Key].TotalMilliseconds;
            var ruleCount = result.Value.RuleResults.Count;
            var avgTimePerRule = ruleCount > 0 ? executionTime / ruleCount : 0;
            
            categoryTimes[result.Key] = avgTimePerRule;
        }
        
        var averageTime = categoryTimes.Values.Average();
        return categoryTimes.Where(kvp => kvp.Value > averageTime * 1.5).Select(kvp => kvp.Key).ToList();
    }

    private static List<string> GenerateOptimizationSuggestions(ParallelExecutionResults results)
    {
        var suggestions = new List<string>();
        
        if (results.ParallelEfficiency < 2.0)
        {
            suggestions.Add("Consider increasing parallel execution groups for better CPU utilization");
        }
        
        var slowestCategory = results.ExecutionTimes.OrderByDescending(kvp => kvp.Value).First();
        if (slowestCategory.Value.TotalSeconds > 30)
        {
            suggestions.Add($"Optimize rules in category '{slowestCategory.Key}' - execution time exceeds 30 seconds");
        }
        
        var totalFailures = results.Results.Values.Sum(r => r.RuleResults.Count(rr => !rr.IsSuccess));
        if (totalFailures > 0)
        {
            suggestions.Add($"Address {totalFailures} rule violations to improve architecture health");
        }
        
        return suggestions;
    }

    private static double CalculateParallelizationBenefit(ParallelExecutionResults results)
    {
        var sequentialTime = results.ExecutionTimes.Values.Sum(t => t.TotalSeconds);
        var parallelTime = results.TotalExecutionTime.TotalSeconds;
        
        return parallelTime > 0 ? (sequentialTime - parallelTime) / sequentialTime * 100 : 0;
    }

    private static ResourceUtilizationMetrics CalculateResourceUtilization(ParallelExecutionResults results)
    {
        return new ResourceUtilizationMetrics
        {
            CpuUtilization = Math.Min(100.0, results.ParallelEfficiency * 100 / Environment.ProcessorCount),
            MemoryUtilization = EstimateMemoryUsage(results),
            ConcurrencyUtilization = (double)results.MaxConcurrency / Environment.ProcessorCount * 100
        };
    }

    private static Dictionary<string, double> AnalyzeCategoryCoverage(List<IArchitectureRule> rules)
    {
        var categories = rules.GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => (double)g.Count());
            
        var total = categories.Values.Sum();
        return categories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / total * 100);
    }

    private static Dictionary<RuleSeverity, int> AnalyzeSeverityDistribution(List<IArchitectureRule> rules)
    {
        return rules.GroupBy(r => r.Severity)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static double EstimateCodeCoverage(List<IArchitectureRule> rules, IArchitectureContext context)
    {
        var totalTypes = context.Types.Count();
        var rulesAnalyzingTypes = rules.Count(r => r.Description.Contains("type") || r.Description.Contains("class"));
        
        return totalTypes > 0 ? Math.Min(100.0, (double)rulesAnalyzingTypes / totalTypes * 100) : 0;
    }

    private static double CalculateRuleComplexity(List<IArchitectureRule> rules)
    {
        // Simplified complexity calculation based on rule description length and category
        return rules.Average(r => r.Description.Length + (r.Category.Split(' ').Length * 10));
    }

    private static List<string> DetectDuplicateRules(List<IArchitectureRule> rules)
    {
        return rules.GroupBy(r => r.Description)
            .Where(g => g.Count() > 1)
            .Select(g => $"Potential duplicate: {g.Key}")
            .ToList();
    }

    private static double CalculateOverallHealthScore(ParallelExecutionResults results)
    {
        var totalRules = results.Results.Values.Sum(r => r.RuleResults.Count);
        var passedRules = results.Results.Values.Sum(r => r.RuleResults.Count(rr => rr.IsSuccess));
        
        return totalRules > 0 ? (double)passedRules / totalRules * 100 : 100;
    }

    private static Dictionary<string, double> CalculateCategoryScores(ParallelExecutionResults results)
    {
        var categoryScores = new Dictionary<string, double>();
        
        foreach (var result in results.Results)
        {
            var totalRules = result.Value.RuleResults.Count;
            var passedRules = result.Value.RuleResults.Count(r => r.IsSuccess);
            
            categoryScores[result.Key] = totalRules > 0 ? (double)passedRules / totalRules * 100 : 100;
        }
        
        return categoryScores;
    }

    private static TrendAnalysis AnalyzeTrends()
    {
        // Simplified trend analysis - in practice, would compare with historical data
        return new TrendAnalysis
        {
            OverallTrend = "Stable",
            ImprovingCategories = new List<string>(),
            DegradingCategories = new List<string>()
        };
    }

    private static RiskAssessment AssessArchitectureRisks(ParallelExecutionResults results)
    {
        var criticalFailures = results.Results.Values
            .SelectMany(r => r.RuleResults)
            .Count(rr => !rr.IsSuccess);
            
        return new RiskAssessment
        {
            RiskLevel = criticalFailures > 10 ? "High" : criticalFailures > 5 ? "Medium" : "Low",
            CriticalIssues = criticalFailures,
            RiskFactors = GenerateRiskFactors(results)
        };
    }

    private static ComplianceStatus AssessComplianceStatus(ParallelExecutionResults results)
    {
        var overallScore = CalculateOverallHealthScore(results);
        
        return new ComplianceStatus
        {
            IsCompliant = overallScore >= 80,
            ComplianceScore = overallScore,
            NonCompliantAreas = results.Results
                .Where(r => CalculateCategoryScore(r.Value) < 80)
                .Select(r => r.Key)
                .ToList()
        };
    }

    private static List<string> GenerateRecommendedActions(ParallelExecutionResults results)
    {
        var actions = new List<string>();
        var overallScore = CalculateOverallHealthScore(results);
        
        if (overallScore < 90)
        {
            actions.Add("Review and address failing architecture rules");
        }
        
        if (results.ParallelEfficiency < 2.0)
        {
            actions.Add("Optimize test execution for better parallel performance");
        }
        
        return actions;
    }

    private static double EstimateMemoryUsage(ParallelExecutionResults results)
    {
        // Simplified memory estimation
        var totalRules = results.Results.Values.Sum(r => r.RuleResults.Count);
        return Math.Min(100.0, totalRules * 0.1); // Assume 0.1% per rule
    }

    private static double CalculateCategoryScore(EngineResult result)
    {
        var totalRules = result.RuleResults.Count;
        var passedRules = result.RuleResults.Count(r => r.IsSuccess);
        
        return totalRules > 0 ? (double)passedRules / totalRules * 100 : 100;
    }

    private static List<string> GenerateRiskFactors(ParallelExecutionResults results)
    {
        var factors = new List<string>();
        
        var totalFailures = results.Results.Values.Sum(r => r.RuleResults.Count(rr => !rr.IsSuccess));
        if (totalFailures > 0)
        {
            factors.Add($"{totalFailures} architecture rule violations");
        }
        
        return factors;
    }

    private static TimeSpan EstimateExecutionDuration(List<IArchitectureRule> rules, ExecutionConstraints constraints)
    {
        // Simplified estimation - 100ms per rule on average
        var estimatedMs = rules.Count * 100;
        var parallelFactor = Math.Min(constraints.MaxConcurrency, Environment.ProcessorCount);
        
        return TimeSpan.FromMilliseconds(estimatedMs / parallelFactor);
    }

    private static List<ExecutionPhase> CreateExecutionPhases(List<IArchitectureRule> rules)
    {
        var phases = new List<ExecutionPhase>();
        var criticalRules = rules.Where(r => r.Severity == RuleSeverity.Critical).ToList();
        var otherRules = rules.Except(criticalRules).ToList();
        
        if (criticalRules.Any())
        {
            phases.Add(new ExecutionPhase
            {
                Name = "Critical Rules",
                Rules = criticalRules,
                Priority = 1
            });
        }
        
        if (otherRules.Any())
        {
            phases.Add(new ExecutionPhase
            {
                Name = "Standard Rules",
                Rules = otherRules,
                Priority = 2
            });
        }
        
        return phases;
    }

    private static ResourceRequirements CalculateResourceRequirements(List<IArchitectureRule> rules)
    {
        return new ResourceRequirements
        {
            EstimatedMemoryMB = rules.Count * 10, // 10MB per rule estimate
            EstimatedCpuUsage = Math.Min(100, rules.Count * 0.5), // 0.5% per rule
            RecommendedConcurrency = Environment.ProcessorCount
        };
    }

    private static RiskMitigationStrategy CreateRiskMitigationStrategy()
    {
        return new RiskMitigationStrategy
        {
            TimeoutStrategy = "Progressive timeout with 2-minute initial, 5-minute maximum",
            FailureHandling = "Continue on non-critical failures, stop on critical failures",
            ResourceMonitoring = "Monitor memory usage and CPU utilization during execution"
        };
    }

    #endregion
}

#region Supporting Data Models

public class ParallelExecutionResults
{
    public Dictionary<string, EngineResult> Results { get; set; } = new();
    public Dictionary<string, TimeSpan> ExecutionTimes { get; set; } = new();
    public TimeSpan TotalExecutionTime { get; set; }
    public double ParallelEfficiency { get; set; }
    public int MaxConcurrency { get; set; }
}

public class PerformanceAnalysisResult
{
    public int TotalRulesExecuted { get; set; }
    public TimeSpan AverageRuleExecutionTime { get; set; }
    public List<string> BottleneckCategories { get; set; } = new();
    public List<string> OptimizationSuggestions { get; set; } = new();
    public double ParallelizationBenefit { get; set; }
    public ResourceUtilizationMetrics ResourceUtilization { get; set; } = new();
}

public class TestQualityMetrics
{
    public Dictionary<string, double> CategoryCoverage { get; set; } = new();
    public Dictionary<RuleSeverity, int> SeverityDistribution { get; set; } = new();
    public double CodeCoverageEstimate { get; set; }
    public double RuleComplexityScore { get; set; }
    public List<string> DuplicateRuleDetection { get; set; } = new();
}

public class ArchitectureHealthDashboard
{
    public DateTime GeneratedAt { get; set; }
    public double OverallHealthScore { get; set; }
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public TrendAnalysis TrendAnalysis { get; set; } = new();
    public RiskAssessment RiskAssessment { get; set; } = new();
    public ComplianceStatus ComplianceStatus { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}

public class ExecutionPlan
{
    public int TotalRules { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public List<ExecutionPhase> ExecutionPhases { get; set; } = new();
    public ResourceRequirements ResourceRequirements { get; set; } = new();
    public RiskMitigationStrategy RiskMitigation { get; set; } = new();
}

public class ResourceUtilizationMetrics
{
    public double CpuUtilization { get; set; }
    public double MemoryUtilization { get; set; }
    public double ConcurrencyUtilization { get; set; }
}

public class TrendAnalysis
{
    public string OverallTrend { get; set; } = string.Empty;
    public List<string> ImprovingCategories { get; set; } = new();
    public List<string> DegradingCategories { get; set; } = new();
}

public class RiskAssessment
{
    public string RiskLevel { get; set; } = string.Empty;
    public int CriticalIssues { get; set; }
    public List<string> RiskFactors { get; set; } = new();
}

public class ComplianceStatus
{
    public bool IsCompliant { get; set; }
    public double ComplianceScore { get; set; }
    public List<string> NonCompliantAreas { get; set; } = new();
}

public class ExecutionConstraints
{
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(10);
    public long MaxMemoryUsageMB { get; set; } = 1024;
}

public class ExecutionPhase
{
    public string Name { get; set; } = string.Empty;
    public List<IArchitectureRule> Rules { get; set; } = new();
    public int Priority { get; set; }
}

public class ResourceRequirements
{
    public int EstimatedMemoryMB { get; set; }
    public double EstimatedCpuUsage { get; set; }
    public int RecommendedConcurrency { get; set; }
}

public class RiskMitigationStrategy
{
    public string TimeoutStrategy { get; set; } = string.Empty;
    public string FailureHandling { get; set; } = string.Empty;
    public string ResourceMonitoring { get; set; } = string.Empty;
}

#endregion