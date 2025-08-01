using System.Diagnostics;
using Moq;
using NUnit.Framework;

namespace Axon.Tests.Shared.TestBase;

/// <summary>
/// Comprehensive behavior validation scenario for complex interaction testing
/// </summary>
public class BehaviorValidationScenario<T> where T : class
{
    private readonly string _scenarioName;
    private readonly LondonSchoolTestBase _testBase;
    private readonly List<InteractionStep> _steps = new();
    private readonly Stopwatch _executionTimer = new();

    public BehaviorValidationScenario(string scenarioName, LondonSchoolTestBase testBase)
    {
        _scenarioName = scenarioName;
        _testBase = testBase;
    }

    /// <summary>
    /// Adds an interaction step to the scenario
    /// </summary>
    public BehaviorValidationScenario<T> When(string description, Action<Mock<T>> interaction)
    {
        _steps.Add(new InteractionStep
        {
            Description = description,
            StepType = StepType.When,
            Interaction = interaction
        });
        return this;
    }

    /// <summary>
    /// Adds a verification step to the scenario
    /// </summary>
    public BehaviorValidationScenario<T> Then(string description, Action<Mock<T>> verification)
    {
        _steps.Add(new InteractionStep
        {
            Description = description,
            StepType = StepType.Then,
            Interaction = verification
        });
        return this;
    }

    /// <summary>
    /// Adds a setup step to the scenario
    /// </summary>
    public BehaviorValidationScenario<T> Given(string description, Action<Mock<T>> setup)
    {
        _steps.Add(new InteractionStep
        {
            Description = description,
            StepType = StepType.Given,
            Interaction = setup
        });
        return this;
    }

    /// <summary>
    /// Executes the complete behavior scenario
    /// </summary>
    public async Task ExecuteAsync(Mock<T> mock)
    {
        TestContext.WriteLine($"=== EXECUTING BEHAVIOR SCENARIO: {_scenarioName} ===");
        _executionTimer.Start();

        try
        {
            foreach (var step in _steps)
            {
                TestContext.WriteLine($"{step.StepType}: {step.Description}");
                
                var stepTimer = Stopwatch.StartNew();
                step.Interaction(mock);
                stepTimer.Stop();
                
                TestContext.WriteLine($"  ✓ Completed in {stepTimer.ElapsedMilliseconds}ms");
            }
        }
        finally
        {
            _executionTimer.Stop();
            TestContext.WriteLine($"=== SCENARIO COMPLETED in {_executionTimer.ElapsedMilliseconds}ms ===");
        }
    }

    private class InteractionStep
    {
        public required string Description { get; init; }
        public required StepType StepType { get; init; }
        public required Action<Mock<T>> Interaction { get; init; }
    }

    private enum StepType { Given, When, Then }
}

/// <summary>
/// Test spy for recording and analyzing interactions
/// </summary>
public class TestSpy<T> where T : class
{
    private readonly Mock<T> _mock;
    private readonly List<InteractionRecord> _interactions = new();

    public TestSpy(Mock<T> mock)
    {
        _mock = mock;
    }

    public Mock<T> Mock => _mock;
    public IReadOnlyList<InteractionRecord> Interactions => _interactions.AsReadOnly();

    /// <summary>
    /// Records an interaction with the spy
    /// </summary>
    public void Record(string methodName, object[] parameters)
    {
        _interactions.Add(new InteractionRecord
        {
            Timestamp = DateTime.UtcNow,
            MethodName = methodName,
            Parameters = parameters,
            ThreadId = Thread.CurrentThread.ManagedThreadId
        });
    }

    /// <summary>
    /// Verifies interaction patterns occurred as expected
    /// </summary>
    public void VerifyInteractionPattern(string patternName, Func<IReadOnlyList<InteractionRecord>, bool> patternCheck)
    {
        var patternMatched = patternCheck(_interactions);
        
        TestContext.WriteLine($"Verifying interaction pattern: {patternName}");
        TestContext.WriteLine($"Total interactions recorded: {_interactions.Count}");
        
        if (!patternMatched)
        {
            TestContext.WriteLine("Recorded interactions:");
            foreach (var interaction in _interactions)
            {
                TestContext.WriteLine($"  {interaction.Timestamp:HH:mm:ss.fff} - {interaction.MethodName}");
            }
        }
        
        Assert.That(patternMatched, Is.True, $"Interaction pattern '{patternName}' was not satisfied");
    }

    public class InteractionRecord
    {
        public required DateTime Timestamp { get; init; }
        public required string MethodName { get; init; }
        public required object[] Parameters { get; init; }
        public required int ThreadId { get; init; }
    }
}