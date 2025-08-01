using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;
using Axon.ArchitectureTests.Core.Rules.CleanArchitecture;

namespace Axon.ArchitectureTests.Core.Tests;

[TestFixture]
public sealed class CleanArchitectureTests
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
    public async Task ApiLayer_ShouldOnlyDependOnApplicationAndShared()
    {
        // Arrange
        var rule = new ApiLayerDependencyRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CA001");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"API layer dependency violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task ApplicationLayer_ShouldOnlyDependOnDomainAndShared()
    {
        // Arrange
        var rule = new ApplicationLayerDependencyRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CA002");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Application layer dependency violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task InfrastructureLayer_ShouldOnlyDependOnApplicationAndShared()
    {
        // Arrange
        var rule = new InfrastructureLayerDependencyRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CA003");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Infrastructure layer dependency violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task DomainLayer_MustBeIsolated()
    {
        // Arrange
        var rule = new DomainLayerIsolationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CA004");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"CRITICAL Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Domain layer isolation violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task AllCleanArchitectureRules_ShouldPass()
    {
        // Arrange
        _ruleEngine.RegisterRules(new IArchitectureRule[]
        {
            new ApiLayerDependencyRule(),
            new ApplicationLayerDependencyRule(),
            new InfrastructureLayerDependencyRule(),
            new DomainLayerIsolationRule()
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

        result.IsSuccess.ShouldBeTrue($"Clean Architecture violations found. Total violations: {result.TotalViolations}");
    }
}