using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Diagnostics;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Exceptions;
using BuildingBlocks.Web.Extensions;

namespace BuildingBlocks.Web.Middleware;

/// <summary>
/// Global exception handling middleware that converts exceptions to Result-aware responses.
/// Integrates with the Result pattern by converting uncaught exceptions to appropriate HTTP responses.
/// Provides consistent error formatting and logging across the application.
/// </summary>
public sealed class ResultExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResultExceptionMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ResultExceptionMiddleware(
        RequestDelegate next,
        ILogger<ResultExceptionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, 
                "Unhandled exception occurred while processing request {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await HandleExceptionAsync(context, exception);
        }
    }

    /// <summary>
    /// Handles exceptions by converting them to appropriate HTTP responses.
    /// Uses Result pattern mapping to ensure consistent error handling.
    /// </summary>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Convert exception to Result pattern for consistent handling
        var result = exception.ToResult();
        var error = result.Error;

        // Determine HTTP status code and response details
        var (statusCode, title, detail) = MapErrorToHttpResponse(error, exception);

        // Set response headers
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        // Create problem details response
        var problemDetails = new
        {
            type = GetProblemTypeUri(error.Type),
            title = title,
            status = (int)statusCode,
            detail = detail,
            instance = context.Request.Path.Value,
            traceId = context.TraceIdentifier,
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            errorCode = error.Code,
            errorType = error.Type.ToString()
        };

        // Add additional error context in development
        if (ShouldIncludeDetailedErrors(context))
        {
            var detailedProblem = new
            {
                problemDetails.type,
                problemDetails.title,
                problemDetails.status,
                problemDetails.detail,
                problemDetails.instance,
                problemDetails.traceId,
                problemDetails.timestamp,
                problemDetails.errorCode,
                problemDetails.errorType,
                stackTrace = exception.StackTrace,
                innerException = exception.InnerException?.Message,
                metadata = error.Metadata?.Count > 0 ? error.Metadata : null
            };

            var detailedJson = JsonSerializer.Serialize(detailedProblem, _jsonOptions);
            await context.Response.WriteAsync(detailedJson);
            return;
        }

        // Serialize and write response
        var json = JsonSerializer.Serialize(problemDetails, _jsonOptions);
        await context.Response.WriteAsync(json);

        // Log additional context
        LogExceptionContext(context, error, statusCode, exception);
    }

    /// <summary>
    /// Maps Error types to appropriate HTTP status codes and messages.
    /// </summary>
    private static (HttpStatusCode statusCode, string title, string detail) MapErrorToHttpResponse(
        Error error, 
        Exception originalException)
    {
        return error.Type switch
        {
            ErrorType.Validation => (
                HttpStatusCode.BadRequest,
                "Validation Failed",
                error.Message),

            ErrorType.NotFound => (
                HttpStatusCode.NotFound,
                "Resource Not Found",
                error.Message),

            ErrorType.Unauthorized => (
                HttpStatusCode.Unauthorized,
                "Unauthorized Access",
                error.Message),

            ErrorType.Conflict => (
                HttpStatusCode.Conflict,
                "Conflict",
                error.Message),

            ErrorType.BusinessRule => (
                HttpStatusCode.UnprocessableEntity,
                "Business Rule Violation",
                error.Message),

            ErrorType.Cancelled => (
                HttpStatusCode.RequestTimeout,
                "Operation Cancelled",
                "The operation was cancelled before completion."),

            ErrorType.Aggregate => (
                HttpStatusCode.BadRequest,
                "Multiple Errors",
                error.Message),

            ErrorType.Internal => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later."),

            _ => HandleSpecialExceptions(originalException)
        };
    }

    /// <summary>
    /// Handles special .NET exception types that don't map directly to our Error types.
    /// </summary>
    private static (HttpStatusCode statusCode, string title, string detail) HandleSpecialExceptions(Exception exception)
    {
        return exception switch
        {
            TaskCanceledException or OperationCanceledException => (
                HttpStatusCode.RequestTimeout,
                "Request Timeout",
                "The request was cancelled or timed out."),

            ArgumentException or ArgumentNullException => (
                HttpStatusCode.BadRequest,
                "Invalid Argument",
                "One or more arguments are invalid."),

            NotImplementedException => (
                HttpStatusCode.NotImplemented,
                "Not Implemented",
                "This feature is not yet implemented."),

            UnauthorizedAccessException => (
                HttpStatusCode.Forbidden,
                "Access Forbidden",
                "You don't have permission to access this resource."),

            InvalidOperationException => (
                HttpStatusCode.BadRequest,
                "Invalid Operation",
                "The requested operation is not valid in the current state."),

            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later.")
        };
    }

    /// <summary>
    /// Gets the problem type URI based on error type.
    /// Follows RFC 7807 problem details specification.
    /// </summary>
    private static string GetProblemTypeUri(ErrorType errorType)
    {
        var baseUri = "https://tools.ietf.org/html/rfc7231";
        
        return errorType switch
        {
            ErrorType.Validation => $"{baseUri}#section-6.5.1", // Bad Request
            ErrorType.NotFound => $"{baseUri}#section-6.5.4", // Not Found
            ErrorType.Unauthorized => $"{baseUri}#section-6.5.1", // Unauthorized
            ErrorType.Conflict => $"{baseUri}#section-6.5.8", // Conflict
            ErrorType.BusinessRule => "https://tools.ietf.org/html/rfc4918#section-11.2", // Unprocessable Entity
            ErrorType.Cancelled => $"{baseUri}#section-6.5.7", // Request Timeout
            ErrorType.Internal => $"{baseUri}#section-6.6.1", // Internal Server Error
            _ => $"{baseUri}#section-6.6.1"
        };
    }

    /// <summary>
    /// Determines if detailed error information should be included in the response.
    /// Only includes in development or when explicitly configured.
    /// </summary>
    private static bool ShouldIncludeDetailedErrors(HttpContext context)
    {
        // Check if we're in development environment
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        
        // Check for explicit header requesting detailed errors (for debugging)
        var includeDetails = context.Request.Headers.ContainsKey("X-Include-Error-Details");
        
        return isDevelopment || includeDetails;
    }

    /// <summary>
    /// Logs additional context about the exception for monitoring and debugging.
    /// </summary>
    private void LogExceptionContext(
        HttpContext context,
        Error error,
        HttpStatusCode statusCode,
        Exception _)
    {
        var logLevel = GetLogLevelForStatusCode(statusCode);
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value ?? "",
            ["StatusCode"] = (int)statusCode,
            ["ErrorCode"] = error.Code,
            ["ErrorType"] = error.Type.ToString(),
            ["TraceId"] = context.TraceIdentifier,
            ["UserId"] = context.User?.Identity?.Name ?? "Anonymous"
        });

        _logger.Log(logLevel,
            "Request {Method} {Path} resulted in {StatusCode} ({ErrorType}): {ErrorMessage}",
            context.Request.Method,
            context.Request.Path,
            (int)statusCode,
            error.Type,
            error.Message);

        // Log additional metadata if present
        if (error.Metadata?.Count > 0)
        {
            _logger.LogDebug("Error metadata: {@Metadata}", error.Metadata);
        }
    }

    /// <summary>
    /// Gets appropriate log level based on HTTP status code.
    /// </summary>
    private static LogLevel GetLogLevelForStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode switch
        {
            >= 500 => LogLevel.Error,      // Server errors
            >= 400 and < 500 => LogLevel.Warning, // Client errors
            _ => LogLevel.Information       // Success and redirects
        };
    }
}