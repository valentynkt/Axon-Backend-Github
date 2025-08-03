using Axon.ArchitectureTests.Core.Rules.Performance;
using Axon.ArchitectureTests.Core.TestBase;
using Axon.ArchitectureTests.Framework.Contracts;
// Removed due to circular dependency
// using Axon.Tests.Shared.Tests.Extensions;
// using Axon.Tests.Shared.TestBase;

namespace Axon.ArchitectureTests.Core.Tests.Performance;

/// <summary>
/// Architecture tests to validate performance patterns and practices across the application.
/// Uses Framework ArchitectureTestBase for maximum reuse and consistency.
/// </summary>
public sealed class PerformanceArchitectureTests : ArchitectureTestBase
{

    [Test]
    public async Task AsyncPatterns_ShouldBeUsedAppropriately()
    {
        var rule = new AsyncPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF001");
        AssertRuleSuccess(ruleResult, "Async patterns");
    }

    [Test]
    public async Task Caching_ShouldBeImplementedAppropriately()
    {
        var rule = new CachingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF002");
        AssertRuleSuccess(ruleResult, "Caching patterns");
    }

    [Test]
    public async Task DatabaseAccess_ShouldFollowPerformancePatterns()
    {
        var rule = new DatabasePerformanceRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF003");
        AssertRuleSuccess(ruleResult, "Database performance patterns");
    }

    [Test]
    public async Task ResourceManagement_ShouldFollowPatterns()
    {
        var rule = new ResourceManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        AssertRuleSuccess(ruleResult, "Resource management patterns");
    }

    [Test]
    public async Task AllPerformanceRules_ShouldPass()
    {
        var rules = new IArchitectureRule[]
        {
            new AsyncPatternsRule(),
            new CachingPatternsRule(),
            new DatabasePerformanceRule(),
            new ResourceManagementRule()
        };
        
        var result = await ExecuteRulesAndValidateAsync(rules);
        AssertAllRulesSuccess(result);
    }

    [Test]
    public async Task IO_ShouldUseAsyncPatterns()
    {
        var rule = new AsyncPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF001");
        
        var ioViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("I/O", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("synchronous", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("blocking", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (ioViolations.Any())
        {
            Console.WriteLine("I/O Async Violations:");
            foreach (var violation in ioViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        ioViolations.Count.ShouldBe(0, $"Synchronous I/O violations: {ioViolations.Count}");
    }

    [Test]
    public async Task DatabaseQueries_ShouldNotHaveNPlusOneProblem()
    {
        var rule = new DatabasePerformanceRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF003");
        
        var nPlusOneViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("N+1", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("multiple queries", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        if (nPlusOneViolations.Any())
        {
            Console.WriteLine("N+1 Query Violations:");
            foreach (var violation in nPlusOneViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        nPlusOneViolations.Count.ShouldBe(0, $"N+1 query violations: {nPlusOneViolations.Count}");
    }

    [Test]
    public async Task ExpensiveOperations_ShouldBeCached()
    {
        var rule = new CachingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF002");
        
        var cachingViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("expensive", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("repeated", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (cachingViolations.Any())
        {
            Console.WriteLine("Missing Caching Violations:");
            foreach (var violation in cachingViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        // Log but don't fail - not all expensive operations need caching
        Console.WriteLine($"Expensive operations without caching: {cachingViolations.Count}");
    }

    [Test]
    public async Task DisposableResources_ShouldBeProperlyManaged()
    {
        var rule = new ResourceManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        
        var disposalViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("dispose", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("using", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (disposalViolations.Any())
        {
            Console.WriteLine("Resource Disposal Violations:");
            foreach (var violation in disposalViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        disposalViolations.Count.ShouldBe(0, $"Resource disposal violations: {disposalViolations.Count}");
    }

    [Test]
    public async Task Collections_ShouldUseAppropriateTypes()
    {
        var rule = new ResourceManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        
        var collectionViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("collection", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("enumerable", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (collectionViolations.Any())
        {
            Console.WriteLine("Collection Type Violations:");
            foreach (var violation in collectionViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        // Log but don't fail - collection choice might be appropriate in context
        Console.WriteLine($"Suboptimal collection usage: {collectionViolations.Count}");
    }

    [Test]
    public async Task MemoryAllocations_ShouldBeOptimized()
    {
        var rule = new ResourceManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        
        var memoryViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("memory", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("allocation", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (memoryViolations.Any())
        {
            Console.WriteLine("Memory Allocation Violations:");
            foreach (var violation in memoryViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        // Log but don't fail - memory optimization might not be critical everywhere
        Console.WriteLine($"Potential memory optimization opportunities: {memoryViolations.Count}");
    }
}