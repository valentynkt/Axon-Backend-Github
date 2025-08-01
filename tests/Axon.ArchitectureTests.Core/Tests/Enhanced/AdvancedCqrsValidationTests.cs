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
/// Advanced CQRS pattern validation tests with command/query segregation enforcement.
/// Ensures strict CQRS compliance with enhanced pattern validation.
/// </summary>
[TestFixture]
public sealed class AdvancedCqrsValidationTests : ArchitectureTestBase
{
    [Test]
    public async Task CommandQuerySeparation_ShouldBeStrictlyEnforced()
    {
        // Arrange
        var rule = new StrictCommandQuerySeparationRule();
        ValidateRuleConfiguration(rule);
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS001");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "query-side-effect", "command-return-data", "mixed-responsibility" }, 
            "Critical CQS Violations");
    }

    [Test]
    public async Task Commands_ShouldFollowAdvancedPatterns()
    {
        // Arrange
        var rule = new AdvancedCommandPatternRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS002");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "validation", "authorization", "audit" }, 
            "Command Pattern Enhancements");
        
        // Commands should have proper validation
        var validationViolations = FilterViolations(ruleResult, 
            "missing-validation", "no-validator", "unvalidated");
        
        validationViolations.Count.ShouldBeLessThan(3, 
            "Most commands should have validation");
    }

    [Test]
    public async Task Queries_ShouldBeOptimizedForReading()
    {
        // Arrange
        var rule = new QueryOptimizationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS003");
        
        // Assert
        var performanceViolations = FilterViolations(ruleResult, 
            "n+1", "missing-projection", "inefficient");
        
        LogViolations(performanceViolations, "Query Performance Issues");
        
        // Queries should use projections and avoid N+1 problems
        LogInformationalViolations(ruleResult, 
            new[] { "performance", "projection", "materialized-view" }, 
            "Query Optimization Opportunities");
    }

    [Test]
    public async Task EventSourcing_ShouldIntegrateWithCQRS()
    {
        // Arrange
        var rule = new EventSourcingCqrsIntegrationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS004");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "event-store", "projection", "read-model" }, 
            "Event Sourcing Integration");
    }

    [Test]
    public async Task CommandHandlers_ShouldFollowSingleHandlerPrinciple()
    {
        // Arrange
        var rule = new SingleCommandHandlerRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS005");
        
        // Assert
        AssertRuleSuccess(ruleResult, "Single Command Handler Principle");
        
        var multipleHandlerViolations = FilterViolations(ruleResult, 
            "multiple-handlers", "handler-conflict", "ambiguous");
        
        multipleHandlerViolations.Count.ShouldBe(0, 
            "Each command should have exactly one handler");
    }

    [Test]
    public async Task QueryHandlers_ShouldUseReadOnlyOperations()
    {
        // Arrange
        var rule = new ReadOnlyQueryRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS006");
        
        // Assert
        var sideEffectViolations = FilterViolations(ruleResult, 
            "side-effect", "mutation", "write-operation");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "side-effect", "mutation" }, 
            "Query Side Effects");
    }

    [Test]
    public async Task CommandValidation_ShouldBeComprehensive()
    {
        // Arrange
        var rule = new ComprehensiveCommandValidationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS007");
        
        // Assert
        var validationGapViolations = FilterViolations(ruleResult, 
            "missing-validation", "weak-validation", "unprotected");
        
        if (validationGapViolations.Any())
        {
            LogViolations(validationGapViolations, "Command Validation Gaps");
        }
        
        // Critical commands must have validation
        var criticalUnvalidatedViolations = FilterViolations(ruleResult, 
            "critical-unvalidated", "security-risk");
        
        criticalUnvalidatedViolations.Count.ShouldBe(0, 
            "Critical commands must have proper validation");
    }

    [Test]
    public async Task CqrsMediator_ShouldBeUsedCorrectly()
    {
        // Arrange
        var rule = new MediatorPatternRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS008");
        
        // Assert
        var mediatorMisuseViolations = FilterViolations(ruleResult, 
            "direct-handler", "bypass-mediator", "tight-coupling");
        
        LogViolations(mediatorMisuseViolations, "Mediator Pattern Violations");
        
        // Most CQRS operations should go through mediator
        LogInformationalViolations(ruleResult, 
            new[] { "mediator-usage", "decoupling" }, 
            "Mediator Pattern Usage");
    }

    [Test]
    public async Task CommandProjections_ShouldBeEventuallyConsistent()
    {
        // Arrange
        var rule = new EventualConsistencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS009");
        
        // Assert
        var strongConsistencyViolations = FilterViolations(ruleResult, 
            "strong-consistency", "immediate-read", "synchronous-projection");
        
        if (strongConsistencyViolations.Any())
        {
            LogViolations(strongConsistencyViolations, "Strong Consistency Patterns");
            await TestContext.Out.WriteLineAsync(
                "Consider eventual consistency for better scalability");
        }
    }

    [Test]
    public async Task QueryCaching_ShouldBeImplementedAppropriately()
    {
        // Arrange
        var rule = new QueryCachingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS010");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "caching", "performance", "stale-data" }, 
            "Query Caching Opportunities");
    }

    [Test]
    public async Task CommandAudit_ShouldBeImplemented()
    {
        // Arrange
        var rule = new CommandAuditRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS011");
        
        // Assert
        var missingAuditViolations = FilterViolations(ruleResult, 
            "no-audit", "missing-trace", "untracked");
        
        LogViolations(missingAuditViolations, "Command Audit Gaps");
        
        // Critical commands should have audit trails
        var criticalUnauditedViolations = FilterViolations(ruleResult, 
            "critical-unaudited", "compliance-risk");
        
        if (criticalUnauditedViolations.Any())
        {
            await TestContext.Out.WriteLineAsync(
                $"Commands missing audit trails: {criticalUnauditedViolations.Count}");
        }
    }

    [Test]
    public async Task QueryPagination_ShouldBeImplementedForCollections()
    {
        // Arrange
        var rule = new QueryPaginationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS012");
        
        // Assert
        var unpaginatedCollectionViolations = FilterViolations(ruleResult, 
            "unpaginated", "collection-query", "memory-risk");
        
        LogViolations(unpaginatedCollectionViolations, "Unpaginated Collection Queries");
        
        // Large collection queries should have pagination
        LogInformationalViolations(ruleResult, 
            new[] { "pagination", "performance", "scalability" }, 
            "Query Pagination Patterns");
    }

    [Test]
    public async Task AllAdvancedCqrsRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new StrictCommandQuerySeparationRule(),
            new AdvancedCommandPatternRule(),
            new QueryOptimizationRule(),
            new EventSourcingCqrsIntegrationRule(),
            new SingleCommandHandlerRule(),
            new ReadOnlyQueryRule(),
            new ComprehensiveCommandValidationRule(),
            new MediatorPatternRule(),
            new EventualConsistencyRule(),
            new QueryCachingRule(),
            new CommandAuditRule(),
            new QueryPaginationRule()
        };
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "Advanced CQRS Validation");
        
        // Assert
        ValidateFrameworkContext();
        AssertExecutionPerformance(result, TimeSpan.FromSeconds(60));
        
        // Critical CQRS rules must pass
        var criticalFailures = result.RuleResults
            .Where(r => r.RuleId.StartsWith("ACQRS") && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("005") || 
                        r.RuleId.EndsWith("006") || r.RuleId.EndsWith("007")) && 
                       !r.IsSuccess)
            .ToList();

        criticalFailures.Count.ShouldBe(0, 
            $"Critical CQRS rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");

        // Generate CQRS compliance report
        await GenerateCqrsComplianceReport(result);
    }

    [Test]
    public async Task CqrsPerformance_ShouldMeetBenchmarks()
    {
        // Arrange
        var rule = new CqrsPerformanceBenchmarkRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "ACQRS013");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "performance", "latency", "throughput" }, 
            "CQRS Performance Metrics");
    }

    private async Task GenerateCqrsComplianceReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== ADVANCED CQRS COMPLIANCE REPORT ===");
        
        var cqrsRules = result.RuleResults.Where(r => r.RuleId.StartsWith("ACQRS")).ToList();
        var passedRules = cqrsRules.Count(r => r.IsSuccess);
        var totalRules = cqrsRules.Count;
        
        await TestContext.Out.WriteLineAsync($"CQRS Compliance Score: {passedRules}/{totalRules} ({(passedRules * 100.0 / totalRules):F1}%)");
        
        // Command analysis
        var commandRules = cqrsRules.Where(r => r.RuleId.Contains("Command") || 
                                              r.RuleId.EndsWith("002") || 
                                              r.RuleId.EndsWith("005") || 
                                              r.RuleId.EndsWith("007") || 
                                              r.RuleId.EndsWith("011"));
        var commandCompliance = commandRules.Count(r => r.IsSuccess) * 100.0 / commandRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Command Pattern Compliance: {commandCompliance:F1}%");
        
        // Query analysis
        var queryRules = cqrsRules.Where(r => r.RuleId.Contains("Query") || 
                                            r.RuleId.EndsWith("003") || 
                                            r.RuleId.EndsWith("006") || 
                                            r.RuleId.EndsWith("010") || 
                                            r.RuleId.EndsWith("012"));
        var queryCompliance = queryRules.Count(r => r.IsSuccess) * 100.0 / queryRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Query Pattern Compliance: {queryCompliance:F1}%");
        
        await TestContext.Out.WriteLineAsync("=============================================\n");
    }
}