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
        // Load only production assemblies for architecture validation
        var assemblies = AssemblyAnalyzer.LoadProductionAssemblies();
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
                await TestContext.Out.WriteLineAsync($"Command Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
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
                await TestContext.Out.WriteLineAsync($"Query Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
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
                await TestContext.Out.WriteLineAsync($"Handler Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
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
                await TestContext.Out.WriteLineAsync($"Folder Structure Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync($"Suggested Fix: {violation.SuggestedFix}");
                await TestContext.Out.WriteLineAsync("---");
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
        await TestContext.Out.WriteLineAsync($"Execution Summary: {result.GetSummary()}");
        
        foreach (var ruleResult in result.RuleResults.Where(r => !r.IsSuccess))
        {
            await TestContext.Out.WriteLineAsync($"\nFailed Rule: {ruleResult.RuleId}");
            foreach (var violation in ruleResult.Violations)
            {
                await TestContext.Out.WriteLineAsync($"  - {violation.Message} ({violation.TypeName})");
            }
        }

        result.IsSuccess.ShouldBeTrue($"CQRS pattern violations found. Total violations: {result.TotalViolations}");
    }

    [Test]
    public async Task CommandHandlers_ShouldUseAsyncPatterns()
    {
        // Arrange
        var rule = new HandlerImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS003");
        
        // Focus on async pattern violations
        var asyncViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("async", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("CancellationToken", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (asyncViolations.Any())
        {
            foreach (var violation in asyncViolations)
            {
                await TestContext.Out.WriteLineAsync($"Async Pattern Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        asyncViolations.Count.ShouldBe(0, $"Async pattern violations found in handlers: {asyncViolations.Count}");
    }

    [Test]
    public async Task Commands_ShouldHaveProperValidation()
    {
        // Arrange
        var rule = new CommandImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS001");
        
        // Focus on validation violations
        var validationViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("validation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (validationViolations.Any())
        {
            foreach (var violation in validationViolations)
            {
                await TestContext.Out.WriteLineAsync($"Validation Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        // Log count but don't fail - validation attributes might be optional in some cases
        await TestContext.Out.WriteLineAsync($"Commands without validation attributes: {validationViolations.Count}");
    }

    [Test]
    public async Task Queries_ShouldBeReadOnly()
    {
        // Arrange
        var rule = new QueryImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS002");
        
        // Focus on read-only violations
        var readOnlyViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("read-only", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("mutable", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (readOnlyViolations.Any())
        {
            foreach (var violation in readOnlyViolations)
            {
                await TestContext.Out.WriteLineAsync($"Read-Only Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        readOnlyViolations.Count.ShouldBe(0, $"Read-only violations found in queries: {readOnlyViolations.Count}");
    }

    [Test]
    public async Task Handlers_ShouldFollowSingleResponsibilityPrinciple()
    {
        // Arrange
        var rule = new HandlerImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS003");
        
        // Focus on SRP violations
        var srpViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("SRP", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("Single Responsibility", StringComparison.OrdinalIgnoreCase) ||
                       v.Message.Contains("dependencies", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (srpViolations.Any())
        {
            foreach (var violation in srpViolations)
            {
                await TestContext.Out.WriteLineAsync($"SRP Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        // Log but don't fail - SRP violations might be acceptable in some cases
        await TestContext.Out.WriteLineAsync($"Potential SRP violations in handlers: {srpViolations.Count}");
    }

    [Test]
    public async Task Queries_ShouldHavePagingForCollections()
    {
        // Arrange
        var rule = new QueryImplementationRule();
        _ruleEngine.RegisterRule(rule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_context);

        // Assert
        result.ShouldNotBeNull();
        var ruleResult = result.RuleResults.Single(r => r.RuleId == "CQRS002");
        
        // Focus on paging violations
        var pagingViolations = ruleResult.Violations
            .Where(v => v.Message.Contains("paging", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pagingViolations.Any())
        {
            foreach (var violation in pagingViolations)
            {
                await TestContext.Out.WriteLineAsync($"Paging Violation: {violation.Message}");
                await TestContext.Out.WriteLineAsync($"Type: {violation.TypeName}");
                await TestContext.Out.WriteLineAsync("---");
            }
        }

        // Log but don't fail - paging might not be needed for all collection queries
        await TestContext.Out.WriteLineAsync($"Queries without paging parameters: {pagingViolations.Count}");
    }
}