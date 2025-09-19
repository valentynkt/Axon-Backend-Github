using Axon.Modules.Chat.Domain.Tests.TestDoubles;
using CSharpFunctionalExtensions;
using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Base class for all Chat Domain tests.
/// Provides common setup, utilities, and patterns for domain testing.
/// Implements shared infrastructure like TimeProvider, logging, and cleanup.
/// </summary>
[TestFixture]
public abstract class DomainTestBase
{
    protected FakeTimeProvider TimeProvider { get; private set; } = null!;
    protected Faker Faker { get; private set; } = null!;

    /// <summary>
    /// Setup run before each test method.
    /// Initializes common test infrastructure and resets state.
    /// </summary>
    [SetUp]
    public virtual void SetUp()
    {
        // Initialize test infrastructure
        TimeProvider = new FakeTimeProvider(TestConstants.DateTimes.DefaultTestTime);
        Faker = new Faker();
        
        // Seed random for reproducible tests
        Faker.Random = new Randomizer(42);

        // Perform any derived class setup
        OnSetUp();
    }

    /// <summary>
    /// Cleanup run after each test method.
    /// Override in derived classes for additional cleanup.
    /// </summary>
    [TearDown]
    public virtual void TearDown()
    {
        OnTearDown();
    }

    /// <summary>
    /// Override in derived classes for additional setup.
    /// Called after base setup is complete.
    /// </summary>
    protected virtual void OnSetUp() { }

    /// <summary>
    /// Override in derived classes for additional cleanup.
    /// Called before base cleanup.
    /// </summary>
    protected virtual void OnTearDown() { }

    /// <summary>
    /// Advances test time by the specified amount.
    /// Useful for testing time-dependent behavior.
    /// </summary>
    protected void AdvanceTime(TimeSpan timeSpan)
    {
        TimeProvider.Advance(timeSpan);
    }

    /// <summary>
    /// Sets the test time to a specific value.
    /// </summary>
    protected void SetTime(DateTimeOffset time)
    {
        TimeProvider.SetUtcNow(time.UtcDateTime);
    }

    /// <summary>
    /// Gets the current test time.
    /// </summary>
    protected DateTimeOffset CurrentTime => TimeProvider.GetUtcNow();

    /// <summary>
    /// Asserts that a Result is successful and returns the value.
    /// Provides clear error messages for failed assertions.
    /// </summary>
    protected static T AssertSuccess<T, TError>(Result<T, TError> result, string? message = null)
        where TError : class
    {
        result.IsSuccess.ShouldBeTrue(message ?? $"Expected success but got error: {result.Error}");
        return result.Value;
    }

    /// <summary>
    /// Asserts that a Result is a failure and returns the error.
    /// Provides clear error messages for unexpected successes.
    /// </summary>
    protected static TError AssertFailure<T, TError>(Result<T, TError> result, string? message = null)
        where TError : class
    {
        result.IsFailure.ShouldBeTrue(message ?? $"Expected failure but got success: {result.Value}");
        return result.Error;
    }

    /// <summary>
    /// Asserts that a business rule exception is thrown with the expected rule type.
    /// </summary>
    protected static void AssertBusinessRuleViolation<TRule>(Action action, string? message = null)
        where TRule : IBusinessRule
    {
        var exception = Should.Throw<BusinessRuleException>(action, message);
        exception.RuleCode.ShouldBeOfType<TRule>();
    }

    /// <summary>
    /// Asserts that no business rule exception is thrown.
    /// </summary>
    protected static void AssertNoBusinessRuleViolation(Action action, string? message = null)
    {
        Should.NotThrow(action, message);
    }

    /// <summary>
    /// Creates a valid AxonUserId for testing.
    /// </summary>
    protected static AxonUserId CreateAxonUserId() => AxonUserId.New();

    /// <summary>
    /// Creates a valid ConversationId for testing.
    /// </summary>
    protected static ConversationId CreateConversationId() => ConversationId.New();

    /// <summary>
    /// Creates a valid MessageId for testing.
    /// </summary>
    protected static MessageId CreateMessageId() => MessageId.New();

    /// <summary>
    /// Creates a valid AiResponseId for testing.
    /// </summary>
    protected static AiResponseId CreateAiResponseId() => new AiResponseId(TestDataGenerator.GenerateAiResponseId());

    /// <summary>
    /// Creates a domain event test helper for asserting events were raised.
    /// </summary>
    protected static void AssertDomainEventRaised<TEvent>(IHasDomainEvents aggregate, string? message = null)
        where TEvent : class
    {
        var events = aggregate.DomainEvents;
        events.OfType<TEvent>().ShouldNotBeEmpty(message ?? $"Expected domain event {typeof(TEvent).Name} to be raised");
    }

    /// <summary>
    /// Asserts that no domain events were raised.
    /// </summary>
    protected static void AssertNoDomainEventsRaised(IHasDomainEvents aggregate, string? message = null)
    {
        aggregate.DomainEvents.ShouldBeEmpty(message ?? "Expected no domain events to be raised");
    }

    /// <summary>
    /// Asserts that exactly the expected number of domain events were raised.
    /// </summary>
    protected static void AssertDomainEventCount(IHasDomainEvents aggregate, int expectedCount, string? message = null)
    {
        aggregate.DomainEvents.Count.ShouldBe(expectedCount, 
            message ?? $"Expected {expectedCount} domain events but found {aggregate.DomainEvents.Count}");
    }

    /// <summary>
    /// Clears all domain events from an aggregate (useful for testing).
    /// </summary>
    protected static void ClearDomainEvents(IHasDomainEvents aggregate)
    {
        aggregate.ClearDomainEvents();
    }

    /// <summary>
    /// Creates test data for edge case scenarios.
    /// </summary>
    protected static class EdgeCaseData
    {
        public static string ExactMaxTitle => TestConstants.EdgeCases.ExactMaxTitle;
        public static string TooLongTitle => TestConstants.EdgeCases.OneOverMaxTitle;
        public static string ExactMaxMessage => TestConstants.EdgeCases.ExactMaxMessage;
        public static string TooLongMessage => TestConstants.EdgeCases.OneOverMaxMessage;
        public static string EmptyString => string.Empty;
        public static string WhitespaceString => "   ";
    }
}