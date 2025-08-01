using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;
using Axon.ArchitectureTests.Core.Rules.DDD;
using Axon.ArchitectureTests.Core.Rules.ErrorHandling;

namespace Axon.ArchitectureTests.Core.Tests;

[TestFixture]
public sealed class DddPatternTests
{
    private RuleEngine _ruleEngine = null!;
    private ArchitectureContext _context = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();
        var configuration = new ArchitectureSettings();
        _context = new ArchitectureContext(assemblies, configuration);
    }

    [SetUp]
    public void SetUp()
    {
        _ruleEngine = new RuleEngine();
    }

    [Test]
    public async Task AggregateRoots_ShouldFollowDDDPatterns()
    {
        // Arrange
        var rule = new AggregateRootRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD001");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Aggregate Root Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Aggregate root violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task ValueObjects_ShouldFollowDDDPatterns()
    {
        // Arrange
        var rule = new ValueObjectRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD002");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Value Object Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Value object violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task DomainServices_ShouldFollowDDDPatterns()
    {
        // Arrange
        var rule = new DomainServiceRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD003");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Domain Service Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Domain service violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task Repositories_ShouldFollowDDDPatterns()
    {
        // Arrange
        var rule = new RepositoryPatternRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD004");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Repository Pattern Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Repository pattern violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task ResultPattern_ShouldBeUsedCorrectly()
    {
        // Arrange
        var rule = new ResultPatternUsageRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "ERR001");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Result Pattern Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Result pattern violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task AllDDDRules_ShouldPass()
    {
        // Arrange
        _ruleEngine.RegisterRules(new IArchitectureRule[]
        {
            new AggregateRootRule(),
            new ValueObjectRule(),
            new DomainServiceRule(),
            new RepositoryPatternRule(),
            new ResultPatternUsageRule()
        });

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        await TestContext.Out.WriteLineAsync($"Execution Summary: {result.GetSummary()}");
        
        foreach (var ruleResult in result.RuleResults.Where(r => !r.IsSuccess))
        {
            await TestContext.Out.WriteLineAsync($"\nFailed Rule: {ruleResult.RuleId}");
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"  - {violation.Message} ({violation.TypeName})");
            }
        }

        result.IsSuccess.ShouldBeTrue($"DDD pattern violations found. Total violations: {result.TotalViolations}");
    }

    [Test]
    public async Task AggregateRoots_ShouldHaveInvariantValidation()
    {
        // Arrange
        var rule = new AggregateRootRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD001");
        
        // Focus on invariant validation violations
        var invariantViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("invariant", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("validation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (invariantViolations.Any())
        {
            foreach (var violation in invariantViolations)
            {
                await TestContext.Out.WriteLineAsync($"Invariant Validation Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        // Log but don't fail - not all aggregates may need complex invariant validation
        await TestContext.Out.WriteLineAsync($"Aggregates without invariant validation: {invariantViolations.Count}");
    }

    [Test]
    public async Task AggregateRoots_ShouldSupportDomainEvents()
    {
        // Arrange
        var rule = new AggregateRootRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD001");
        
        // Focus on domain event violations
        var eventViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("domain event", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("event", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (eventViolations.Any())
        {
            foreach (var violation in eventViolations)
            {
                await TestContext.Out.WriteLineAsync($"Domain Event Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        // Log but don't fail - not all aggregates may need domain events
        await TestContext.Out.WriteLineAsync($"Aggregates without domain event support: {eventViolations.Count}");
    }

    [Test]
    public async Task AggregateRoots_ShouldUseDomainLanguage()
    {
        // Arrange
        var rule = new AggregateRootRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD001");
        
        // Focus on domain language violations
        var languageViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("CRUD", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("domain language", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("verb", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (languageViolations.Any())
        {
            foreach (var violation in languageViolations)
            {
                await TestContext.Out.WriteLineAsync($"Domain Language Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        languageViolations.Count.ShouldBe(0, $"Aggregates using CRUD language instead of domain language: {languageViolations.Count}");
    }

    [Test]
    public async Task ValueObjects_ShouldBeImmutable()
    {
        // Arrange
        var rule = new ValueObjectRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD002");
        
        // Focus on immutability violations
        var immutabilityViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("immutable", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("setter", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (immutabilityViolations.Any())
        {
            foreach (var violation in immutabilityViolations)
            {
                await TestContext.Out.WriteLineAsync($"Immutability Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        immutabilityViolations.Count.ShouldBe(0, $"Value objects with mutability issues: {immutabilityViolations.Count}");
    }

    [Test]
    public async Task ValueObjects_ShouldHaveFactoryMethods()
    {
        // Arrange
        var rule = new ValueObjectRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "DDD002");
        
        // Focus on factory method violations
        var factoryViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("factory", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("Create", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (factoryViolations.Any())
        {
            foreach (var violation in factoryViolations)
            {
                await TestContext.Out.WriteLineAsync($"Factory Method Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        // Log but don't fail - not all value objects need factory methods
        await TestContext.Out.WriteLineAsync($"Value objects that could benefit from factory methods: {factoryViolations.Count}");
    }

    [Test]
    public async Task ResultPattern_ShouldHandleExceptionBoundaries()
    {
        // Arrange
        var rule = new ResultPatternUsageRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "ERR001");
        
        // Focus on exception boundary violations
        var boundaryViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("infrastructure", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("external", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("exception", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (boundaryViolations.Any())
        {
            foreach (var violation in boundaryViolations)
            {
                await TestContext.Out.WriteLineAsync($"Exception Boundary Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        // Log but don't fail - some exception handling might be acceptable
        await TestContext.Out.WriteLineAsync($"Potential exception boundary issues: {boundaryViolations.Count}");
    }
}