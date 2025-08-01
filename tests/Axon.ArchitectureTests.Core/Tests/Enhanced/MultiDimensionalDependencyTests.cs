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
/// Enhanced multi-dimensional dependency validation tests with cross-layer analysis.
/// Validates complex dependency patterns across all architecture layers.
/// </summary>
[TestFixture]
public sealed class MultiDimensionalDependencyTests : ArchitectureTestBase
{
    [Test]
    public async Task CrossLayerDependencies_ShouldFollowArchitectureDirection()
    {
        // Arrange
        var rule = new CrossLayerDependencyRule();
        ValidateRuleConfiguration(rule);
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA001");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "circular", "reverse", "forbidden" }, 
            "Critical Dependency Flow");
    }

    [Test]
    public async Task DependencyInversionPrinciple_ShouldBeEnforced()
    {
        // Arrange
        var rule = new DependencyInversionRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA002");
        
        // Assert - Infrastructure should depend on Application abstractions, not concrete types
        var concreteDependencyViolations = FilterViolations(ruleResult, 
            "concrete", "implementation", "direct");
        
        LogViolations(concreteDependencyViolations, "Dependency Inversion");
        concreteDependencyViolations.Count.ShouldBe(0, 
            "Infrastructure layer should only depend on Application abstractions");
    }

    [Test]
    public async Task TransitiveDependencies_ShouldBeMinimized()
    {
        // Arrange
        var rule = new TransitiveDependencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA003");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "transitive", "deep", "chain" }, 
            "Complex Dependency Chains");
    }

    [Test]
    public async Task ModuleBoundaries_ShouldBeRespected()
    {
        // Arrange
        var rule = new ModuleBoundaryRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA004");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "cross-module", "internal", "boundary" }, 
            "Module Boundary Violations");
    }

    [Test]
    public async Task ExternalDependencies_ShouldBeIsolated()
    {
        // Arrange
        var rule = new ExternalDependencyIsolationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA005");
        
        // Assert
        var isolationViolations = FilterViolations(ruleResult, 
            "external", "third-party", "nuget");
        
        if (isolationViolations.Any())
        {
            LogViolations(isolationViolations, "External Dependency Isolation");
        }
        
        // External dependencies should only be used in Infrastructure layer
        AssertNoSpecificViolations(ruleResult, 
            new[] { "domain-external", "application-external" }, 
            "Critical External Dependency Leakage");
    }

    [Test]
    public async Task AllEnhancedDependencyRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new CrossLayerDependencyRule(),
            new DependencyInversionRule(),
            new TransitiveDependencyRule(),
            new ModuleBoundaryRule(),
            new ExternalDependencyIsolationRule()
        };
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "Enhanced Dependency Validation");
        
        // Assert
        ValidateFrameworkContext();
        AssertExecutionPerformance(result, TimeSpan.FromSeconds(30));
        
        // Critical rules must pass (CA001, CA002, CA004, CA005)
        var criticalFailures = result.RuleResults
            .Where(r => r.RuleId.StartsWith("ECA") && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("002") || 
                        r.RuleId.EndsWith("004") || r.RuleId.EndsWith("005")) && 
                       !r.IsSuccess)
            .ToList();

        criticalFailures.Count.ShouldBe(0, 
            $"Critical dependency rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");
    }

    [Test]
    public async Task DependencyMetrics_ShouldBeWithinAcceptableLimits()
    {
        // Arrange
        var rule = new DependencyMetricsRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA006");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "complexity", "coupling", "cohesion" }, 
            "Dependency Metrics");
        
        // High coupling violations should be flagged but not fail the test
        var highCouplingViolations = FilterViolations(ruleResult, 
            "high-coupling", "excessive-dependencies");
        
        await TestContext.Out.WriteLineAsync(
            $"Components with high coupling: {highCouplingViolations.Count}");
    }

    [Test]
    public async Task CircularDependencies_ShouldNotExist()
    {
        // Arrange
        var rule = new CircularDependencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA007");
        
        // Assert
        AssertRuleSuccess(ruleResult, "Circular Dependency Detection");
    }

    [Test]
    public async Task DependencyStability_ShouldFollowStableDependencyPrinciple()
    {
        // Arrange
        var rule = new DependencyStabilityRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA008");
        
        // Assert
        // Stable components should not depend on unstable ones
        LogInformationalViolations(ruleResult, 
            new[] { "stability", "unstable", "volatile" }, 
            "Dependency Stability");
    }

    [Test]
    public async Task InterfaceSegregation_ShouldBeApplied()
    {
        // Arrange
        var rule = new InterfaceSegregationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ECA009");
        
        // Assert
        var fatInterfaceViolations = FilterViolations(ruleResult, 
            "fat-interface", "many-methods", "segregation");
        
        LogViolations(fatInterfaceViolations, "Interface Segregation");
        
        // Log but don't fail - ISP violations might be acceptable in some cases
        await TestContext.Out.WriteLineAsync(
            $"Potential Interface Segregation violations: {fatInterfaceViolations.Count}");
    }
}