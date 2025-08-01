using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;
using Axon.ArchitectureTests.Core.Rules.CleanArchitecture;

namespace Axon.ArchitectureTests.Core.Tests;

[TestFixture]
public sealed class IntegrationTests
{
    [Test]
    public void Framework_ShouldLoadAssembliesSuccessfully()
    {
        // Arrange
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();
        
        // Assert
        assemblies.ShouldNotBeEmpty();
        TestContext.Out.WriteLine($"Loaded {assemblies.Count()} assemblies");
        
        foreach (var assembly in assemblies.Take(5))
        {
            TestContext.Out.WriteLine($"  - {assembly.GetName().Name}");
        }
    }

    [Test]
    public void Framework_ShouldCreateContextSuccessfully()
    {
        // Arrange
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();
        var configuration = new ArchitectureSettings();
        
        // Act
        var context = new ArchitectureContext(assemblies, configuration);
        
        // Assert
        context.ShouldNotBeNull();
        context.Assemblies.ShouldNotBeEmpty();
        context.Types.ShouldNotBeEmpty();
        
        TestContext.Out.WriteLine($"Context contains {context.Types.Count()} types from {context.Assemblies.Count()} assemblies");
    }

    [Test]
    public async Task RuleEngine_ShouldExecuteRuleSuccessfully()
    {
        // Arrange
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();
        var configuration = new ArchitectureSettings();
        var context = new ArchitectureContext(assemblies, configuration);
        var ruleEngine = new RuleEngine();
        var rule = new ApiLayerDependencyRule();
        
        ruleEngine.RegisterRule(rule);
        
        // Act
        var result = await ruleEngine.ExecuteAsync(context);
        
        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.Count.ShouldBe(1);
        
        var ruleResult = result.RuleResults.Single();
        ruleResult.RuleId.ShouldBe("CA001");
        
        await TestContext.Out.WriteLineAsync($"Rule executed in {ruleResult.ExecutionTime.TotalMilliseconds}ms");
        await TestContext.Out.WriteLineAsync($"Result: {(ruleResult.IsSuccess ? "PASSED" : "FAILED")}");
        
        if (!ruleResult.IsSuccess)
        {
            await TestContext.Out.WriteLineAsync($"Violations: {ruleResult.Violations.Count}");
            foreach (var violation in ruleResult.Violations.Take(3))
            {
                await TestContext.Out.WriteLineAsync($"  - {violation.TypeName}: {violation.Message}");
            }
        }
        
        // The test should not fail even if violations are found - we just want to verify the framework works
        ruleResult.RuleId.ShouldBe("CA001");
    }

    [Test]
    public async Task RuleEngine_WithMultipleRules_ShouldExecuteAllRules()
    {
        // Arrange
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();
        var configuration = new ArchitectureSettings();
        var context = new ArchitectureContext(assemblies, configuration);
        var ruleEngine = new RuleEngine();
        
        var rules = new IArchitectureRule[]
        {
            new ApiLayerDependencyRule(),
            new ApplicationLayerDependencyRule()
        };
        
        ruleEngine.RegisterRules(rules);
        
        // Act
        var result = await ruleEngine.ExecuteAsync(context);
        
        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.Count.ShouldBe(2);
        
        await TestContext.Out.WriteLineAsync($"Engine executed {result.RuleResults.Count} rules in {result.TotalExecutionTime.TotalMilliseconds}ms");
        await TestContext.Out.WriteLineAsync($"Summary: {result.GetSummary()}");
        
        foreach (var ruleResult in result.RuleResults)
        {
            await TestContext.Out.WriteLineAsync($"  {ruleResult.RuleId}: {(ruleResult.IsSuccess ? "PASSED" : "FAILED")} ({ruleResult.ExecutionTime.TotalMilliseconds}ms)");
        }
        
        // Verify all expected rules were executed
        result.RuleResults.Select(r => r.RuleId).ShouldContain("CA001");
        result.RuleResults.Select(r => r.RuleId).ShouldContain("CA002");
    }
}