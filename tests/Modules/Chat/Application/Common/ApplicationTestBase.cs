namespace Axon.Modules.Chat.Application.Tests.Common;

/// <summary>
/// Base class for all Chat application layer tests.
/// Provides common testing infrastructure including mocking, timing control, and assertion helpers.
/// Follows SOLID principles with extensible design for different test scenarios.
/// </summary>
[TestFixture]
public abstract class ApplicationTestBase : DomainTestBase
{
    protected ServiceCollection Services { get; private set; } = null!;
    protected ServiceProvider ServiceProvider { get; private set; } = null!;
    protected ILogger<ApplicationTestBase> Logger { get; private set; } = null!;

    /// <summary>
    /// Common application services that are frequently mocked
    /// </summary>
    protected IUserAuthenticationService MockAuthService { get; private set; } = null!;
    protected IChatTelemetry MockTelemetry { get; private set; } = null!;
    protected ILogger MockLogger { get; private set; } = null!;

    protected override void OnSetUp()
    {
        Services = new ServiceCollection();
        ConfigureServices(Services);
        ServiceProvider = Services.BuildServiceProvider();
        Logger = ServiceProvider.GetRequiredService<ILogger<ApplicationTestBase>>();

        SetupCommonMocks();
        OnApplicationSetUp();
    }

    protected override void OnTearDown()
    {
        OnApplicationTearDown();
        ServiceProvider?.Dispose();
    }

    /// <summary>
    /// Override to configure additional services for specific test scenarios
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();
    }

    /// <summary>
    /// Sets up commonly used mocks with sensible defaults
    /// </summary>
    protected virtual void SetupCommonMocks()
    {
        MockAuthService = Substitute.For<IUserAuthenticationService>();
        MockTelemetry = Substitute.For<IChatTelemetry>();
        MockLogger = Substitute.For<ILogger>();

        // Configure default behaviors
        MockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(CreateUserId()));

        MockTelemetry.StartActivity(Arg.Any<string>())
            .Returns((Activity?)null);
    }

    /// <summary>
    /// Override in derived classes for additional application-specific setup
    /// </summary>
    protected virtual void OnApplicationSetUp() { }

    /// <summary>
    /// Override in derived classes for additional application-specific cleanup
    /// </summary>
    protected virtual void OnApplicationTearDown() { }

    /// <summary>
    /// Creates a mock handler for testing purposes
    /// </summary>
    protected static T CreateMockHandler<T>() where T : class
    {
        return Substitute.For<T>();
    }

    /// <summary>
    /// Asserts that a command/query result is successful and returns the value
    /// </summary>
    protected static T AssertSuccess<T>(Result<T, Error> result, string? customMessage = null)
    {
        var message = customMessage ?? 
            (result.IsFailure ? $"Expected successful result but got error: {result.Error.Message}" 
                              : "Expected successful result");
        result.IsSuccess.ShouldBeTrue(message);
        return result.Value;
    }

    /// <summary>
    /// Asserts that a command/query result is a failure and returns the error
    /// </summary>
    protected static Error AssertFailure<T>(Result<T, Error> result, string? customMessage = null)
    {
        var message = customMessage ?? 
            (result.IsSuccess ? $"Expected failure but got success with value: {result.Value}" 
                              : "Expected failure");
        result.IsFailure.ShouldBeTrue(message);
        return result.Error;
    }

    /// <summary>
    /// Asserts that a result contains a specific error type
    /// </summary>
    protected static TError AssertErrorType<T, TError>(Result<T, Error> result)
        where TError : class
    {
        AssertFailure(result);
        result.Error.ShouldBeOfType<TError>();
        return (TError)(object)result.Error;
    }

    /// <summary>
    /// Creates a cancellation token for testing with optional timeout
    /// </summary>
    protected static CancellationToken CreateCancellationToken(TimeSpan? timeout = null)
    {
        if (timeout.HasValue)
        {
            using var cts = new CancellationTokenSource(timeout.Value);
            return cts.Token;
        }
        return CancellationToken.None;
    }

    /// <summary>
    /// Measures execution time of an async operation
    /// </summary>
    protected static async Task<(T result, TimeSpan elapsed)> MeasureExecutionTimeAsync<T>(Func<Task<T>> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await operation();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed);
    }

    /// <summary>
    /// Asserts that an operation completes within the expected time
    /// </summary>
    protected static async Task AssertExecutionTime<T>(
        Func<Task<T>> operation,
        TimeSpan maxExecutionTime,
        string? message = null)
    {
        var (_, elapsed) = await MeasureExecutionTimeAsync(operation);
        elapsed.ShouldBeLessThan(maxExecutionTime,
            message ?? $"Operation took {elapsed.TotalMilliseconds}ms but should have completed within {maxExecutionTime.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Verifies that authentication service was called correctly
    /// </summary>
    protected void AssertAuthenticationCalled()
    {
        MockAuthService.Received(1).GetAuthenticatedUserId();
    }

    /// <summary>
    /// Verifies that telemetry activity was started with the expected name
    /// </summary>
    protected void AssertTelemetryActivityStarted(string expectedActivityName)
    {
        MockTelemetry.Received(1).StartActivity(expectedActivityName);
    }
}