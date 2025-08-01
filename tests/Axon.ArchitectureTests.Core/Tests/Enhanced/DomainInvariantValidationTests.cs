using Axon.ArchitectureTests.Core.Rules.DDD;
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
/// Domain model invariant validation tests with aggregate consistency checks.
/// Ensures domain models maintain business invariants and follow DDD patterns.
/// </summary>
[TestFixture]
public sealed class DomainInvariantValidationTests : ArchitectureTestBase
{
    [Test]
    public async Task AggregateRoots_ShouldEnforceInvariants()
    {
        // Arrange
        var rule = new AggregateInvariantRule();
        ValidateRuleConfiguration(rule);
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI001");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "invariant-violation", "consistency-breach", "invalid-state" }, 
            "Critical Invariant Violations");
    }

    [Test]
    public async Task ValueObjects_ShouldMaintainImmutability()
    {
        // Arrange
        var rule = new ValueObjectImmutabilityRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI002");
        
        // Assert
        AssertRuleSuccess(ruleResult, "Value Object Immutability");
        
        var mutabilityViolations = FilterViolations(ruleResult, 
            "mutable", "setter", "modification");
        
        mutabilityViolations.Count.ShouldBe(0, 
            "Value objects must be immutable");
    }

    [Test]
    public async Task DomainEntities_ShouldHaveProperIdentity()
    {
        // Arrange
        var rule = new EntityIdentityRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI003");
        
        // Assert
        var identityViolations = FilterViolations(ruleResult, 
            "missing-id", "weak-identity", "primitive-id");
        
        LogViolations(identityViolations, "Entity Identity Issues");
        
        // Entities should have strongly-typed IDs
        LogInformationalViolations(ruleResult, 
            new[] { "strongly-typed", "identity-pattern" }, 
            "Identity Pattern Usage");
    }

    [Test]
    public async Task DomainServices_ShouldEncapsulateComplexLogic()
    {
        // Arrange
        var rule = new DomainServiceRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI004");
        
        // Assert
        var logicLeakageViolations = FilterViolations(ruleResult, 
            "anemic-domain", "logic-in-service", "missing-domain-service");
        
        LogViolations(logicLeakageViolations, "Domain Logic Distribution");
        
        // Complex domain logic should be in domain services or entities
        LogInformationalViolations(ruleResult, 
            new[] { "domain-service", "business-logic", "encapsulation" }, 
            "Domain Logic Patterns");
    }

    [Test]
    public async Task Aggregates_ShouldMaintainConsistencyBoundaries()
    {
        // Arrange
        var rule = new AggregateConsistencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI005");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "boundary-violation", "cross-aggregate-transaction" }, 
            "Aggregate Consistency Violations");
        
        var consistencyViolations = FilterViolations(ruleResult, 
            "consistency", "transaction", "boundary");
        
        LogViolations(consistencyViolations, "Aggregate Consistency Issues");
    }

    [Test]
    public async Task DomainEvents_ShouldBeProperlyImplemented()
    {
        // Arrange
        var rule = new DomainEventRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI006");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "domain-event", "event-pattern", "decoupling" }, 
            "Domain Event Usage");
        
        var eventImplementationViolations = FilterViolations(ruleResult, 
            "missing-event", "poor-event-design", "event-pollution");
        
        LogViolations(eventImplementationViolations, "Domain Event Implementation");
    }

    [Test]
    public async Task FactoryMethods_ShouldValidateInvariants()
    {
        // Arrange
        var rule = new FactoryValidationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI007");
        
        // Assert
        var unvalidatedFactoryViolations = FilterViolations(ruleResult, 
            "unvalidated-factory", "weak-construction", "invalid-creation");
        
        LogViolations(unvalidatedFactoryViolations, "Factory Validation Issues");
        
        // Factory methods should validate business rules
        AssertNoSpecificViolations(ruleResult, 
            new[] { "critical-unvalidated", "invariant-bypass" }, 
            "Critical Factory Validation Gaps");
    }

    [Test]
    public async Task DomainSpecifications_ShouldBeUsedForComplexRules()
    {
        // Arrange
        var rule = new SpecificationPatternRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI008");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "specification", "complex-rule", "reusability" }, 
            "Specification Pattern Opportunities");
    }

    [Test]
    public async Task DomainModels_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var rule = new DomainInfrastructureIsolationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI009");
        
        // Assert
        AssertRuleSuccess(ruleResult, "Domain-Infrastructure Isolation");
        
        var infrastructureDependenceViolations = FilterViolations(ruleResult, 
            "infrastructure-dependency", "external-coupling", "layer-violation");
        
        infrastructureDependenceViolations.Count.ShouldBe(0, 
            "Domain models must not depend on infrastructure");
    }

    [Test]
    public async Task BusinessRules_ShouldBeExplicitlyModeled()
    {
        // Arrange
        var rule = new ExplicitBusinessRuleRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI010");
        
        // Assert
        var implicitRuleViolations = FilterViolations(ruleResult, 
            "implicit-rule", "hidden-logic", "unclear-intent");
        
        LogViolations(implicitRuleViolations, "Implicit Business Rules");
        
        // Business rules should be clearly expressed in domain model
        LogInformationalViolations(ruleResult, 
            new[] { "explicit-rule", "domain-language", "clarity" }, 
            "Business Rule Clarity");
    }

    [Test]
    public async Task DomainTypes_ShouldUseUbiquitousLanguage()
    {
        // Arrange
        var rule = new UbiquitousLanguageRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI011");
        
        // Assert
        var languageViolations = FilterViolations(ruleResult, 
            "technical-naming", "poor-naming", "unclear-intent");
        
        LogViolations(languageViolations, "Ubiquitous Language Issues");
        
        // Domain types should use business language
        LogInformationalViolations(ruleResult, 
            new[] { "business-language", "domain-naming", "clarity" }, 
            "Domain Language Usage");
    }

    [Test]
    public async Task AntiCorruptionLayer_ShouldProtectDomain()
    {
        // Arrange
        var rule = new AntiCorruptionLayerRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI012");
        
        // Assert
        var corruptionViolations = FilterViolations(ruleResult, 
            "external-model-leak", "corruption", "adaptation-missing");
        
        LogViolations(corruptionViolations, "Domain Corruption Issues");
        
        // External models should not leak into domain
        AssertNoSpecificViolations(ruleResult, 
            new[] { "critical-corruption", "model-leak" }, 
            "Critical Domain Corruption");
    }

    [Test]
    public async Task AllDomainInvariantRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new AggregateInvariantRule(),
            new ValueObjectImmutabilityRule(),
            new EntityIdentityRule(),
            new DomainServiceRule(),
            new AggregateConsistencyRule(),
            new DomainEventRule(),
            new FactoryValidationRule(),
            new SpecificationPatternRule(),
            new DomainInfrastructureIsolationRule(),
            new ExplicitBusinessRuleRule(),
            new UbiquitousLanguageRule(),
            new AntiCorruptionLayerRule()
        };
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "Domain Invariant Validation");
        
        // Assert
        ValidateFrameworkContext();
        AssertExecutionPerformance(result, TimeSpan.FromSeconds(45));
        
        // Critical domain rules must pass
        var criticalFailures = result.RuleResults
            .Where(r => r.RuleId.StartsWith("DI") && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("002") || 
                        r.RuleId.EndsWith("005") || r.RuleId.EndsWith("009") || 
                        r.RuleId.EndsWith("012")) && 
                       !r.IsSuccess)
            .ToList();

        criticalFailures.Count.ShouldBe(0, 
            $"Critical domain rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");

        // Generate domain health report
        await GenerateDomainHealthReport(result);
    }

    [Test]
    public async Task DomainComplexity_ShouldBeManageable()
    {
        // Arrange
        var rule = new DomainComplexityRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "DI013");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "complexity", "cognitive-load", "maintainability" }, 
            "Domain Complexity Metrics");
    }

    private async Task GenerateDomainHealthReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== DOMAIN MODEL HEALTH REPORT ===");
        
        var domainRules = result.RuleResults.Where(r => r.RuleId.StartsWith("DI")).ToList();
        var passedRules = domainRules.Count(r => r.IsSuccess);
        var totalRules = domainRules.Count;
        
        await TestContext.Out.WriteLineAsync($"Domain Health Score: {passedRules}/{totalRules} ({(passedRules * 100.0 / totalRules):F1}%)");
        
        // Aggregate analysis
        var aggregateRules = domainRules.Where(r => 
            r.RuleId.EndsWith("001") || r.RuleId.EndsWith("005") || r.RuleId.EndsWith("007"));
        var aggregateHealth = aggregateRules.Count(r => r.IsSuccess) * 100.0 / aggregateRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Aggregate Pattern Health: {aggregateHealth:F1}%");
        
        // Value Object analysis
        var valueObjectRules = domainRules.Where(r => 
            r.RuleId.EndsWith("002") || r.RuleId.EndsWith("003"));
        var valueObjectHealth = valueObjectRules.Count(r => r.IsSuccess) * 100.0 / valueObjectRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Value Object Pattern Health: {valueObjectHealth:F1}%");
        
        // Business Rule analysis
        var businessRuleRules = domainRules.Where(r => 
            r.RuleId.EndsWith("004") || r.RuleId.EndsWith("008") || r.RuleId.EndsWith("010"));
        var businessRuleHealth = businessRuleRules.Count(r => r.IsSuccess) * 100.0 / businessRuleRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Business Rule Modeling Health: {businessRuleHealth:F1}%");
        
        await TestContext.Out.WriteLineAsync("====================================\n");
    }
}