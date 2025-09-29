using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.ProblemDetails;

/// <summary>
/// Maps Error types to HTTP status codes following RFC 7807 standards.
/// </summary>
public static class ErrorHttpMapping
{
    /// <summary>
    /// Maps an Error to the appropriate HTTP status code.
    /// </summary>
    /// <param name="error">The error to map</param>
    /// <returns>HTTP status code that best represents the error type</returns>
    public static int ToHttpStatusCode(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Serialization => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Concurrency => StatusCodes.Status409Conflict,
        ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Aggregate => StatusCodes.Status422UnprocessableEntity,
        ErrorType.RateLimit => StatusCodes.Status429TooManyRequests,
        ErrorType.Internal => StatusCodes.Status500InternalServerError,
        ErrorType.Configuration => StatusCodes.Status500InternalServerError,
        ErrorType.External => StatusCodes.Status502BadGateway,
        ErrorType.Network => StatusCodes.Status502BadGateway,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorType.Timeout => StatusCodes.Status504GatewayTimeout,
        ErrorType.Persistence => StatusCodes.Status507InsufficientStorage,
        ErrorType.Security => StatusCodes.Status401Unauthorized,
        ErrorType.Cancelled => 499, // Client Closed Request (non-standard but widely supported)
        _ => StatusCodes.Status500InternalServerError
        };
    }

    /// <summary>
    /// Gets the RFC 7807 problem type URI for an error.
    /// </summary>
    /// <param name="errorType">The error type</param>
    /// <param name="statusCode">The HTTP status code</param>
    /// <returns>Problem type URI</returns>
    public static string GetProblemType(ErrorType errorType, int statusCode) => errorType switch
    {
        ErrorType.Validation => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        ErrorType.Serialization => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
        ErrorType.Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
        ErrorType.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        ErrorType.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
        ErrorType.Concurrency => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
        ErrorType.PreconditionFailed => "https://tools.ietf.org/html/rfc7231#section-6.5.10",
        ErrorType.BusinessRule => "https://tools.ietf.org/html/rfc4918#section-11.2",
        ErrorType.Aggregate => "https://tools.ietf.org/html/rfc4918#section-11.2",
        ErrorType.RateLimit => "https://tools.ietf.org/html/rfc6585#section-4",
        ErrorType.Timeout => "https://tools.ietf.org/html/rfc7231#section-6.6.5",
        _ => $"https://httpstatuses.com/{statusCode}"
    };

    /// <summary>
    /// Gets a standard title for the HTTP status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code</param>
    /// <returns>Standard HTTP status title</returns>
    public static string GetProblemTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        412 => "Precondition Failed",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        499 => "Client Closed Request",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        507 => "Insufficient Storage",
        _ => "An error occurred"
    };
}