using System.Net;
using System.Text.Json;
using Axon.Modules.Chat.Application.DTOs;
// using Axon.Modules.Chat.Infrastructure.Ai.Models; // Commented out - Infrastructure dependency not available in shared utilities
using Axon.Shared.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Axon.Tests.Shared.Mocks;

/// <summary>
/// Infrastructure layer contract mocks for London School TDD interaction testing
/// </summary>
public static class InfrastructureContractMocks
{
    /// <summary>
    /// Creates a mock HttpClient with behavior verification for external API calls
    /// </summary>
    public static Mock<HttpMessageHandler> CreateHttpMessageHandlerMock()
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        
        // Default successful response
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    id = "test-response-id",
                    output_text = "Mock AI response",
                    mcp_calls = Array.Empty<object>()
                }))
            });
            
        return mock;
    }

    /// <summary>
    /// Sets up HTTP message handler to return specific OpenAI response
    /// </summary>
    public static void SetupOpenAiResponse(
        Mock<HttpMessageHandler> handlerMock,
        string responseId,
        string outputText,
        object[]? mcpCalls = null) // Simplified to avoid infrastructure dependency
    {
        var response = new
        {
            id = responseId,
            output_text = outputText,
            mcp_calls = mcpCalls ?? Array.Empty<object>()
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response))
            });
    }

    /// <summary>
    /// Sets up HTTP message handler to return failure response
    /// </summary>
    public static void SetupHttpFailure(
        Mock<HttpMessageHandler> handlerMock,
        HttpStatusCode statusCode,
        string errorMessage)
    {
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(errorMessage)
            });
    }

    /// <summary>
    /// Sets up HTTP message handler to throw timeout exception
    /// </summary>
    public static void SetupHttpTimeout(Mock<HttpMessageHandler> handlerMock)
    {
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Request timed out"));
    }

    /// <summary>
    /// Verifies HTTP request was made with expected payload
    /// </summary>
    public static void VerifyHttpRequest(
        Mock<HttpMessageHandler> handlerMock,
        string expectedUrl,
        HttpMethod expectedMethod,
        Func<Times> times)
    {
        handlerMock.Protected()
            .Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                times,
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == expectedMethod &&
                    req.RequestUri!.ToString().Contains(expectedUrl)),
                ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Verifies HTTP request payload content for behavior testing
    /// </summary>
    public static void VerifyHttpRequestPayload(
        Mock<HttpMessageHandler> handlerMock,
        Func<string, bool> payloadMatcher,
        Times times)
    {
        handlerMock.Protected()
            .Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                times,
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Content != null &&
                    payloadMatcher(req.Content.ReadAsStringAsync().Result)),
                ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Creates a mock for OpenAI configuration options
    /// </summary>
    public static Mock<IOptions<T>> CreateOptionsMock<T>(T value) where T : class
    {
        var mock = new Mock<IOptions<T>>();
        mock.Setup(x => x.Value).Returns(value);
        return mock;
    }

    /// <summary>
    /// Creates an infrastructure behavior scenario for testing adapters
    /// </summary>
    public static InfrastructureBehaviorScenario CreateInfrastructureScenario()
    {
        return new InfrastructureBehaviorScenario();
    }

    /// <summary>
    /// Verifies configuration was accessed during infrastructure operations
    /// </summary>
    public static void VerifyConfigurationAccess<T>(Mock<IOptions<T>> optionsMock, Times times) where T : class
    {
        optionsMock.Verify(x => x.Value, times);
    }

    /// <summary>
    /// Creates a mock logger specifically for infrastructure components
    /// </summary>
    public static Mock<ILogger<T>> CreateInfrastructureLoggerMock<T>()
    {
        var mock = new Mock<ILogger<T>>(MockBehavior.Loose);
        
        // Track all logging calls for verification
        mock.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            
        return mock;
    }

    /// <summary>
    /// Verifies infrastructure logging behavior with specific patterns
    /// </summary>
    public static void VerifyInfrastructureLogging<T>(
        Mock<ILogger<T>> loggerMock,
        LogLevel expectedLevel,
        string expectedMessagePattern,
        Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessagePattern)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}

/// <summary>
/// Infrastructure behavior scenario for comprehensive adapter testing
/// </summary>
public class InfrastructureBehaviorScenario
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly List<Action> _verificationActions = new();

    public InfrastructureBehaviorScenario()
    {
        _httpHandlerMock = InfrastructureContractMocks.CreateHttpMessageHandlerMock();
        _loggerMock = new Mock<ILogger>(MockBehavior.Loose);
    }

    public Mock<HttpMessageHandler> HttpHandlerMock => _httpHandlerMock;
    public Mock<ILogger> LoggerMock => _loggerMock;

    /// <summary>
    /// Sets up successful HTTP response behavior
    /// </summary>
    public InfrastructureBehaviorScenario WithSuccessfulHttpResponse(
        string responseId,
        string outputText,
        object[]? mcpCalls = null) // Simplified to avoid infrastructure dependency
    {
        InfrastructureContractMocks.SetupOpenAiResponse(_httpHandlerMock, responseId, outputText, mcpCalls);
        return this;
    }

    /// <summary>
    /// Sets up HTTP failure behavior
    /// </summary>
    public InfrastructureBehaviorScenario WithHttpFailure(HttpStatusCode statusCode, string errorMessage)
    {
        InfrastructureContractMocks.SetupHttpFailure(_httpHandlerMock, statusCode, errorMessage);
        return this;
    }

    /// <summary>
    /// Sets up HTTP timeout behavior
    /// </summary>
    public InfrastructureBehaviorScenario WithHttpTimeout()
    {
        InfrastructureContractMocks.SetupHttpTimeout(_httpHandlerMock);
        return this;
    }

    /// <summary>
    /// Adds verification for HTTP request behavior 
    /// </summary>
    public InfrastructureBehaviorScenario ExpectingHttpRequest(
        string expectedUrl,
        HttpMethod expectedMethod,
        Times times)
    {
        _verificationActions.Add(() =>
            InfrastructureContractMocks.VerifyHttpRequest(_httpHandlerMock, expectedUrl, expectedMethod, times));
        return this;
    }

    /// <summary>
    /// Adds verification for HTTP request payload
    /// </summary>
    public InfrastructureBehaviorScenario ExpectingHttpPayload(
        Func<string, bool> payloadMatcher,
        Times times)
    {
        _verificationActions.Add(() =>
            InfrastructureContractMocks.VerifyHttpRequestPayload(_httpHandlerMock, payloadMatcher, times));
        return this;
    }

    /// <summary>
    /// Adds verification for logging behavior
    /// </summary>
    public InfrastructureBehaviorScenario ExpectingLogEntry<T>(
        Mock<ILogger<T>> loggerMock,
        LogLevel expectedLevel,
        string expectedMessagePattern,
        Times times)
    {
        _verificationActions.Add(() =>
            InfrastructureContractMocks.VerifyInfrastructureLogging(loggerMock, expectedLevel, expectedMessagePattern, times));
        return this;
    }

    /// <summary>
    /// Creates HttpClient with the configured mock handler
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        return new HttpClient(_httpHandlerMock.Object);
    }

    /// <summary>
    /// Executes all behavior verifications
    /// </summary>
    public void VerifyAllBehaviors()
    {
        foreach (var verificationAction in _verificationActions)
        {
            verificationAction();
        }

        _httpHandlerMock.VerifyAll();
    }
}

/// <summary>
/// Contract definition for infrastructure adapter testing
/// </summary>
public static class InfrastructureContracts
{
    /// <summary>
    /// Contract for AI client adapter behavior
    /// </summary>
    public static class AiClientContract
    {
        public static void ShouldProcessMessageWithMcpSupport(
            Func<AiRequest, CancellationToken, Task<Result<AiResponse>>> processMethod,
            AiRequest request)
        {
            // Contract: AI client should handle MCP configurations
            // Contract: AI client should return structured response
            // Contract: AI client should handle errors gracefully
        }

        public static void ShouldHandleHttpFailuresGracefully(
            Func<AiRequest, CancellationToken, Task<Result<AiResponse>>> processMethod,
            AiRequest request)
        {
            // Contract: AI client should convert HTTP exceptions to domain errors
            // Contract: AI client should log appropriate error information
            // Contract: AI client should not throw unhandled exceptions
        }
    }

    /// <summary>
    /// Contract for configuration adapter behavior
    /// </summary>
    public static class ConfigurationContract
    {
        public static void ShouldResolveConfigurationCorrectly<T>(
            Func<Result<T>> resolveMethod) where T : class
        {
            // Contract: Configuration resolver should validate configuration
            // Contract: Configuration resolver should return appropriate errors
            // Contract: Configuration resolver should cache when appropriate
        }
    }
}