using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Security;

namespace BuildingBlocks.Web.Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions
/// and returns consistent error responses
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = Activity.Current?.Id ?? context.TraceIdentifier;
        
        _logger.LogError(exception, 
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
            correlationId, context.Request.Path, context.Request.Method);

        // Don't overwrite response if it has already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response has already started, cannot modify status code or headers");
            return;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = GetStatusCode(exception);
        
        var errorResponse = new ErrorResponse(
            Error: GetErrorMessage(exception),
            Code: GetErrorCode(exception),
            CorrelationId: correlationId,
            Timestamp: DateTime.UtcNow
        );

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);

        await context.Response.WriteAsync(json);
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        ArgumentException or ArgumentNullException => 400,
        UnauthorizedAccessException => 401,
        SecurityException => 403,
        FileNotFoundException or KeyNotFoundException => 404,
        InvalidOperationException => 409,
        NotSupportedException => 422,
        TimeoutException => 408,
        _ => 500
    };

    private static string GetErrorMessage(Exception exception) => exception switch
    {
        ArgumentException or ArgumentNullException => "Invalid request parameters",
        UnauthorizedAccessException => "Authentication required",
        SecurityException => "Access forbidden",
        FileNotFoundException or KeyNotFoundException => "Resource not found",
        InvalidOperationException => "Operation not allowed in current state",
        NotSupportedException => "Operation not supported",
        TimeoutException => "Request timeout",
        _ => "An unexpected error occurred"
    };

    private static string GetErrorCode(Exception exception) => exception switch
    {
        ArgumentException or ArgumentNullException => "INVALID_REQUEST",
        UnauthorizedAccessException => "UNAUTHORIZED",
        SecurityException => "FORBIDDEN",
        FileNotFoundException or KeyNotFoundException => "NOT_FOUND",
        InvalidOperationException => "CONFLICT",
        NotSupportedException => "NOT_SUPPORTED", 
        TimeoutException => "TIMEOUT",
        _ => "INTERNAL_ERROR"
    };
}

/// <summary>
/// Standard error response format
/// </summary>
public record ErrorResponse(
    string Error,
    string Code,
    string CorrelationId,
    DateTime Timestamp
);