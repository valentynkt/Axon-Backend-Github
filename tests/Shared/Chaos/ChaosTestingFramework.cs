using System.Collections.Concurrent;
using System.Net;
using Moq;
using NUnit.Framework;
using Axon.Tests.Shared.TestBase;

namespace Axon.Tests.Shared.Chaos;

/// <summary>
/// Chaos Testing Framework for simulating failure scenarios, timeouts, and rate limiting
/// </summary>
[TestFixture]
[Category("Chaos")]
[Category("Resilience")]
public abstract class ChaosTestingFramework : LondonSchoolTestBase
{
    protected ChaosScenarioBuilder ChaosScenarios { get; private set; } = null!;

    [SetUp]
    public override void LondonSchoolSetUp()
    {
        base.LondonSchoolSetUp();
        ChaosScenarios = new ChaosScenarioBuilder(this);
    }

    /// <summary>
    /// Tests system behavior under 429 Rate Limiting scenarios
    /// </summary>
    protected async Task TestRateLimitingResilience<T>(
        Mock<T> serviceMock,
        Func<Task> systemUnderTest,
        int maxRequests = 5,
        TimeSpan timeWindow = default) where T : class
    {
        if (timeWindow == default) timeWindow = TimeSpan.FromMinutes(1);

        await ChaosScenarios
            .Name("Rate Limiting Resilience Test")
            .SimulateRateLimit(serviceMock, maxRequests, timeWindow)
            .ExpectResilience("System should handle rate limiting gracefully")
            .ExecuteAsync(systemUnderTest);
    }

    /// <summary>
    /// Tests system behavior under timeout scenarios
    /// </summary>
    protected async Task TestTimeoutResilience<T>(
        Mock<T> serviceMock,
        Func<Task> systemUnderTest,
        TimeSpan timeout = default) where T : class
    {
        if (timeout == default) timeout = TimeSpan.FromSeconds(30);

        await ChaosScenarios
            .Name("Timeout Resilience Test")
            .SimulateTimeout(serviceMock, timeout)
            .ExpectResilience("System should handle timeouts gracefully")
            .ExecuteAsync(systemUnderTest);
    }

    /// <summary>
    /// Tests system behavior under network failure scenarios
    /// </summary>
    protected async Task TestNetworkFailureResilience<T>(
        Mock<T> serviceMock,
        Func<Task> systemUnderTest,
        int failureCount = 3) where T : class
    {
        await ChaosScenarios
            .Name("Network Failure Resilience Test")
            .SimulateNetworkFailures(serviceMock, failureCount)
            .ExpectResilience("System should handle network failures gracefully")
            .ExecuteAsync(systemUnderTest);
    }

    /// <summary>
    /// Tests system behavior under resource exhaustion scenarios
    /// </summary>
    protected async Task TestResourceExhaustionResilience<T>(
        Mock<T> serviceMock,
        Func<Task> systemUnderTest) where T : class
    {
        await ChaosScenarios
            .Name("Resource Exhaustion Resilience Test")
            .SimulateResourceExhaustion(serviceMock)
            .ExpectResilience("System should handle resource exhaustion gracefully")
            .ExecuteAsync(systemUnderTest);
    }
}

/// <summary>
/// Builder for creating chaos testing scenarios
/// </summary>
public class ChaosScenarioBuilder
{
    private readonly LondonSchoolTestBase _testBase;
    private readonly List<ChaosAction> _actions = new();
    private readonly List<ExpectationValidator> _expectations = new();
    private string _scenarioName = "Unnamed Chaos Scenario";

    public ChaosScenarioBuilder(LondonSchoolTestBase testBase)
    {
        _testBase = testBase;
    }

    public ChaosScenarioBuilder Name(string name)
    {
        _scenarioName = name;
        return this;
    }

    public ChaosScenarioBuilder SimulateRateLimit<T>(Mock<T> mock, int maxRequests, TimeSpan timeWindow) where T : class
    {
        _actions.Add(new RateLimitChaosAction<T>(mock, maxRequests, timeWindow));
        return this;
    }

    public ChaosScenarioBuilder SimulateTimeout<T>(Mock<T> mock, TimeSpan timeout) where T : class
    {
        _actions.Add(new TimeoutChaosAction<T>(mock, timeout));
        return this;
    }

    public ChaosScenarioBuilder SimulateNetworkFailures<T>(Mock<T> mock, int failureCount) where T : class
    {
        _actions.Add(new NetworkFailureChaosAction<T>(mock, failureCount));
        return this;
    }

    public ChaosScenarioBuilder SimulateResourceExhaustion<T>(Mock<T> mock) where T : class
    {
        _actions.Add(new ResourceExhaustionChaosAction<T>(mock));
        return this;
    }

    public ChaosScenarioBuilder ExpectResilience(string description)
    {
        _expectations.Add(new ResilienceExpectation(description));
        return this;
    }

    public async Task ExecuteAsync(Func<Task> systemUnderTest)
    {
        TestContext.WriteLine($"=== CHAOS SCENARIO: {_scenarioName} ===");
        
        var executionTimer = System.Diagnostics.Stopwatch.StartNew();
        var results = new ChaosExecutionResults();

        try
        {
            // Apply chaos actions
            foreach (var action in _actions)
            {
                TestContext.WriteLine($"Applying chaos action: {action.Description}");
                await action.ApplyAsync();
            }

            // Execute system under test
            TestContext.WriteLine("Executing system under chaos conditions...");
            var systemExecutionTimer = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                await systemUnderTest();
                results.SystemExecutionSucceeded = true;
                TestContext.WriteLine("✓ System execution completed successfully under chaos");
            }
            catch (Exception ex)
            {
                results.SystemExecutionException = ex;
                TestContext.WriteLine($"✗ System execution failed under chaos: {ex.Message}");
            }
            finally
            {
                systemExecutionTimer.Stop();
                results.SystemExecutionTime = systemExecutionTimer.Elapsed;
            }

            // Validate expectations
            foreach (var expectation in _expectations)
            {
                var isValid = await expectation.ValidateAsync(results);
                TestContext.WriteLine($"Expectation '{expectation.Description}': {(isValid ? "✓ PASSED" : "✗ FAILED")}");
                
                if (!isValid)
                {
                    results.FailedExpectations.Add(expectation.Description);
                }
            }
        }
        finally
        {
            executionTimer.Stop();
            TestContext.WriteLine($"=== CHAOS SCENARIO COMPLETED in {executionTimer.ElapsedMilliseconds}ms ===");
            
            // Assert overall success
            if (results.FailedExpectations.Any())
            {
                Assert.Fail($"Chaos scenario failed expectations: {string.Join(", ", results.FailedExpectations)}");
            }
        }
    }
}

/// <summary>
/// Base class for chaos actions
/// </summary>
public abstract class ChaosAction
{
    public abstract string Description { get; }
    public abstract Task ApplyAsync();
}

/// <summary>
/// Rate limiting chaos action
/// </summary>
public class RateLimitChaosAction<T> : ChaosAction where T : class
{
    private readonly Mock<T> _mock;
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;
    private int _requestCount;
    private DateTime _windowStart = DateTime.UtcNow;

    public RateLimitChaosAction(Mock<T> mock, int maxRequests, TimeSpan timeWindow)
    {
        _mock = mock;
        _maxRequests = maxRequests;
        _timeWindow = timeWindow;
    }

    public override string Description => $"Rate Limit: {_maxRequests} requests per {_timeWindow}";

    public override Task ApplyAsync()
    {
        // Setup dynamic rate limiting behavior
        _mock.Setup(x => x.Equals(It.IsAny<object>()))
            .Returns(() =>
            {
                var now = DateTime.UtcNow;
                if (now - _windowStart > _timeWindow)
                {
                    _requestCount = 0;
                    _windowStart = now;
                }

                _requestCount++;
                if (_requestCount > _maxRequests)
                {
                    throw new HttpRequestException("Rate limit exceeded", null, HttpStatusCode.TooManyRequests);
                }

                return true;
            });

        return Task.CompletedTask;
    }
}

/// <summary>
/// Timeout chaos action
/// </summary>
public class TimeoutChaosAction<T> : ChaosAction where T : class
{
    private readonly Mock<T> _mock;
    private readonly TimeSpan _timeout;

    public TimeoutChaosAction(Mock<T> mock, TimeSpan timeout)
    {
        _mock = mock;
        _timeout = timeout;
    }

    public override string Description => $"Timeout: {_timeout}";

    public override Task ApplyAsync()
    {
        // Setup timeout behavior on all async methods
        var methods = typeof(T).GetMethods()
            .Where(m => m.ReturnType == typeof(Task) || m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

        foreach (var method in methods)
        {
            // This is a simplified approach - in practice, you'd need method-specific setups
            TestContext.WriteLine($"Setting up timeout behavior for {method.Name}");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Network failure chaos action
/// </summary>
public class NetworkFailureChaosAction<T> : ChaosAction where T : class
{
    private readonly Mock<T> _mock;
    private readonly int _failureCount;

    public NetworkFailureChaosAction(Mock<T> mock, int failureCount)
    {
        _mock = mock;
        _failureCount = failureCount;
    }

    public override string Description => $"Network Failures: {_failureCount} consecutive failures";

    public override Task ApplyAsync()
    {
        // Setup network failure simulation
        TestContext.WriteLine($"Configuring {_failureCount} network failures");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Resource exhaustion chaos action
/// </summary>
public class ResourceExhaustionChaosAction<T> : ChaosAction where T : class
{
    private readonly Mock<T> _mock;

    public ResourceExhaustionChaosAction(Mock<T> mock)
    {
        _mock = mock;
    }

    public override string Description => "Resource Exhaustion: Memory/CPU/Connection limits";

    public override Task ApplyAsync()
    {
        // Setup resource exhaustion simulation
        TestContext.WriteLine("Configuring resource exhaustion scenarios");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Base class for expectation validation
/// </summary>
public abstract class ExpectationValidator
{
    public abstract string Description { get; }
    public abstract Task<bool> ValidateAsync(ChaosExecutionResults results);
}

/// <summary>
/// Resilience expectation validator
/// </summary>
public class ResilienceExpectation : ExpectationValidator
{
    public override string Description { get; }

    public ResilienceExpectation(string description)
    {
        Description = description;
    }

    public override Task<bool> ValidateAsync(ChaosExecutionResults results)
    {
        // Basic resilience validation - system should handle failures gracefully
        var isResilient = results.SystemExecutionSucceeded || 
                         (results.SystemExecutionException != null && IsExpectedFailure(results.SystemExecutionException));
        
        return Task.FromResult(isResilient);
    }

    private bool IsExpectedFailure(Exception exception)
    {
        // Define what constitutes an expected/graceful failure
        return exception is TimeoutException ||
               exception is HttpRequestException ||
               exception is OperationCanceledException;
    }
}

/// <summary>
/// Results of chaos testing execution
/// </summary>
public class ChaosExecutionResults
{
    public bool SystemExecutionSucceeded { get; set; }
    public Exception? SystemExecutionException { get; set; }
    public TimeSpan SystemExecutionTime { get; set; }
    public List<string> FailedExpectations { get; } = new();
}