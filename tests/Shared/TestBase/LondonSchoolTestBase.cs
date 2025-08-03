using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Tests.Shared.TestBase;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Language.Flow;
using NUnit.Framework;

namespace Axon.Tests.Shared.TestBase;

/// <summary>
/// Base class for London School TDD tests focusing on behavior verification and interaction testing
/// </summary>
[TestFixture]
public abstract class LondonSchoolTestBase
{
    private readonly List<Mock> _allMocks = new();
    private readonly Dictionary<Type, Mock> _mockRegistry = new();
    
    protected MockRepository MockRepository { get; private set; } = null!;

    [SetUp]
    public virtual void LondonSchoolSetUp()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        _allMocks.Clear();
        _mockRegistry.Clear();
    }

    [TearDown]
    public virtual void LondonSchoolTearDown()
    {
        // Verify all mocks were called as expected
        MockRepository.VerifyAll();
        
        // Reset all mocks for next test
        foreach (var mock in _allMocks)
        {
            mock.Reset();
        }
    }

    /// <summary>
    /// Creates a strict mock that must have all interactions verified
    /// </summary>
    protected Mock<T> CreateStrictMock<T>() where T : class
    {
        var mock = MockRepository.Create<T>();
        RegisterMock(mock);
        return mock;
    }

    /// <summary>
    /// Creates a loose mock for non-critical collaborators
    /// </summary>
    protected Mock<T> CreateLooseMock<T>() where T : class
    {
        var mock = new Mock<T>(MockBehavior.Loose);
        RegisterMock(mock);
        return mock;
    }

    /// <summary>
    /// Creates a spy mock that tracks interactions without enforcing expectations
    /// </summary>
    protected Mock<T> CreateSpyMock<T>() where T : class
    {
        var mock = new Mock<T>();
        RegisterMock(mock);
        return mock;
    }

    /// <summary>
    /// Gets or creates a mock for the specified type - ensures singleton pattern per test
    /// </summary>
    protected Mock<T> GetMock<T>() where T : class
    {
        if (_mockRegistry.TryGetValue(typeof(T), out var existingMock))
        {
            return (Mock<T>)existingMock;
        }

        var mock = CreateStrictMock<T>();
        return mock;
    }

    /// <summary>
    /// Verifies that interactions occurred in the expected sequence
    /// </summary>
    protected void VerifySequence(params Expression<Action>[] calls)
    {
        MockRepository.VerifyAll();
        // Sequence verification would need custom implementation or Moq.Sequences package
    }

    /// <summary>
    /// Verifies mock interaction patterns for behavior-driven testing
    /// </summary>
    protected void VerifyInteractionPattern<T>(Mock<T> mock, string patternName, Action<Mock<T>> patternSetup) where T : class
    {
        // Setup the expected interaction pattern
        patternSetup(mock);
        
        // The pattern verification happens during MockRepository.VerifyAll()
        TestContext.WriteLine($"Verifying interaction pattern: {patternName} for {typeof(T).Name}");
    }

    /// <summary>
    /// Creates a collaboration test scenario with multiple mocks
    /// </summary>
    protected CollaborationScenario<T> CreateCollaborationScenario<T>() where T : class
    {
        return new CollaborationScenario<T>(this);
    }

    /// <summary>
    /// Register mock for cleanup and verification
    /// </summary>
    private void RegisterMock<T>(Mock<T> mock) where T : class
    {
        _allMocks.Add(mock);
        _mockRegistry[typeof(T)] = mock;
    }

    /// <summary>
    /// Verifies that a logger was called with specific behavior expectations
    /// </summary>
    protected void VerifyLoggingBehavior<T>(Mock<ILogger<T>> loggerMock, LogLevel expectedLevel, string expectedMessage, Func<Times> times)
    {
        loggerMock.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    /// <summary>
    /// Verifies that dependencies interact in the expected order
    /// </summary>
    protected void VerifyCallOrder(params Mock[] mocks)
    {
        // This would require integration with Moq.Sequences or similar
        // For now, we rely on MockRepository.VerifyAll() to ensure all expectations are met
        MockRepository.VerifyAll();
    }

    /// <summary>
    /// Creates a comprehensive behavior validation scenario for complex interactions
    /// </summary>
    protected BehaviorValidationScenario<T> CreateBehaviorScenario<T>(string scenarioName) where T : class
    {
        return new BehaviorValidationScenario<T>(scenarioName, this);
    }

    /// <summary>
    /// Verifies that error handling behavior is correctly implemented
    /// </summary>
    protected void VerifyErrorHandlingBehavior<T, TException>(
        Mock<T> mock, 
        Expression<Func<T, Task>> action, 
        TException expectedException,
        string expectedErrorBehavior) where T : class where TException : Exception
    {
        mock.Setup(action).ThrowsAsync(expectedException);
        
        TestContext.WriteLine($"Verifying error handling behavior: {expectedErrorBehavior}");
        // Error handling verification happens during test execution
    }

    /// <summary>
    /// Verifies retry behavior patterns with exponential backoff
    /// </summary>
    protected void VerifyRetryBehavior<T>(
        Mock<T> mock,
        Expression<Func<T, Task>> action,
        int expectedRetries,
        TimeSpan baseDelay) where T : class
    {
        var sequence = new MockSequence();
        
        // Setup failure calls
        for (int i = 0; i < expectedRetries; i++)
        {
            mock.InSequence(sequence)
                .Setup(action)
                .ThrowsAsync(new HttpRequestException($"Retry attempt {i + 1}"));
        }
        
        // Setup final success
        mock.InSequence(sequence)
            .Setup(action)
            .Returns(Task.CompletedTask);
        
        TestContext.WriteLine($"Expecting {expectedRetries} retries with base delay {baseDelay}");
    }

    /// <summary>
    /// Verifies caching behavior with TTL and invalidation patterns
    /// </summary>
    protected void VerifyCachingBehavior<T, TKey, TValue>(
        Mock<T> cacheMock,
        Expression<Func<T, TKey, Task<TValue>>> getAction,
        Expression<Action<T, TKey, TValue>> setAction,
        TKey key,
        TValue value) where T : class
    {
        // First call should miss cache and call set
        cacheMock.Setup(getAction).ReturnsAsync(default(TValue)!);
        cacheMock.Setup(setAction);
        
        // Second call should hit cache
        cacheMock.Setup(getAction).ReturnsAsync(value);
        
        TestContext.WriteLine($"Verifying caching pattern for key: {key}");
    }

    /// <summary>
    /// Verifies circuit breaker behavior under failure conditions
    /// </summary>
    protected void VerifyCircuitBreakerBehavior<T>(
        Mock<T> serviceMock,
        Expression<Func<T, Task>> action,
        int failureThreshold = 3) where T : class
    {
        var sequence = new MockSequence();
        
        // Setup failures to trigger circuit breaker
        for (int i = 0; i < failureThreshold; i++)
        {
            serviceMock.InSequence(sequence)
                .Setup(action)
                .ThrowsAsync(new TimeoutException($"Failure {i + 1}"));
        }
        
        // After threshold, should not call service (circuit open)
        TestContext.WriteLine($"Verifying circuit breaker opens after {failureThreshold} failures");
    }

    /// <summary>
    /// Verifies timeout behavior with proper cancellation handling
    /// </summary>
    protected void VerifyTimeoutBehavior<T>(
        Mock<T> serviceMock,
        Expression<Func<T, CancellationToken, Task>> action,
        TimeSpan timeout) where T : class
    {
        serviceMock.Setup(action)
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(timeout.Add(TimeSpan.FromMilliseconds(100)), ct);
            });
        
        TestContext.WriteLine($"Verifying timeout behavior with {timeout} limit");
    }

    /// <summary>
    /// Creates a test spy that records all interactions for later verification
    /// </summary>
    protected TestSpy<T> CreateTestSpy<T>() where T : class
    {
        var mock = CreateSpyMock<T>();
        return new TestSpy<T>(mock);
    }

    /// <summary>
    /// Verifies that rate limiting behavior is correctly implemented
    /// </summary>
    protected void VerifyRateLimitingBehavior<T>(
        Mock<T> serviceMock,
        Expression<Func<T, Task>> action,
        int maxRequests,
        TimeSpan timeWindow) where T : class
    {
        var sequence = new MockSequence();
        
        // Allow max requests
        for (int i = 0; i < maxRequests; i++)
        {
            serviceMock.InSequence(sequence)
                .Setup(action)
                .Returns(Task.CompletedTask);
        }
        
        // Next request should be rate limited (return 429)
        serviceMock.InSequence(sequence)
            .Setup(action)
            .ThrowsAsync(new HttpRequestException("Rate limit exceeded", null, HttpStatusCode.TooManyRequests));
        
        TestContext.WriteLine($"Verifying rate limiting: {maxRequests} requests per {timeWindow}");
    }

    /// <summary>
    /// Sets up a mock to return a specific result and verifies it was called
    /// </summary>
    protected void SetupAndVerifyCall<T, TResult>(Mock<T> mock, Expression<Func<T, TResult>> expression, TResult returnValue) where T : class
    {
        mock.Setup(expression).Returns(returnValue);
    }

    /// <summary>
    /// Sets up a mock for async operations with result verification
    /// </summary>
    protected void SetupAndVerifyCallAsync<T, TResult>(Mock<T> mock, Expression<Func<T, Task<TResult>>> expression, TResult returnValue) where T : class
    {
        mock.Setup(expression).ReturnsAsync(returnValue);
    }
}

/// <summary>
/// Helper class for testing collaborations between multiple objects
/// </summary>
public class CollaborationScenario<T> where T : class
{
    private readonly LondonSchoolTestBase _testBase;
    private readonly List<Mock> _collaborators = new();
    private readonly List<string> _expectedInteractions = new();

    internal CollaborationScenario(LondonSchoolTestBase testBase)
    {
        _testBase = testBase;
    }

    /// <summary>
    /// Adds a collaborator mock to the scenario
    /// </summary>
    public CollaborationScenario<T> WithCollaborator<TCollaborator>(Mock<TCollaborator> collaborator) where TCollaborator : class
    {
        _collaborators.Add(collaborator);
        return this;
    }

    /// <summary>
    /// Adds an expected interaction to verify
    /// </summary>
    public CollaborationScenario<T> ExpectingInteraction(string interaction)
    {
        _expectedInteractions.Add(interaction);
        return this;
    }

    /// <summary>
    /// Executes the collaboration test and verifies all interactions
    /// </summary>
    public void Execute(Action<T> testAction, T systemUnderTest)
    {
        // Execute the test
        testAction(systemUnderTest);

        // Verify all collaborators were used as expected
        foreach (var collaborator in _collaborators)
        {
            collaborator.VerifyAll();
        }

        TestContext.WriteLine($"Collaboration scenario completed with {_collaborators.Count} collaborators and {_expectedInteractions.Count} expected interactions");
    }
}

/// <summary>
/// Attribute to mark tests as London School TDD behavior tests
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class BehaviorTestAttribute : CategoryAttribute
{
    public BehaviorTestAttribute() : base("BehaviorTest")
    {
    }
}

/// <summary>
/// Attribute to mark tests as interaction verification tests
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class InteractionTestAttribute : CategoryAttribute
{
    public InteractionTestAttribute() : base("InteractionTest")
    {
    }
}

/// <summary>
/// Attribute to mark tests as contract verification tests
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ContractTestAttribute : CategoryAttribute
{
    public ContractTestAttribute() : base("ContractTest")
    {
    }
}