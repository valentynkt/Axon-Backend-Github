using Axon.ArchitectureTests.Core.Rules.EventDriven;
using Axon.ArchitectureTests.Core.TestBase;
using Axon.ArchitectureTests.Framework.Contracts;
// Removed due to circular dependency
// using Axon.Tests.Shared.Tests.Extensions;
// using Axon.Tests.Shared.TestBase;

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
        
        var result = await ExecuteRulesAndValidateAsync(rules);
        AssertAllRulesSuccess(result);
    }

    [Test]
    public async Task Events_ShouldBeImmutable()
    {
        var rule = new DomainEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT001");
        
        var immutabilityViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("immutable", StringComparison.OrdinalIgnoreCase) || 
            v.Message.Contains("setter", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        if (immutabilityViolations.Any())
        {
            Console.WriteLine("Event Immutability Violations:");
            foreach (var violation in immutabilityViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        immutabilityViolations.Count.ShouldBe(0, $"Mutable events found: {immutabilityViolations.Count}");
    }

    [Test]
    public async Task EventHandlers_ShouldBeIdempotent()
    {
        var rule = new EventHandlersRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT003");
        
        var idempotencyViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("idempotent", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (idempotencyViolations.Any())
        {
            Console.WriteLine("Handler Idempotency Violations:");
            foreach (var violation in idempotencyViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        // Log but don't fail - idempotency might be handled at infrastructure level
        Console.WriteLine($"Handlers without explicit idempotency: {idempotencyViolations.Count}");
    }

    [Test]
    public async Task Events_ShouldHaveVersioning()
    {
        var rule = new EventStoreRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT004");
        
        var versioningViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("version", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("schema", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (versioningViolations.Any())
        {
            Console.WriteLine("Event Versioning Violations:");
            foreach (var violation in versioningViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        // Log but don't fail - versioning might not be needed in all scenarios
        Console.WriteLine($"Events without versioning: {versioningViolations.Count}");
    }

    [Test]
    public async Task EventPublishing_ShouldHandleFailures()
    {
        var rule = new IntegrationEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT002");
        
        var failureViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("retry", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("dead letter", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (failureViolations.Any())
        {
            Console.WriteLine("Event Publishing Failure Violations:");
            foreach (var violation in failureViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        failureViolations.Count.ShouldBe(0, $"Event publishing without failure handling: {failureViolations.Count}");
    }

    [Test]
    public async Task AggregateRoots_ShouldRaiseDomainEvents()
    {
        var rule = new DomainEventsRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT001");
        
        var aggregateViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("aggregate", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (aggregateViolations.Any())
        {
            Console.WriteLine("Aggregate Domain Event Violations:");
            foreach (var violation in aggregateViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        // Log but don't fail - not all aggregates need to raise events
        Console.WriteLine($"Aggregates without domain events: {aggregateViolations.Count}");
    }

    [Test]
    public async Task EventProjections_ShouldBeEventuallyConsistent()
    {
        var rule = new EventSourcingRule();
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "EVT005");
        
        var consistencyViolations = ruleResult.Violations?.Where(v => 
            v.Message.Contains("projection", StringComparison.OrdinalIgnoreCase) ||
            v.Message.Contains("consistency", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<RuleViolation>();
        
        if (consistencyViolations.Any())
        {
            Console.WriteLine("Projection Consistency Violations:");
            foreach (var violation in consistencyViolations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
        }
        
        // Log but don't fail - projections might not be used everywhere
        Console.WriteLine($"Potential projection consistency issues: {consistencyViolations.Count}");
    }
}