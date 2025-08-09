using Microsoft.Data.SqlClient;
using System.Net;
using System.Net.Sockets;

namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Static utility class for classifying transient faults across different technologies.
/// Provides centralized logic for determining if exceptions should trigger retry attempts.
/// </summary>
public static class TransientFaultClassifier
{
    /// <summary>
    /// SQL Server error numbers that indicate transient failures.
    /// </summary>
    public static readonly IReadOnlySet<int> TransientSqlErrorNumbers = new HashSet<int>
    {
        1205, // Deadlock
        1222, // Lock timeout
        49918, // Cannot process request. Not enough resources
        49919, // Cannot process create or update request. Too many operations
        49920, // Cannot process request. Too many resources
        10928, // Resource ID 10928. The %s limit for the database is %d and has been reached
        10929, // Resource ID 10929. The %s minimum guarantee is %d, maximum limit is %d
        40197, // The service has encountered an error processing your request
        40501, // The service is currently busy
        40613, // Database XXXX on server YYYY is not currently available
    }.ToHashSet();

    /// <summary>
    /// HTTP status codes that indicate transient failures.
    /// </summary>
    public static readonly IReadOnlySet<HttpStatusCode> TransientHttpStatusCodes = new HashSet<HttpStatusCode>
    {
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    }.ToHashSet();

    /// <summary>
    /// Socket error codes that indicate transient failures.
    /// </summary>
    public static readonly IReadOnlySet<SocketError> TransientSocketErrorCodes = new HashSet<SocketError>
    {
        SocketError.ConnectionRefused,
        SocketError.ConnectionReset,
        SocketError.ConnectionAborted,
        SocketError.NetworkUnreachable,
        SocketError.HostUnreachable,
        SocketError.TimedOut
    }.ToHashSet();

    /// <summary>
    /// Determines if a SQL exception represents a transient error.
    /// </summary>
    /// <param name="sqlException">The SQL exception to classify</param>
    /// <returns>True if the exception is transient; otherwise false</returns>
    public static bool IsSqlTransientError(SqlException sqlException)
    {
        ArgumentNullException.ThrowIfNull(sqlException);

        return TransientSqlErrorNumbers.Contains(sqlException.Number) ||
               sqlException.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               sqlException.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if an HTTP exception represents a transient error.
    /// </summary>
    /// <param name="httpException">The HTTP exception to classify</param>
    /// <returns>True if the exception is transient; otherwise false</returns>
    public static bool IsHttpTransientError(HttpRequestException httpException)
    {
        ArgumentNullException.ThrowIfNull(httpException);

        var message = httpException.Message.AsSpan();
        return message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("temporarily", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if a socket exception represents a transient error.
    /// </summary>
    /// <param name="socketException">The socket exception to classify</param>
    /// <returns>True if the exception is transient; otherwise false</returns>
    public static bool IsSocketTransientError(SocketException socketException)
    {
        ArgumentNullException.ThrowIfNull(socketException);

        return TransientSocketErrorCodes.Contains(socketException.SocketErrorCode);
    }

    /// <summary>
    /// Determines if an exception represents a transient error using a comprehensive classification.
    /// </summary>
    /// <param name="exception">The exception to classify</param>
    /// <returns>True if the exception is transient; otherwise false</returns>
    public static bool IsTransientError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            SqlException sqlEx => IsSqlTransientError(sqlEx),
            HttpRequestException httpEx => IsHttpTransientError(httpEx),
            SocketException socketEx => IsSocketTransientError(socketEx),
            TaskCanceledException => false, // Usually indicates user cancellation
            TimeoutException => true,
            InvalidOperationException invOpEx when invOpEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            InvalidOperationException invOpEx when invOpEx.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) => true,
            OperationCanceledException => false, // Don't retry cancelled operations
            _ when exception.InnerException != null => IsTransientError(exception.InnerException),
            _ => false // Conservative approach - don't retry by default
        };
    }

    /// <summary>
    /// Determines if an HTTP status code represents a transient error.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to classify</param>
    /// <returns>True if the status code is transient; otherwise false</returns>
    public static bool IsTransientHttpStatusCode(HttpStatusCode statusCode)
    {
        return TransientHttpStatusCodes.Contains(statusCode);
    }
}