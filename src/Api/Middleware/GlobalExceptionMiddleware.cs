using System.Diagnostics;
using Axon.Api.Contracts.Common;
using Axon.Api.ErrorHandling;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Axon.Api.Middleware;

/// <summary>
/// Global exception handling middleware that wraps unhandled exceptions in consistent ApiError envelopes.
/// Logs full exception details with correlation IDs while returning safe error information to clients.
/// Preserves existing HTTP status codes from handled errors.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

    /// <summary>
    /// Handles unhandled exceptions by converting them to structured Error objects
    /// and responding with consistent ApiError envelopes.
    /// </summary>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValue) 
            ? headerValue.ToString() 
            : traceId;

        // Don't handle responses that have already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Cannot send error response - response has already started");
            return;
        }

        // Convert exception to structured Error
        var error = Error.FromException(exception)
            .WithCorrelationId(correlationId)
            .WithSource("GlobalExceptionMiddleware");

        // Log full exception details for debugging
        LogExceptionDetails(context, exception, error, correlationId);

        // Clear any existing response content and headers
        context.Response.Clear();
        context.Response.Headers.Clear();

        // Send structured ApiError response
        await context.SendApiErrorAsync(error);
    }

    /// <summary>
    /// Logs comprehensive exception details for debugging while ensuring
    /// no sensitive information is exposed to clients.
    /// </summary>
    private void LogExceptionDetails(HttpContext context, Exception exception, Error error, string correlationId)
    {
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            ["RequestPath"] = requestPath,
            ["RequestMethod"] = requestMethod,
            ["RemoteIP"] = remoteIp,
            ["UserAgent"] = userAgent,
            ["ErrorCode"] = error.Code,
            ["ErrorType"] = error.Type.ToString(),
            ["HttpStatusCode"] = error.ToHttpStatusCode()
        });

        // Log at appropriate level based on error severity
        var logLevel = DetermineLogLevel(error.Severity, error.Type);
        
        _logger.Log(logLevel, exception, 
            "Unhandled exception in {RequestMethod} {RequestPath}. " +
            "Error: {ErrorCode} ({ErrorType}). " +
            "CorrelationId: {CorrelationId}",
            requestMethod, requestPath, error.Code, error.Type, correlationId);

        // Log additional context for critical errors
        if (logLevel >= LogLevel.Error)
        {
            _logger.LogError("Exception Details - Type: {ExceptionType}, Message: {ExceptionMessage}, " +
                           "StackTrace: {StackTrace}", 
                exception.GetType().FullName, exception.Message, exception.StackTrace);
        }
    }

    /// <summary>
    /// Determines the appropriate log level based on error severity and type.
    /// </summary>
    private static LogLevel DetermineLogLevel(ErrorSeverity severity, ErrorType errorType)
    {
        return severity switch
        {
            ErrorSeverity.Fatal => LogLevel.Critical,
            ErrorSeverity.Critical => LogLevel.Critical,
            ErrorSeverity.Error => LogLevel.Error,
            ErrorSeverity.Warning => errorType is ErrorType.Validation or ErrorType.BusinessRule 
                ? LogLevel.Information 
                : LogLevel.Warning,
            ErrorSeverity.Info => LogLevel.Information,
            _ => LogLevel.Warning
        };
    }
}

/// <summary>
/// Extension methods for registering GlobalExceptionMiddleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    /// <summary>
    /// Registers the GlobalExceptionMiddleware in the request pipeline.
    /// Should be called early in the pipeline to catch all unhandled exceptions.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}