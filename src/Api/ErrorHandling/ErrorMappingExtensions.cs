using System.Globalization;
using Axon.Api.Contracts.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Http;

namespace Axon.Api.ErrorHandling;

/// <summary>
/// Maps domain Result&lt;T, Error&gt; failures to HTTP status codes and ApiError envelopes.
/// Provides privacy-safe error responses following Story 1.6 requirements.
/// </summary>
public static class ErrorMappingExtensions
{
    /// <summary>
    /// Maps domain Error to HTTP status code following Story 1.6 specification.
    /// Supports: 400 (validation), 401 (auth), 409 (conflict), 422 (business), 429 (rate limit), 500 (internal)
    /// </summary>
    /// <param name="error">Domain error to map</param>
    /// <returns>Appropriate HTTP status code</returns>
    public static int ToHttpStatusCode(this Error error) => error.Type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Serialization => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Concurrency => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Aggregate => StatusCodes.Status422UnprocessableEntity,
        ErrorType.RateLimit => StatusCodes.Status429TooManyRequests,
        ErrorType.Internal => StatusCodes.Status500InternalServerError,
        ErrorType.Configuration => StatusCodes.Status500InternalServerError,
        ErrorType.External => StatusCodes.Status500InternalServerError,
        ErrorType.Network => StatusCodes.Status500InternalServerError,
        ErrorType.Timeout => StatusCodes.Status500InternalServerError,
        ErrorType.Persistence => StatusCodes.Status500InternalServerError,
        ErrorType.Security => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>
    /// Maps domain Error to ApiError envelope with privacy-safe details.
    /// Special handling for 409 conflicts to exclude Principal IDs.
    /// </summary>
    /// <param name="error">Domain error to map</param>
    /// <returns>ApiError envelope for JSON response</returns>
    public static ApiError ToApiError(this Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation or ErrorType.Serialization =>
                MapValidationError(error),

            ErrorType.Unauthorized or ErrorType.Security =>
                ApiError.Unauthorized(error.Message),

            ErrorType.NotFound =>
                ApiError.NotFound(error.Message),

            ErrorType.Conflict or ErrorType.Concurrency =>
                MapConflictError(error),

            ErrorType.BusinessRule or ErrorType.Aggregate =>
                ApiError.BusinessRuleViolation(error.Message, ExtractSafeDetails(error)),

            ErrorType.RateLimit =>
                MapRateLimitError(error),

            _ => ApiError.InternalError("An internal error occurred")
        };
    }

    /// <summary>
    /// Maps validation errors with optional field-level details.
    /// </summary>
    private static ApiError MapValidationError(Error error)
    {
        var details = ExtractSafeDetails(error);
        return ApiError.Validation(error.Message, details);
    }

    /// <summary>
    /// Maps conflict errors with privacy-safe details.
    /// For wallet ownership conflicts, includes only chainId and address.
    /// </summary>
    private static ApiError MapConflictError(Error error)
    {
        // Check if this is a wallet ownership conflict
        if (error.Code == "WALLET_OWNERSHIP_CONFLICT" && error.Metadata != null)
        {
            var chainId = error.Metadata.TryGetValue("chainId", out var chain) ? chain?.ToString() : "solana";
            var address = error.Metadata.TryGetValue("address", out var addr) ? addr?.ToString() : "";
            
            if (!string.IsNullOrEmpty(address))
            {
                return ApiError.WalletOwnershipConflict(chainId!, address);
            }
        }

        // Generic conflict error
        return ApiError.Create("CONFLICT", error.Message, ExtractSafeDetails(error));
    }

    /// <summary>
    /// Maps rate limit errors with retry-after information.
    /// </summary>
    private static ApiError MapRateLimitError(Error error)
    {
        int? retryAfter = null;
        if (error.Metadata?.TryGetValue("RetryAfter", out var retry) == true && retry is double retrySeconds)
        {
            retryAfter = (int)retrySeconds;
        }

        return ApiError.RateLimitExceeded(error.Message, retryAfter);
    }

    /// <summary>
    /// Extracts safe metadata details excluding sensitive information.
    /// Filters out Principal IDs and other privacy-sensitive data.
    /// </summary>
    private static Dictionary<string, object>? ExtractSafeDetails(Error error)
    {
        if (error.Metadata == null || error.Metadata.Count == 0)
            return null;

        var safeDetails = new Dictionary<string, object>();
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "principalId", "userId", "ownerId", "jwt", "token", "credential", "email", "password"
        };

        foreach (var (key, value) in error.Metadata)
        {
            if (!sensitiveKeys.Contains(key) && value != null)
            {
                safeDetails[key] = value;
            }
        }

        return safeDetails.Count > 0 ? safeDetails : null;
    }

    /// <summary>
    /// Sends ApiError response with appropriate HTTP status code and headers.
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <param name="error">Domain error to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task SendApiErrorAsync(
        this HttpContext httpContext,
        Error error,
        CancellationToken cancellationToken = default)
    {
        var statusCode = error.ToHttpStatusCode();
        var apiError = error.ToApiError();

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        
        // Add rate limit headers if applicable
        if (error.Type == ErrorType.RateLimit && error.Metadata != null)
        {
            AddRateLimitHeaders(httpContext, error.Metadata);
        }

        await httpContext.Response.WriteAsJsonAsync(apiError, cancellationToken);
    }

    /// <summary>
    /// Adds rate limit headers to HTTP response.
    /// </summary>
    private static void AddRateLimitHeaders(HttpContext httpContext, IReadOnlyDictionary<string, object> metadata)
    {
        if (metadata.TryGetValue("RetryAfter", out var retryAfter) && retryAfter is double seconds)
        {
            httpContext.Response.Headers.RetryAfter = ((int)seconds).ToString(CultureInfo.InvariantCulture);
        }

        if (metadata.TryGetValue("Remaining", out var remaining))
        {
            httpContext.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString() ?? string.Empty;
        }

        if (metadata.TryGetValue("Reset", out var reset))
        {
            httpContext.Response.Headers["X-RateLimit-Reset"] = reset.ToString() ?? string.Empty;
        }
    }
}