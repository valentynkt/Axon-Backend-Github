using System.Data.Common;
using System.Net;
using BuildingBlocks.Application.Events.Enveloping;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Consumption.Errors;

/// <summary>
/// Default implementation of error classification for integration event handler failures.
/// Uses exception type analysis to distinguish transient from permanent errors.
/// </summary>
public sealed class DefaultInboundErrorClassifier : IInboundErrorClassifier
{
    private readonly ILogger<DefaultInboundErrorClassifier> _logger;

    public DefaultInboundErrorClassifier(ILogger<DefaultInboundErrorClassifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public InboundErrorKind Classify(Exception exception, IntegrationEventEnvelope envelope, Type eventType)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(eventType);

        var classification = ClassifyException(exception);
        
        _logger.LogDebug(
            "Classified exception {ExceptionType} as {Classification} for event {EventType} in envelope {EnvelopeId}",
            exception.GetType().Name, classification, eventType.Name, envelope.EnvelopeId);

        return classification;
    }

    private static InboundErrorKind ClassifyException(Exception exception)
    {
        // Handle aggregate exceptions by examining inner exceptions
        if (exception is AggregateException aggregateException)
        {
            var innerExceptions = aggregateException.Flatten().InnerExceptions;
            
            // If any inner exception is permanent, treat the whole as permanent
            if (innerExceptions.Any(ex => ClassifyException(ex) == InboundErrorKind.Permanent))
                return InboundErrorKind.Permanent;
                
            // If any inner exception is transient, treat as transient
            if (innerExceptions.Any(ex => ClassifyException(ex) == InboundErrorKind.Transient))
                return InboundErrorKind.Transient;
                
            return InboundErrorKind.Unclassified;
        }

        // Permanent errors - business logic, validation, and data issues
        if (IsPermanentException(exception))
            return InboundErrorKind.Permanent;

        // Transient errors - infrastructure and temporary failures
        if (IsTransientException(exception))
            return InboundErrorKind.Transient;

        // Default to unclassified for unknown exception types
        return InboundErrorKind.Unclassified;
    }

    private static bool IsPermanentException(Exception exception)
    {
        return exception switch
        {
            // Argument and validation errors
            ArgumentNullException => true,
            ArgumentOutOfRangeException => true,
            ArgumentException => true,
            
            // Invalid operations and state errors
            InvalidOperationException => true,
            InvalidCastException => true,
            InvalidDataException => true,
            NotSupportedException => true,
            NotImplementedException => true,
            
            // Security and authorization errors
            UnauthorizedAccessException => true,
            SecurityException => true,
            
            // Format and serialization errors
            FormatException => true,
            OverflowException => true,
            DivideByZeroException => true,
            
            // Domain-specific permanent errors
            DomainException => true,
            ValidationException => true,
            BusinessRuleException => true,
            
            _ => false
        };
    }

    private static bool IsTransientException(Exception exception)
    {
        return exception switch
        {
            // Timeout and cancellation errors
            TimeoutException => true,
            TaskCanceledException => true,
            OperationCanceledException => true,
            
            // HTTP client errors (some status codes are transient)
            HttpRequestException httpEx => IsTransientHttpStatus(httpEx),
            
            // Database connection and timeout errors
            DbException dbEx => IsTransientDatabaseException(dbEx),
            
            System.Net.NetworkInformation.PingException => true,
            
            // Out of memory (could be temporary)
            OutOfMemoryException => true,
            
            _ => false
        };
    }

    private static bool IsTransientHttpStatus(HttpRequestException httpException)
    {
        // Extract status code from the exception message or data
        // This is a simplified approach - in practice, you might need more sophisticated parsing
        var message = httpException.Message?.ToLowerInvariant() ?? string.Empty;
        
        // Common transient HTTP status indicators
        if (message.Contains("timeout") || 
            message.Contains("429") ||  // Too Many Requests
            message.Contains("502") ||  // Bad Gateway
            message.Contains("503") ||  // Service Unavailable
            message.Contains("504"))    // Gateway Timeout
        {
            return true;
        }

        // Check HttpRequestException.Data for status code if available
        if (httpException.Data.Contains("StatusCode") && 
            httpException.Data["StatusCode"] is HttpStatusCode statusCode)
        {
            return statusCode is 
                HttpStatusCode.RequestTimeout or
                HttpStatusCode.TooManyRequests or
                HttpStatusCode.InternalServerError or
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout;
        }

        return false;
    }

    private static bool IsTransientDatabaseException(DbException dbException)
    {
        // Common database error codes that indicate transient failures
        // This is database-provider specific, but these are common patterns
        var errorMessage = dbException.Message?.ToLowerInvariant() ?? string.Empty;
        
        return errorMessage.Contains("timeout") ||
               errorMessage.Contains("connection") ||
               errorMessage.Contains("network") ||
               errorMessage.Contains("deadlock") ||
               errorMessage.Contains("lock timeout");
    }
}

// Placeholder domain exception types for classification
// In a real implementation, these would be defined in the domain layer
internal class DomainException : Exception
{
    public DomainException() { }
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

internal class ValidationException : Exception
{
    public ValidationException() { }
    public ValidationException(string message) : base(message) { }
    public ValidationException(string message, Exception innerException) : base(message, innerException) { }
}

internal class BusinessRuleException : Exception
{
    public BusinessRuleException() { }
    public BusinessRuleException(string message) : base(message) { }
    public BusinessRuleException(string message, Exception innerException) : base(message, innerException) { }
}

internal class SecurityException : Exception
{
    public SecurityException() { }
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception innerException) : base(message, innerException) { }
}