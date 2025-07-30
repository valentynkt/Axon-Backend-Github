using Axon.Modules.Chat.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Axon.Tests.Shared.TestBase;

/// <summary>
/// Base class for Application layer tests providing common mocks and utilities
/// </summary>
[TestFixture]
public abstract class ApplicationTestBase
{
    protected Mock<IAiClient> AiClientMock { get; private set; } = null!;
    protected Mock<ILogger> LoggerMock { get; private set; } = null!;

    [SetUp]
    public virtual void BaseSetUp()
    {
        AiClientMock = new Mock<IAiClient>();
        LoggerMock = new Mock<ILogger>();
    }

    /// <summary>
    /// Creates a typed logger mock for specific handler types
    /// </summary>
    protected Mock<ILogger<T>> CreateTypedLoggerMock<T>() => new();

    [TearDown]
    public virtual void BaseTearDown()
    {
        AiClientMock.Reset();
        LoggerMock.Reset();
    }

    /// <summary>
    /// Creates a mock logger for a specific type
    /// </summary>
    protected Mock<ILogger<T>> CreateMockLogger<T>() => new();

    /// <summary>
    /// Verifies that a logger was called with a specific log level
    /// </summary>
    protected void VerifyLoggerCalled<T>(Mock<ILogger<T>> loggerMock, LogLevel logLevel, Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    /// <summary>
    /// Verifies that a logger was called with a specific log level and message content
    /// </summary>
    protected void VerifyLoggerCalledWith<T>(Mock<ILogger<T>> loggerMock, LogLevel logLevel, string messageContent)
    {
        loggerMock.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Creates a CancellationToken that cancels after specified milliseconds
    /// </summary>
    protected CancellationToken CreateTimeoutToken(int milliseconds = 5000)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(milliseconds);
        return cts.Token;
    }
}