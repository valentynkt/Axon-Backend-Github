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
                TestContext.WriteLine($"Aggregate Root Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
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
                TestContext.WriteLine($"Value Object Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
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
                TestContext.WriteLine($"Domain Service Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
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
                TestContext.WriteLine($"Repository Pattern Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
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
                TestContext.WriteLine($"Result Pattern Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
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
        TestContext.WriteLine($"Execution Summary: {result.GetSummary()}");
        
        foreach (var ruleResult in result.RuleResults.Where(r => !r.IsSuccess))
        {
            TestContext.WriteLine($"\nFailed Rule: {ruleResult.RuleId}");
            foreach (var violation in ruleResult.Violations)
            {
                TestContext.WriteLine($"  - {violation.Message} ({violation.TypeName})");
            }
        }

        result.IsSuccess.ShouldBeTrue($"DDD pattern violations found. Total violations: {result.TotalViolations}");
    }
}