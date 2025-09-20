using System.Text.Json;
using Axon.Api.Contracts.Common;
using Axon.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Api.ErrorHandling.Tests;

/// <summary>
/// Unit tests for GlobalExceptionMiddleware handling of unhandled exceptions.
/// Verifies exception logging, ApiError response generation, and status code preservation.
/// </summary>
[TestFixture]
public class GlobalExceptionMiddlewareTests
{
    private ILogger<GlobalExceptionMiddleware> _logger = null!;
    private GlobalExceptionMiddleware _middleware = null!;
    private DefaultHttpContext _httpContext = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<GlobalExceptionMiddleware>>();
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
    }

    [Test]
    public async Task InvokeAsync_WhenNoException_ShouldCallNext()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Test]
    public async Task InvokeAsync_WhenExceptionThrown_ShouldHandleException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        RequestDelegate next = _ => throw exception;
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity); // InvalidOperationException maps to BusinessRule -> 422
        _httpContext.Response.ContentType.ShouldStartWith("application/json");
    }

    [Test]
    public async Task InvokeAsync_WhenExceptionThrown_ShouldLogException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        RequestDelegate next = _ => throw exception;
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _logger.Received().Log(
            Arg.Is<LogLevel>(l => l >= LogLevel.Information),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex == exception),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task InvokeAsync_WhenExceptionThrown_ShouldReturnApiErrorResponse()
    {
        // Arrange
        var exception = new ArgumentNullException("testParam", "Test argument null exception");
        RequestDelegate next = _ => throw exception;
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest); // ArgumentNullException maps to Validation -> 400

        // Read response body
        _httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        var apiError = JsonSerializer.Deserialize<ApiError>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        apiError.ShouldNotBeNull();
        apiError.Code.ShouldBe("VALIDATION_ERROR");
        apiError.Message.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task InvokeAsync_WithCorrelationIdHeader_ShouldUseCorrelationId()
    {
        // Arrange
        const string correlationId = "test-correlation-id";
        _httpContext.Request.Headers["X-Correlation-ID"] = correlationId;
        
        var exception = new InvalidOperationException("Test exception");
        RequestDelegate next = _ => throw exception;
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(correlationId)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task InvokeAsync_WithoutCorrelationIdHeader_ShouldUseTraceId()
    {
        // Arrange
        const string traceId = "test-trace-id";
        _httpContext.TraceIdentifier = traceId;
        
        var exception = new InvalidOperationException("Test exception");
        RequestDelegate next = _ => throw exception;
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(traceId)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task InvokeAsync_WhenResponseAlreadyStarted_ShouldNotSendResponse()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        RequestDelegate next = context =>
        {
            // Manually set response as started using reflection
            var responseFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>();
            if (responseFeature != null)
            {
                // Get the private field that tracks if response has started
                var hasStartedField = responseFeature.GetType().GetField("_hasStarted",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                hasStartedField?.SetValue(responseFeature, true);
            }
            throw exception;
        };
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act & Assert
        await Should.NotThrowAsync(() => _middleware.InvokeAsync(_httpContext));

        _logger.Received().LogWarning("Cannot send error response - response has already started");
    }

    [Test]
    public async Task InvokeAsync_WithDifferentExceptionTypes_ShouldMapToCorrectStatusCodes()
    {
        // Test data for different exception types and their expected status codes
        var testCases = new (Exception Exception, int StatusCode)[]
        {
            (new ArgumentNullException("param"), StatusCodes.Status400BadRequest),
            (new UnauthorizedAccessException("Access denied"), StatusCodes.Status401Unauthorized),
            (new InvalidOperationException("Invalid operation"), StatusCodes.Status422UnprocessableEntity),
            (new TimeoutException("Operation timed out"), StatusCodes.Status500InternalServerError),
#pragma warning disable CA2201 // Do not raise reserved exception types
            (new Exception("Generic exception"), StatusCodes.Status500InternalServerError)
#pragma warning restore CA2201 // Do not raise reserved exception types
        };

        foreach (var (exception, expectedStatusCode) in testCases)
        {
            // Arrange
            _httpContext = new DefaultHttpContext();
            _httpContext.Response.Body = new MemoryStream();
            
            RequestDelegate next = _ => throw exception;
            _middleware = new GlobalExceptionMiddleware(next, _logger);

            // Act
            await _middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.ShouldBe(expectedStatusCode, 
                $"Exception type {exception.GetType().Name} should map to status code {expectedStatusCode}");
        }
    }

    [Test]
    public async Task InvokeAsync_ShouldClearResponseBeforeSendingError()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        RequestDelegate next = context =>
        {
            context.Response.Headers["Custom-Header"] = "should-be-cleared";
            throw exception;
        };
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.ContentType.ShouldStartWith("application/json");
        _httpContext.Response.Headers.ShouldNotContainKey("Custom-Header");
    }

    [Test]
    public async Task InvokeAsync_ShouldLogRequestDetailsWithException()
    {
        // Arrange
        _httpContext.Request.Path = "/test/path";
        _httpContext.Request.Method = "POST";
        _httpContext.Request.Headers.UserAgent = "Test-Agent/1.0";
        _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        var exception = new InvalidOperationException("Test exception");
        RequestDelegate next = _ => throw exception;
        _middleware = new GlobalExceptionMiddleware(next, _logger);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(state => 
                state.ToString()!.Contains("POST") &&
                state.ToString()!.Contains("/test/path")),
            Arg.Is<Exception>(ex => ex == exception),
            Arg.Any<Func<object, Exception?, string>>());
    }
}