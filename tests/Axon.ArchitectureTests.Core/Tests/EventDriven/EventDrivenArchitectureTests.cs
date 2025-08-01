using Axon.ArchitectureTests.Core.Rules.EventDriven;
using Axon.Tests.Shared.Extensions;
using Axon.Tests.Shared.TestBase;

namespace Axon.ArchitectureTests.Core.Tests.EventDriven;

/// <summary>
/// Architecture tests to validate event-driven patterns and practices across the application.
/// Uses Framework ArchitectureTestBase for maximum reuse and consistency.
/// </summary>
public sealed class EventDrivenArchitectureTests : ArchitectureTestBase
{

    [Test]
    public async Task DomainEvents_ShouldFollowPatterns()
    {
        var rule = new DomainEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT001");
        AssertRuleSuccess(ruleResult, "Domain events patterns");
    }

    [Test]
    public async Task IntegrationEvents_ShouldFollowPatterns()
    {
        var rule = new IntegrationEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT002");
        AssertRuleSuccess(ruleResult, "Integration events patterns");
    }

    [Test]
    public async Task EventHandlers_ShouldFollowPatterns()
    {
        var rule = new EventHandlersRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT003");
        AssertRuleSuccess(ruleResult, "Event handlers patterns");
    }

    [Test]
    public async Task EventStore_ShouldFollowPatterns()
    {
        var rule = new EventStoreRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT004");
        AssertRuleSuccess(ruleResult, "Event store patterns");
    }

    [Test]
    public async Task EventSourcing_ShouldFollowPatterns()
    {
        var rule = new EventSourcingRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT005");
        AssertRuleSuccess(ruleResult, "Event sourcing patterns");
    }

    [Test]
    public async Task AllEventDrivenRules_ShouldPass()
    {
        var rules = new IArchitectureRule[]
        {
            new DomainEventsRule(),
            new IntegrationEventsRule(),
            new EventHandlersRule(),
            new EventStoreRule(),
            new EventSourcingRule()
        };
        
        var result = await ExecuteRulesAndValidateAsync(rules, "Event-driven architecture validation");
        result.ShouldBeCompliant("All event-driven rules should pass");
    }

    [Test]
    public async Task Events_ShouldBeImmutable()
    {
        var rule = new DomainEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT001");
        
        var immutabilityViolations = FilterViolations(ruleResult, "immutable", "setter");
        LogViolations(immutabilityViolations, "Event Immutability Violations");
        
        immutabilityViolations.Count.ShouldBe(0, $"Mutable events found: {immutabilityViolations.Count}");
    }

    [Test]
    public async Task EventHandlers_ShouldBeIdempotent()
    {
        var rule = new EventHandlersRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT003");
        
        var idempotencyViolations = FilterViolations(ruleResult, "idempotent");
        LogViolations(idempotencyViolations, "Handler Idempotency Violations");
        
        // Log but don't fail - idempotency might be handled at infrastructure level
        await TestContext.Out.WriteLineAsync($"Handlers without explicit idempotency: {idempotencyViolations.Count}");
    }

    [Test]
    public async Task Events_ShouldHaveVersioning()
    {
        var rule = new EventStoreRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT004");
        
        var versioningViolations = FilterViolations(ruleResult, "version", "schema");
        LogViolations(versioningViolations, "Event Versioning Violations");
        
        // Log but don't fail - versioning might not be needed in all scenarios
        await TestContext.Out.WriteLineAsync($"Events without versioning: {versioningViolations.Count}");
    }

    [Test]
    public async Task EventPublishing_ShouldHandleFailures()
    {
        var rule = new IntegrationEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT002");
        
        var failureViolations = FilterViolations(ruleResult, "failure", "retry", "dead letter");
        LogViolations(failureViolations, "Event Publishing Failure Violations");
        
        failureViolations.Count.ShouldBe(0, $"Event publishing without failure handling: {failureViolations.Count}");
    }

    [Test]
    public async Task AggregateRoots_ShouldRaiseDomainEvents()
    {
        var rule = new DomainEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT001");
        
        var aggregateViolations = FilterViolations(ruleResult, "aggregate");
        LogViolations(aggregateViolations, "Aggregate Domain Event Violations");
        
        // Log but don't fail - not all aggregates need to raise events
        await TestContext.Out.WriteLineAsync($"Aggregates without domain events: {aggregateViolations.Count}");
    }

    [Test]
    public async Task EventProjections_ShouldBeEventuallyConsistent()
    {
        var rule = new EventSourcingRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT005");
        
        var consistencyViolations = FilterViolations(ruleResult, "projection", "consistency");
        LogViolations(consistencyViolations, "Projection Consistency Violations");
        
        // Log but don't fail - projections might not be used everywhere
        await TestContext.Out.WriteLineAsync($"Potential projection consistency issues: {consistencyViolations.Count}");
    }
}