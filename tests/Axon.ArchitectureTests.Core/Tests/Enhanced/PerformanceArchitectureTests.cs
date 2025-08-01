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
/// Performance architecture tests with scalability pattern validation.
/// Ensures architectural patterns support performance and scalability requirements.
/// </summary>
[TestFixture]
public sealed class PerformanceArchitectureTests : ArchitectureTestBase
{
    [Test]
    public async Task AsyncPatterns_ShouldBeUsedAppropriately()
    {
        // Arrange
        var rule = new AsyncPatternRule();
        ValidateRuleConfiguration(rule);
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF001");
        
        // Assert
        var syncOverAsyncViolations = FilterViolations(ruleResult, 
            "sync-over-async", "blocking-async", "task-result");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "sync-over-async", "blocking-async" }, 
            "Critical Async Anti-patterns");
        
        LogInformationalViolations(ruleResult, 
            new[] { "async-pattern", "non-blocking", "scalability" }, 
            "Async Pattern Usage");
    }

    [Test]
    public async Task CachingStrategies_ShouldImprovePerformance()
    {
        // Arrange
        var rule = new PerformanceCachingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF002");
        
        // Assert
        var cachingOpportunityViolations = FilterViolations(ruleResult, 
            "missing-cache", "repeated-computation", "inefficient-lookup");
        
        LogViolations(cachingOpportunityViolations, "Caching Opportunities");
        
        LogInformationalViolations(ruleResult, 
            new[] { "caching-strategy", "memory-cache", "distributed-cache" }, 
            "Caching Patterns");
    }

    [Test]
    public async Task DatabaseQueries_ShouldBeOptimized()
    {
        // Arrange
        var rule = new DatabaseQueryOptimizationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF003");
        
        // Assert
        var queryPerformanceViolations = FilterViolations(ruleResult, 
            "n+1-problem", "missing-index", "full-table-scan", "inefficient-query");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "n+1-problem", "critical-performance" }, 
            "Critical Database Performance Issues");
        
        LogViolations(queryPerformanceViolations, "Database Query Performance");
    }

    [Test]
    public async Task MemoryUsage_ShouldBeEfficient()
    {
        // Arrange
        var rule = new MemoryEfficiencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        
        // Assert
        var memoryViolations = FilterViolations(ruleResult, 
            "memory-leak", "large-object-heap", "excessive-allocation");
        
        LogViolations(memoryViolations, "Memory Usage Issues");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "memory-leak", "unmanaged-resource" }, 
            "Critical Memory Issues");
    }

    [Test]
    public async Task ConcurrencyPatterns_ShouldBeThreadSafe()
    {
        // Arrange
        var rule = new ConcurrencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF005");
        
        // Assert
        var concurrencyViolations = FilterViolations(ruleResult, 
            "race-condition", "deadlock-risk", "thread-unsafe");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "race-condition", "deadlock-risk" }, 
            "Critical Concurrency Issues");
        
        LogViolations(concurrencyViolations, "Concurrency Pattern Issues");
    }

    [Test]
    public async Task IoOperations_ShouldBeAsynchronous()
    {
        // Arrange
        var rule = new IoAsyncRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF006");
        
        // Assert
        var syncIoViolations = FilterViolations(ruleResult, 
            "sync-io", "blocking-io", "file-sync", "network-sync");
        
        LogViolations(syncIoViolations, "Synchronous I/O Operations");
        
        // I/O operations should be async for better scalability
        LogInformationalViolations(ruleResult, 
            new[] { "async-io", "non-blocking", "throughput" }, 
            "Async I/O Patterns");
    }

    [Test]
    public async Task ResourcePools_ShouldBeUsedForExpensiveResources()
    {
        // Arrange
        var rule = new ResourcePoolingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF007");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "object-pooling", "connection-pooling", "resource-reuse" }, 
            "Resource Pooling Opportunities");
        
        var poolingViolations = FilterViolations(ruleResult, 
            "missing-pooling", "resource-waste", "expensive-creation");
        
        LogViolations(poolingViolations, "Resource Pooling Issues");
    }

    [Test]
    public async Task LazyLoading_ShouldBeUsedAppropriately()
    {
        // Arrange
        var rule = new LazyLoadingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF008");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "lazy-loading", "deferred-execution", "initialization" }, 
            "Lazy Loading Patterns");
        
        var eagerLoadingViolations = FilterViolations(ruleResult, 
            "eager-loading", "premature-initialization", "unnecessary-computation");
        
        LogViolations(eagerLoadingViolations, "Eager Loading Issues");
    }

    [Test]
    public async Task SerializationPerformance_ShouldBeOptimized()
    {
        // Arrange
        var rule = new SerializationPerformanceRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF009");
        
        // Assert
        var serializationViolations = FilterViolations(ruleResult, 
            "inefficient-serialization", "reflection-serialization", "xml-serialization");
        
        LogViolations(serializationViolations, "Serialization Performance Issues");
        
        LogInformationalViolations(ruleResult, 
            new[] { "json-serialization", "binary-serialization", "custom-serialization" }, 
            "Serialization Optimization");
    }

    [Test]
    public async Task CollectionOperations_ShouldBeEfficient()
    {
        // Arrange
        var rule = new CollectionEfficiencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF010");
        
        // Assert
        var collectionViolations = FilterViolations(ruleResult, 
            "inefficient-collection", "wrong-collection-type", "unnecessary-iteration");
        
        LogViolations(collectionViolations, "Collection Efficiency Issues");
        
        LogInformationalViolations(ruleResult, 
            new[] { "collection-optimization", "linq-performance", "enumeration" }, 
            "Collection Performance Patterns");
    }

    [Test]
    public async Task EventProcessing_ShouldScaleHorizontally()
    {
        // Arrange
        var rule = new EventProcessingScalabilityRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF011");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "horizontal-scaling", "event-partitioning", "parallel-processing" }, 
            "Event Processing Scalability");
    }

    [Test]
    public async Task HttpRequestProcessing_ShouldBeOptimized()
    {
        // Arrange
        var rule = new HttpPerformanceRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF012");
        
        // Assert
        var httpViolations = FilterViolations(ruleResult, 
            "sync-controller", "inefficient-binding", "missing-compression");
        
        LogViolations(httpViolations, "HTTP Performance Issues");
        
        LogInformationalViolations(ruleResult, 
            new[] { "async-controller", "response-compression", "minimal-apis" }, 
            "HTTP Performance Optimizations");
    }

    [Test]
    public async Task AllPerformanceRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new AsyncPatternRule(),
            new PerformanceCachingRule(),
            new DatabaseQueryOptimizationRule(),
            new MemoryEfficiencyRule(),
            new ConcurrencyRule(),
            new IoAsyncRule(),
            new ResourcePoolingRule(),
            new LazyLoadingRule(),
            new SerializationPerformanceRule(),
            new CollectionEfficiencyRule(),
            new EventProcessingScalabilityRule(),
            new HttpPerformanceRule()
        };
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "Performance Architecture Validation");
        
        // Assert
        ValidateFrameworkContext();
        AssertExecutionPerformance(result, TimeSpan.FromSeconds(60));
        
        // Critical performance rules must pass
        var criticalFailures = result.RuleResults
            .Where(r => r.RuleId.StartsWith("PERF") && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("003") || 
                        r.RuleId.EndsWith("004") || r.RuleId.EndsWith("005")) && 
                       !r.IsSuccess)
            .ToList();

        criticalFailures.Count.ShouldBe(0, 
            $"Critical performance rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");

        // Generate performance analysis report
        await GeneratePerformanceAnalysisReport(result);
    }

    [Test]
    public async Task PerformanceBenchmarks_ShouldMeetTargets()
    {
        // Arrange
        var rule = new PerformanceBenchmarkRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF013");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "latency", "throughput", "resource-usage" }, 
            "Performance Benchmarks");
    }

    [Test]
    public async Task ScalabilityPatterns_ShouldSupportGrowth()
    {
        // Arrange
        var rule = new ScalabilityPatternRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF014");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "horizontal-scaling", "stateless-design", "load-distribution" }, 
            "Scalability Patterns");
        
        var scalabilityBottlenecks = FilterViolations(ruleResult, 
            "single-point-failure", "stateful-bottleneck", "centralized-processing");
        
        LogViolations(scalabilityBottlenecks, "Scalability Bottlenecks");
    }

    private async Task GeneratePerformanceAnalysisReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== PERFORMANCE ARCHITECTURE ANALYSIS REPORT ===");
        
        var perfRules = result.RuleResults.Where(r => r.RuleId.StartsWith("PERF")).ToList();
        var passedRules = perfRules.Count(r => r.IsSuccess);
        var totalRules = perfRules.Count;
        
        await TestContext.Out.WriteLineAsync($"Performance Architecture Score: {passedRules}/{totalRules} ({(passedRules * 100.0 / totalRules):F1}%)");
        
        // Async and Concurrency health
        var asyncConcurrencyRules = perfRules.Where(r => 
            r.RuleId.EndsWith("001") || r.RuleId.EndsWith("005") || r.RuleId.EndsWith("006"));
        var asyncConcurrencyHealth = asyncConcurrencyRules.Count(r => r.IsSuccess) * 100.0 / asyncConcurrencyRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Async & Concurrency Health: {asyncConcurrencyHealth:F1}%");
        
        // Data Access Performance health
        var dataPerformanceRules = perfRules.Where(r => 
            r.RuleId.EndsWith("002") || r.RuleId.EndsWith("003") || r.RuleId.EndsWith("007"));
        var dataPerformanceHealth = dataPerformanceRules.Count(r => r.IsSuccess) * 100.0 / dataPerformanceRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Data Access Performance Health: {dataPerformanceHealth:F1}%");
        
        // Resource Management health
        var resourceRules = perfRules.Where(r => 
            r.RuleId.EndsWith("004") || r.RuleId.EndsWith("008") || r.RuleId.EndsWith("010"));
        var resourceHealth = resourceRules.Count(r => r.IsSuccess) * 100.0 / resourceRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Resource Management Health: {resourceHealth:F1}%");
        
        // Scalability Readiness
        var scalabilityRules = perfRules.Where(r => 
            r.RuleId.EndsWith("011") || r.RuleId.EndsWith("012") || r.RuleId.EndsWith("014"));
        var scalabilityHealth = scalabilityRules.Count(r => r.IsSuccess) * 100.0 / scalabilityRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Scalability Readiness: {scalabilityHealth:F1}%");
        
        // Performance Risk Assessment
        var criticalPerfIssues = perfRules
            .Where(r => !r.IsSuccess && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("003") || 
                        r.RuleId.EndsWith("004") || r.RuleId.EndsWith("005")))
            .ToList();
        
        if (criticalPerfIssues.Any())
        {
            await TestContext.Out.WriteLineAsync("\nCRITICAL PERFORMANCE RISKS:");
            foreach (var issue in criticalPerfIssues)
            {
                await TestContext.Out.WriteLineAsync($"- {issue.RuleId}: {issue.Violations.Count} violations");
            }
        }
        
        await TestContext.Out.WriteLineAsync("===================================================\n");
    }
}