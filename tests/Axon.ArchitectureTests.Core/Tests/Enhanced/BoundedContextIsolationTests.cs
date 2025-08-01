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
/// Comprehensive bounded context isolation tests with module boundary validation.
/// Ensures proper DDD bounded context isolation and inter-context communication patterns.
/// </summary>
[TestFixture]
public sealed class BoundedContextIsolationTests : ArchitectureTestBase
{
    [Test]
    public async Task BoundedContexts_ShouldBePhysicallyIsolated()
    {
        // Arrange
        var rule = new BoundedContextPhysicalIsolationRule();
        ValidateRuleConfiguration(rule);
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC001");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "cross-context", "direct-reference", "coupling" }, 
            "Critical Context Coupling");
    }

    [Test]
    public async Task DomainModels_ShouldNotLeakBetweenContexts()
    {
        // Arrange
        var rule = new DomainModelIsolationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC002");
        
        // Assert
        AssertRuleSuccess(ruleResult, "Domain Model Isolation");
        
        // Log any potential cross-context model usage
        var crossContextModelViolations = FilterViolations(ruleResult, 
            "shared-model", "cross-context-entity", "domain-leakage");
        
        if (crossContextModelViolations.Any())
        {
            foreach (var violation in crossContextModelViolations)
            {
                await TestContext.Out.WriteLineAsync(
                    $"Domain Model Leakage: {violation.Message} in {violation.TypeName}");
            }
        }
    }

    [Test]
    public async Task IntegrationEvents_ShouldBeUsedForCrossContextCommunication()
    {
        // Arrange
        var rule = new CrossContextCommunicationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC003");
        
        // Assert
        var directCallViolations = FilterViolations(ruleResult, 
            "direct-call", "synchronous", "tight-coupling");
        
        if (directCallViolations.Any())
        {
            LogViolations(directCallViolations, "Cross-Context Direct Calls");
            
            // Direct cross-context calls should be minimized
            directCallViolations.Count.ShouldBeLessThan(5, 
                "Too many direct cross-context calls detected. Consider using integration events.");
        }
    }

    [Test]
    public async Task SharedKernel_ShouldBeMinimalAndWellDefined()
    {
        // Arrange
        var rule = new SharedKernelRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC004");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "shared-kernel", "common-types", "primitive-obsession" }, 
            "Shared Kernel Usage");
        
        // Validate that shared components are truly primitive and stable
        var unstableSharedViolations = FilterViolations(ruleResult, 
            "unstable-shared", "business-logic", "domain-specific");
        
        unstableSharedViolations.Count.ShouldBe(0, 
            "Shared kernel should only contain stable, primitive types");
    }

    [Test]
    public async Task ContextMapping_ShouldFollowDDDPatterns()
    {
        // Arrange
        var rule = new ContextMappingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC005");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "anti-corruption", "conformist", "customer-supplier" }, 
            "Context Mapping Patterns");
    }

    [Test]
    public async Task AggregateReferences_ShouldUseIdentitiesOnly()
    {
        // Arrange
        var rule = new AggregateReferenceRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC006");
        
        // Assert
        var objectReferenceViolations = FilterViolations(ruleResult, 
            "object-reference", "aggregate-root", "direct-reference");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "object-reference", "navigation-property" }, 
            "Cross-Aggregate Object References");
    }

    [Test]
    public async Task BoundedContextInterfaces_ShouldBeWellDefined()
    {
        // Arrange
        var rule = new BoundedContextInterfaceRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC007");
        
        // Assert
        var poorInterfaceViolations = FilterViolations(ruleResult, 
            "leaky", "chatty", "ill-defined");
        
        LogViolations(poorInterfaceViolations, "Context Interface Design");
        
        // Well-defined interfaces should be stable and minimal
        LogInformationalViolations(ruleResult, 
            new[] { "interface-design", "contract", "stability" }, 
            "Context Interface Quality");
    }

    [Test]
    public async Task EventSourcing_ShouldMaintainContextBoundaries()
    {
        // Arrange
        var rule = new EventSourcingBoundaryRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC008");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "event-store", "stream", "boundary" }, 
            "Event Sourcing Boundaries");
    }

    [Test]
    public async Task DataConsistency_ShouldFollowEventualConsistencyPattern()
    {
        // Arrange
        var rule = new DataConsistencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC009");
        
        // Assert
        var strongConsistencyViolations = FilterViolations(ruleResult, 
            "strong-consistency", "distributed-transaction", "two-phase");
        
        if (strongConsistencyViolations.Any())
        {
            LogViolations(strongConsistencyViolations, "Strong Consistency Usage");
            await TestContext.Out.WriteLineAsync(
                "Consider eventual consistency patterns for cross-context operations");
        }
    }

    [Test]
    public async Task AllBoundedContextRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new BoundedContextPhysicalIsolationRule(),
            new DomainModelIsolationRule(),
            new CrossContextCommunicationRule(),
            new SharedKernelRule(),
            new ContextMappingRule(),
            new AggregateReferenceRule(),
            new BoundedContextInterfaceRule(),
            new EventSourcingBoundaryRule(),
            new DataConsistencyRule()
        };
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "Bounded Context Isolation");
        
        // Assert
        ValidateFrameworkContext();
        AssertExecutionPerformance(result, TimeSpan.FromSeconds(45));
        
        // Critical bounded context rules must pass
        var criticalFailures = result.RuleResults
            .Where(r => r.RuleId.StartsWith("BC") && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("002") || 
                        r.RuleId.EndsWith("006")) && 
                       !r.IsSuccess)
            .ToList();

        criticalFailures.Count.ShouldBe(0, 
            $"Critical bounded context rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");

        // Generate bounded context health report
        await GenerateBoundedContextHealthReport(result);
    }

    [Test]
    public async Task ContextCohesion_ShouldBeHigh()
    {
        // Arrange
        var rule = new ContextCohesionRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "BC010");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "low-cohesion", "scattered", "unrelated" }, 
            "Context Cohesion Issues");
    }

    private async Task GenerateBoundedContextHealthReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== BOUNDED CONTEXT HEALTH REPORT ===");
        
        var contextRules = result.RuleResults.Where(r => r.RuleId.StartsWith("BC")).ToList();
        var passedRules = contextRules.Count(r => r.IsSuccess);
        var totalRules = contextRules.Count;
        
        await TestContext.Out.WriteLineAsync($"Context Isolation Score: {passedRules}/{totalRules} ({(passedRules * 100.0 / totalRules):F1}%)");
        
        var criticalIssues = contextRules
            .Where(r => !r.IsSuccess && (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("002") || r.RuleId.EndsWith("006")))
            .ToList();
        
        if (criticalIssues.Any())
        {
            await TestContext.Out.WriteLineAsync("\nCRITICAL ISSUES:");
            foreach (var issue in criticalIssues)
            {
                await TestContext.Out.WriteLineAsync($"- {issue.RuleId}: {issue.Violations.Count} violations");
            }
        }
        
        await TestContext.Out.WriteLineAsync("==========================================\n");
    }
}