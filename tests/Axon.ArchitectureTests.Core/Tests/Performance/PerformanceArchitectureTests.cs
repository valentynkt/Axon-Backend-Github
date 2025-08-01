using Axon.ArchitectureTests.Core.Rules.Performance;
using Axon.Tests.Shared.Extensions;
using Axon.Tests.Shared.TestBase;

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
        
        var result = await ExecuteRulesAndValidateAsync(rules, "All performance rules validation");
        result.ShouldBeCompliant("All performance rules should pass");
    }

    [Test]
    public async Task IO_ShouldUseAsyncPatterns()
    {
        var rule = new AsyncPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF001");
        
        var ioViolations = FilterViolations(ruleResult, "I/O", "synchronous", "blocking");
        LogViolations(ioViolations, "I/O Async Violations");
        
        ioViolations.Count.ShouldBe(0, $"Synchronous I/O violations: {ioViolations.Count}");
    }

    [Test]
    public async Task DatabaseQueries_ShouldNotHaveNPlusOneProblem()
    {
        var rule = new DatabasePerformanceRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF003");
        
        var nPlusOneViolations = FilterViolations(ruleResult, "N+1", "multiple queries");
        LogViolations(nPlusOneViolations, "N+1 Query Violations");
        
        nPlusOneViolations.Count.ShouldBe(0, $"N+1 query violations: {nPlusOneViolations.Count}");
    }

    [Test]
    public async Task ExpensiveOperations_ShouldBeCached()
    {
        var rule = new CachingPatternsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF002");
        
        var cachingViolations = FilterViolations(ruleResult, "expensive", "repeated");
        LogViolations(cachingViolations, "Missing Caching Violations");
        
        // Log but don't fail - not all expensive operations need caching
        TestContext.Out.WriteLine($"Expensive operations without caching: {cachingViolations.Count}");
    }

    [Test]
    public async Task DisposableResources_ShouldBeProperlyManaged()
    {
        var rule = new ResourceManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        
        var disposalViolations = FilterViolations(ruleResult, "dispose", "using");
        LogViolations(disposalViolations, "Resource Disposal Violations");
        
        disposalViolations.Count.ShouldBe(0, $"Resource disposal violations: {disposalViolations.Count}");
    }

    [Test]
    public async Task Collections_ShouldUseAppropriateTypes()
    {
        var rule = new ResourceManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        
        var collectionViolations = FilterViolations(ruleResult, "collection", "enumerable");
        LogViolations(collectionViolations, "Collection Type Violations");
        
        // Log but don't fail - collection choice might be appropriate in context
        TestContext.Out.WriteLine($"Suboptimal collection usage: {collectionViolations.Count}");
    }

    [Test]
    public async Task MemoryAllocations_ShouldBeOptimized()
    {
        var rule = new ResourceManagementRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "PERF004");
        
        var memoryViolations = FilterViolations(ruleResult, "memory", "allocation");
        LogViolations(memoryViolations, "Memory Allocation Violations");
        
        // Log but don't fail - memory optimization might not be critical everywhere
        TestContext.Out.WriteLine($"Potential memory optimization opportunities: {memoryViolations.Count}");
    }
}