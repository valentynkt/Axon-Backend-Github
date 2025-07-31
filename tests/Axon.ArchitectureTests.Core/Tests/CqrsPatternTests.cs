using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;
using Axon.ArchitectureTests.Core.Rules.CQRS;

namespace Axon.ArchitectureTests.Core.Tests;

[TestFixture]
public sealed class CqrsPatternTests
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
    public async Task Commands_ShouldFollowCQRSPatterns()
    {
        // Arrange
        var rule = new CommandImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS001");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                TestContext.WriteLine($"Command Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Command implementation violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task Queries_ShouldFollowCQRSPatterns()
    {
        // Arrange
        var rule = new QueryImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS002");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                TestContext.WriteLine($"Query Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Query implementation violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task Handlers_ShouldFollowCQRSPatterns()
    {
        // Arrange
        var rule = new HandlerImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS003");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                TestContext.WriteLine($"Handler Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"Handler implementation violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task FolderStructure_ShouldFollowCQRSConventions()
    {
        // Arrange
        var rule = new FolderStructureRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS004");
        
        if (!ruleResult.IsSuccess)
        {
            foreach (var violation in ruleResult.Violations)
            {
                TestContext.WriteLine($"Folder Structure Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine($"Suggested Fix: {violation.SuggestedFix}");
                TestContext.WriteLine("---");
            }
        }

        ruleResult.IsSuccess.ShouldBeTrue($"CQRS folder structure violations found: {ruleResult.Violations.Count}");
    }

    [Test]
    public async Task AllCQRSRules_ShouldPass()
    {
        // Arrange
        _ruleEngine.RegisterRules(new IArchitectureRule[]
        {
            new CommandImplementationRule(),
            new QueryImplementationRule(),
            new HandlerImplementationRule(),
            new FolderStructureRule()
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

        result.IsSuccess.ShouldBeTrue($"CQRS pattern violations found. Total violations: {result.TotalViolations}");
    }
}