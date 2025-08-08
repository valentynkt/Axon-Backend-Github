using Microsoft.Data.SqlClient;
using System.Net;
using System.Net.Sockets;

namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Implementation of transient fault detector for Epic 05 RetryBehavior.
/// Detects various types of transient failures that should be retried.
/// </summary>
public sealed class TransientFaultDetector : ITransientFaultDetector
{
    private static readonly HashSet<int> TransientSqlErrorNumbers = new()
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
    };

    private static readonly HashSet<HttpStatusCode> TransientHttpStatusCodes = new()
    {
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    };

    public bool IsTransient(Exception exception)
    {
        return exception switch
        {
            SqlException sqlEx => IsTransientSqlError(sqlEx),
            HttpRequestException httpEx => IsTransientHttpError(httpEx),
            SocketException socketEx => IsTransientSocketError(socketEx),
            TaskCanceledException => false, // Usually indicates user cancellation
            TimeoutException => true,
            InvalidOperationException invOpEx when invOpEx.Message.Contains("timeout") => true,
            InvalidOperationException invOpEx when invOpEx.Message.Contains("deadlock") => true,
            OperationCanceledException => false, // Don't retry cancelled operations
            _ when exception.InnerException != null => IsTransient(exception.InnerException),
            _ => false
        };
    }

    private static bool IsTransientSqlError(SqlException sqlException)
    {
        return TransientSqlErrorNumbers.Contains(sqlException.Number) ||
               sqlException.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               sqlException.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientHttpError(HttpRequestException httpException)
    {
        // Check if the exception message contains indicators of transient issues
        var message = httpException.Message.ToLowerInvariant();
        return message.Contains("timeout") ||
               message.Contains("connection") ||
               message.Contains("network") ||
               message.Contains("temporarily") ||
               message.Contains("unavailable");
    }

    private static bool IsTransientSocketError(SocketException socketException)
    {
        return socketException.SocketErrorCode switch
        {
            SocketError.ConnectionRefused => true,
            SocketError.ConnectionReset => true,
            SocketError.ConnectionAborted => true,
            SocketError.NetworkUnreachable => true,
            SocketError.HostUnreachable => true,
            SocketError.TimedOut => true,
            _ => false
        };
    }
}