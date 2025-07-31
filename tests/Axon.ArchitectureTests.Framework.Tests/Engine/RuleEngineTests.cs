using NUnit.Framework;
using Shouldly;
using NSubstitute;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Contracts;
using System.Diagnostics;

namespace Axon.ArchitectureTests.Framework.Tests.Engine;

[TestFixture]
public sealed class RuleEngineTests
{
    private RuleEngine _ruleEngine = null!;
    private IArchitectureContext _mockContext = null!;
    private IArchitectureRule _mockRule = null!;

    [SetUp]
    public void SetUp()
    {
        _ruleEngine = new RuleEngine();
        _mockContext = Substitute.For<IArchitectureContext>();
        _mockRule = Substitute.For<IArchitectureRule>();
        
        // Setup default mock behavior
        _mockContext.Configuration.Returns(Substitute.For<IArchitectureConfiguration>());
        _mockContext.Configuration.Execution.Returns(Substitute.For<IExecutionConfiguration>());
        _mockContext.Configuration.Execution.MaxDegreeOfParallelism.Returns(Environment.ProcessorCount);
        _mockContext.Configuration.Execution.FailFastOnCritical.Returns(false);
    }

    [Test]
    public void RegisterRule_WithValidRule_ShouldSucceed()
    {
        // Arrange
        _mockRule.RuleId.Returns("TEST001");
        _mockRule.Name.Returns("Test Rule");

        // Act & Assert
        Should.NotThrow(() => _ruleEngine.RegisterRule(_mockRule));
    }

    [Test]
    public void RegisterRule_WithNullRule_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _ruleEngine.RegisterRule(null!));
    }

    [Test]
    public void RegisterRules_WithValidRules_ShouldSucceed()
    {
        // Arrange
        var rules = new List<IArchitectureRule> { _mockRule };
        _mockRule.RuleId.Returns("TEST001");

        // Act & Assert
        Should.NotThrow(() => _ruleEngine.RegisterRules(rules));
    }

    [Test]
    public void RegisterRules_WithNullRules_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _ruleEngine.RegisterRules(null!));
    }

    [Test]
    public async Task ExecuteAsync_WithNoRules_ShouldReturnEmptyResult()
    {
        // Act
        var result = await _ruleEngine.ExecuteAsync(_mockContext);

        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.ShouldBeEmpty();
        result.TotalExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public async Task ExecuteAsync_WithSingleRule_ShouldExecuteRule()
    {
        // Arrange
        var expectedResult = RuleResult.Success("TEST001", TimeSpan.FromMilliseconds(10));
        _mockRule.RuleId.Returns("TEST001");
        _mockRule.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResult));
        
        _ruleEngine.RegisterRule(_mockRule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_mockContext);

        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.Count.ShouldBe(1);
        result.RuleResults.First().RuleId.ShouldBe("TEST001");
        result.RuleResults.First().IsSuccess.ShouldBeTrue();
        result.TotalExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleRules_ShouldExecuteAllRules()
    {
        // Arrange
        var rule1 = Substitute.For<IArchitectureRule>();
        var rule2 = Substitute.For<IArchitectureRule>();
        
        rule1.RuleId.Returns("TEST001");
        rule2.RuleId.Returns("TEST002");
        
        rule1.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RuleResult.Success("TEST001", TimeSpan.FromMilliseconds(5))));
        rule2.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RuleResult.Success("TEST002", TimeSpan.FromMilliseconds(7))));

        _ruleEngine.RegisterRules(new[] { rule1, rule2 });

        // Act
        var result = await _ruleEngine.ExecuteAsync(_mockContext);

        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.Count.ShouldBe(2);
        result.RuleResults.Select(r => r.RuleId).ShouldContain("TEST001");
        result.RuleResults.Select(r => r.RuleId).ShouldContain("TEST002");
        result.TotalExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public async Task ExecuteAsync_WithRuleThrowingException_ShouldCaptureError()
    {
        // Arrange
        _mockRule.RuleId.Returns("TEST001");
        _mockRule.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns<Task<RuleResult>>(_ => throw new InvalidOperationException("Test exception"));
        
        _ruleEngine.RegisterRule(_mockRule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_mockContext);

        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.Count.ShouldBe(1);
        var ruleResult = result.RuleResults.First();
        ruleResult.RuleId.ShouldBe("TEST001");
        ruleResult.IsSuccess.ShouldBeFalse();
        ruleResult.Violations.ShouldContain(v => v.Message.Contains("Test exception"));
    }

    [Test]
    public async Task ExecuteAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _mockRule.RuleId.Returns("TEST001");
        _mockRule.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(async (callInfo) =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                await Task.Delay(1000, ct); // This should be cancelled
                return RuleResult.Success("TEST001", TimeSpan.FromMilliseconds(10));
            });
        
        _ruleEngine.RegisterRule(_mockRule);
        cts.CancelAfter(100); // Cancel after 100ms

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _ruleEngine.ExecuteAsync(_mockContext, cts.Token));
    }

    [Test]
    public async Task ExecuteByCategoryAsync_WithMatchingCategory_ShouldExecuteOnlyMatchingRules()
    {
        // Arrange
        var rule1 = Substitute.For<IArchitectureRule>();
        var rule2 = Substitute.For<IArchitectureRule>();
        
        rule1.RuleId.Returns("TEST001");
        rule1.Category.Returns("CleanArchitecture");
        rule2.RuleId.Returns("TEST002");
        rule2.Category.Returns("CQRS");
        
        rule1.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RuleResult.Success("TEST001", TimeSpan.FromMilliseconds(5))));
        rule2.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RuleResult.Success("TEST002", TimeSpan.FromMilliseconds(7))));

        _ruleEngine.RegisterRules(new[] { rule1, rule2 });

        // Act
        var result = await _ruleEngine.ExecuteByCategoryAsync(_mockContext, new[] { "CleanArchitecture" });

        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.Count.ShouldBe(1);
        result.RuleResults.Single().RuleId.ShouldBe("TEST001");
    }

    [Test]
    public async Task ExecuteByCategoryAsync_WithNonMatchingCategory_ShouldReturnEmptyResult()
    {
        // Arrange
        _mockRule.RuleId.Returns("TEST001");
        _mockRule.Category.Returns("CleanArchitecture");
        _ruleEngine.RegisterRule(_mockRule);

        // Act
        var result = await _ruleEngine.ExecuteByCategoryAsync(_mockContext, new[] { "NonExistent" });

        // Assert
        result.ShouldNotBeNull();
        result.RuleResults.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_WithFailFastOnCritical_ShouldStopOnCriticalFailure()
    {
        // Arrange
        var criticalRule = Substitute.For<IArchitectureRule>();
        var normalRule = Substitute.For<IArchitectureRule>();
        
        criticalRule.RuleId.Returns("CRITICAL001");
        criticalRule.Severity.Returns(RuleSeverity.Critical);
        normalRule.RuleId.Returns("NORMAL001");
        normalRule.Severity.Returns(RuleSeverity.Warning);
        
        _mockContext.Configuration.Execution.FailFastOnCritical.Returns(true);
        
        var criticalFailureResult = RuleResult.FromError("CRITICAL001", "Critical failure", TimeSpan.FromMilliseconds(5));
        criticalRule.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(criticalFailureResult));
        
        normalRule.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RuleResult.Success("NORMAL001", TimeSpan.FromMilliseconds(5))));

        _ruleEngine.RegisterRules(new[] { criticalRule, normalRule });

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _ruleEngine.ExecuteAsync(_mockContext));
    }

    [Test]
    public async Task ExecuteAsync_WithParallelExecution_ShouldRespectMaxDegreeOfParallelism()
    {
        // Arrange
        const int maxParallelism = 2;
        _mockContext.Configuration.Execution.MaxDegreeOfParallelism.Returns(maxParallelism);
        
        var executionTimes = new List<DateTime>();
        var rules = Enumerable.Range(0, 4).Select(i =>
        {
            var rule = Substitute.For<IArchitectureRule>();
            rule.RuleId.Returns($"TEST{i:000}");
            rule.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    executionTimes.Add(DateTime.UtcNow);
                    await Task.Delay(100); // Simulate work
                    return RuleResult.Success($"TEST{i:000}", TimeSpan.FromMilliseconds(100));
                });
            return rule;
        }).ToArray();

        _ruleEngine.RegisterRules(rules);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _ruleEngine.ExecuteAsync(_mockContext);
        stopwatch.Stop();

        // Assert
        result.RuleResults.Count.ShouldBe(4);
        // With max parallelism of 2, execution should take at least 200ms (2 batches of 100ms each)
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan(150);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(300);
    }

    [Test]
    public async Task ExecuteAsync_ShouldTrackExecutionTime()
    {
        // Arrange
        _mockRule.RuleId.Returns("TEST001");
        _mockRule.ValidateAsync(_mockContext, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return RuleResult.Success("TEST001", TimeSpan.FromMilliseconds(50));
            });
        
        _ruleEngine.RegisterRule(_mockRule);

        // Act
        var result = await _ruleEngine.ExecuteAsync(_mockContext);

        // Assert
        result.TotalExecutionTime.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(40));
        result.TotalExecutionTime.ShouldBeLessThan(TimeSpan.FromMilliseconds(200));
    }
}