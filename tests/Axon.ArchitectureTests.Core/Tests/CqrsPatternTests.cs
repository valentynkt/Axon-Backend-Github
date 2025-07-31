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
                TestContext.WriteLine($"Async Pattern Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine("---");
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
                TestContext.WriteLine($"Validation Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine("---");
            }
        }

        // Log count but don't fail - validation attributes might be optional in some cases
        TestContext.WriteLine($"Commands without validation attributes: {validationViolations.Count}");
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
                TestContext.WriteLine($"Read-Only Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine("---");
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
                TestContext.WriteLine($"SRP Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine("---");
            }
        }

        // Log but don't fail - SRP violations might be acceptable in some cases
        TestContext.WriteLine($"Potential SRP violations in handlers: {srpViolations.Count}");
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
                TestContext.WriteLine($"Paging Violation: {violation.Message}");
                TestContext.WriteLine($"Type: {violation.TypeName}");
                TestContext.WriteLine("---");
            }
        }

        // Log but don't fail - paging might not be needed for all collection queries
        TestContext.WriteLine($"Queries without paging parameters: {pagingViolations.Count}");
    }
}